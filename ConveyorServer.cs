using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VRSAutomation
{
    [HarmonyPatch(typeof(Game), "Start")]
    internal static class ConveyorServerRegisterPatch
    {
        private static void Postfix()
        {
            ConveyorServer.Register();
        }
    }

    internal static class ConveyorServer
    {
        private const string RpcApply = "VRS_Apply";
        private const string RpcResult = "VRS_ApplyResult";
        private const int OpGive = 1;
        private const int OpTake = 2;
        internal static int ItemsPerSlot = 1;
        internal static float StartupDelay = 2f;
        private const float RequestTimeout = 6f;
        private const float WorkTimeout = 20f;
        private const int MaxDepth = 4;
        private const string ItemsKey = "items";

        internal static bool LogMoves;
        private static float lastBeat;

        private static readonly int CombinerHash = Conveyor.CombinerName.GetStableHashCode();
        private static readonly int SplitterHash = Conveyor.SplitterName.GetStableHashCode();

        private static readonly Dictionary<int, Kind> kinds = new Dictionary<int, Kind>();
        private static readonly Kind NoKind = new Kind();
        private static readonly List<ZDO> conveyorBuffer = new List<ZDO>();
        private static readonly Dictionary<ZDOID, int> rotation = new Dictionary<ZDOID, int>();
        private static readonly Dictionary<ZDOID, Work> busy = new Dictionary<ZDOID, Work>();
        private static readonly Dictionary<long, Pending> pending = new Dictionary<long, Pending>();
        private static readonly Dictionary<ZDOID, float> safeUntil = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<string, float> blockedUntil = new Dictionary<string, float>();

        private static long nextRequest = DateTime.UtcNow.Ticks;
        private static float readySince = -1f;

        private sealed class Kind
        {
            public int Width;
            public int Height;
            public Smelter Machine;
            public bool Producer;
        }

        private sealed class Box
        {
            public ZDO Zdo;
            public Kind Kind;
            public int Role;
        }

        private static bool Takes(Box box, ItemDrop.ItemData item)
        {
            if (box.Kind.Producer)
            {
                return false;
            }

            if (box.Kind.Machine == null)
            {
                return true;
            }

            if (!MachineStorage.Accepts(box.Kind.Machine, item))
            {
                return false;
            }

            return box.Role == AdapterRole.Any || MachineStorage.IsFuel(box.Kind.Machine, item) == (box.Role == AdapterRole.Fuel);
        }

        private sealed class Lane
        {
            public Box Box;
            public Inventory Inventory;
            public ItemDrop.ItemData Item;
            public int Moved;
        }

        private sealed class Transfer
        {
            public ZDOID Source;
            public ZDOID Destination;
            public List<ItemDrop.ItemData> Items = new List<ItemDrop.ItemData>();
        }

        private sealed class Reply
        {
            public bool TimedOut;
            public int[] Values;
        }

        private sealed class Pending
        {
            public Action<Reply> Done;
            public float Expires;
            public long Owner;
        }

        private sealed class Work
        {
            public int Count;
            public float Expires;
        }

        private sealed class Graph
        {
            private readonly Dictionary<string, Pipe> into = new Dictionary<string, Pipe>();
            private readonly Dictionary<string, Pipe> outOf = new Dictionary<string, Pipe>();

            private static string Key(ZDOID id, int index)
            {
                return id.ToString() + "#" + index;
            }

            internal static Graph Build()
            {
                Graph graph = new Graph();
                foreach (ZDO zdo in WorldIndex.Pipes)
                {
                    byte[] bytes = zdo.GetByteArray("vrs_pipe", null);
                    Pipe pipe = bytes != null && bytes.Length > 0 ? PipeLine.Read(bytes) : null;
                    if (pipe == null)
                    {
                        continue;
                    }

                    graph.into[Key(pipe.InId, pipe.InIndex)] = pipe;
                    graph.outOf[Key(pipe.OutId, pipe.OutIndex)] = pipe;
                }

                return graph;
            }

            internal Pipe Into(ZDOID id, int index)
            {
                Pipe pipe;
                return into.TryGetValue(Key(id, index), out pipe) ? pipe : null;
            }

            internal Pipe OutOf(ZDOID id, int index)
            {
                Pipe pipe;
                return outOf.TryGetValue(Key(id, index), out pipe) ? pipe : null;
            }
        }

        internal static void Register()
        {
            if (ZRoutedRpc.instance == null)
            {
                Plugin.Log.LogWarning("Conveyor network not ready, conveyors will not move items");
                return;
            }

            try
            {
                ZRoutedRpc.instance.m_functions.Remove(RpcApply.GetStableHashCode());
                ZRoutedRpc.instance.m_functions.Remove(RpcResult.GetStableHashCode());
                ZRoutedRpc.instance.Register<ZPackage>(RpcApply, OnApply);
                ZRoutedRpc.instance.Register<ZPackage>(RpcResult, OnResult);
                Plugin.Log.LogInfo("Conveyor network registered");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Conveyor network register failed: " + e.Message);
            }
        }

        private static bool Ready()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZNetScene.instance == null || ZoneSystem.instance == null || ZRoutedRpc.instance == null)
            {
                readySince = -1f;
                return false;
            }

            if (!ZNet.instance.IsDedicated() && Player.m_localPlayer == null)
            {
                readySince = -1f;
                return false;
            }

            if (readySince < 0f)
            {
                readySince = Time.time;
            }

            return PipeServer.Resolved && Time.time - readySince >= StartupDelay;
        }

        internal static void Tick()
        {
            if (!Ready())
            {
                return;
            }

            long whole = Perf.Begin();
            Sweep();
            WorldIndex.Refresh();
            tickNumber++;
            conveyorBuffer.Clear();
            conveyorBuffer.AddRange(WorldIndex.Conveyors);
            if (conveyorBuffer.Count == 0)
            {
                return;
            }

            if (LogMoves && Time.time - lastBeat >= 300f)
            {
                lastBeat = Time.time;
                Plugin.Log.LogInfo(System.DateTime.Now.ToString("HH:mm:ss") + " Conveyor heartbeat: " + conveyorBuffer.Count + " conveyors, " + ZNet.instance.GetNrOfPlayers() + " players");
                Perf.Report();
            }

            Graph graph = cachedGraph;
            long step = Perf.Begin();
            if (graph == null || cachedGraphVersion != WorldIndex.Version)
            {
                try
                {
                    graph = Graph.Build();
                }
                catch (Exception e)
                {
                    Report("Conveyor pipe graph", e);
                    return;
                }

                cachedGraph = graph;
                cachedGraphVersion = WorldIndex.Version;
                Perf.End("conveyor tick: pipe graph rebuild", step);
            }

            step = Perf.Begin();
            foreach (ZDO conveyor in conveyorBuffer)
            {
                try
                {
                    Process(conveyor, graph);
                }
                catch (Exception e)
                {
                    busy.Remove(conveyor.m_uid);
                    Report("Conveyor " + conveyor.m_uid, e);
                }
            }

            Perf.End("conveyor tick: plan and run all conveyors", step);
            EvictBoxes();
            Perf.End("conveyor tick: total", whole);
        }

        private static Graph cachedGraph;
        private static int cachedGraphVersion = -1;
        private static int tickNumber;
        private static readonly Dictionary<ZDOID, ulong> idleSignature = new Dictionary<ZDOID, ulong>();
        private static readonly Dictionary<ZDOID, float> penaltyUntil = new Dictionary<ZDOID, float>();

        private sealed class CachedBox
        {
            public uint Revision;
            public Inventory Master;
            public float Touched;
        }

        private static readonly Dictionary<ZDOID, CachedBox> boxCache = new Dictionary<ZDOID, CachedBox>();

        private static Inventory WorkingCopy(Box box)
        {
            ZDOID id = box.Zdo.m_uid;
            uint revision = box.Zdo.DataRevision;
            CachedBox cached;
            if (!boxCache.TryGetValue(id, out cached) || cached.Revision != revision)
            {
                Inventory master = LoadInventory(box);
                if (master == null)
                {
                    return null;
                }

                cached = new CachedBox { Revision = revision, Master = master };
                boxCache[id] = cached;
            }

            cached.Touched = Time.time;
            Inventory copy = new Inventory(cached.Master.m_name, null, cached.Master.m_width, cached.Master.m_height);
            foreach (ItemDrop.ItemData item in cached.Master.GetAllItems())
            {
                ItemDrop.ItemData clone = item.Clone();
                clone.m_gridPos = item.m_gridPos;
                copy.AddItem(clone, item.m_gridPos);
            }

            return copy;
        }

        private static void EvictBoxes()
        {
            if (boxCache.Count == 0 || tickNumber % 20 != 0)
            {
                return;
            }

            List<ZDOID> old = null;
            foreach (KeyValuePair<ZDOID, CachedBox> pair in boxCache)
            {
                if (Time.time - pair.Value.Touched > 60f)
                {
                    if (old == null)
                    {
                        old = new List<ZDOID>();
                    }

                    old.Add(pair.Key);
                }
            }

            if (old != null)
            {
                foreach (ZDOID id in old)
                {
                    boxCache.Remove(id);
                }
            }
        }

        private static ulong Signature(ZDO conveyor, List<Box> sources, List<Box> destinations)
        {
            ulong hash = 14695981039346656037UL;
            Mix(ref hash, (ulong)conveyor.DataRevision);
            Mix(ref hash, (ulong)WorldIndex.Version);
            foreach (Box box in sources)
            {
                Mix(ref hash, (ulong)(uint)box.Zdo.m_uid.GetHashCode());
                Mix(ref hash, (ulong)box.Zdo.DataRevision);
            }

            Mix(ref hash, 0xFFFFUL);
            foreach (Box box in destinations)
            {
                Mix(ref hash, (ulong)(uint)box.Zdo.m_uid.GetHashCode());
                Mix(ref hash, (ulong)box.Zdo.DataRevision);
                Mix(ref hash, (ulong)box.Role);
            }

            return hash;
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            hash = (hash ^ value) * 1099511628211UL;
        }

        private static readonly HashSet<string> reported = new HashSet<string>();

        internal static void Report(string what, Exception e)
        {
            string line = what + ": " + e.GetType().Name + ": " + e.Message;
            if (reported.Count < 200 && reported.Add(line))
            {
                Plugin.Log.LogWarning(line + "\n" + e.StackTrace);
            }
        }

        private static void Process(ZDO conveyor, Graph graph)
        {
            Work work;
            if (busy.TryGetValue(conveyor.m_uid, out work))
            {
                if (Time.time < work.Expires)
                {
                    return;
                }

                busy.Remove(conveyor.m_uid);
            }

            List<Transfer> transfers = Plan(conveyor, graph);
            if (transfers == null || transfers.Count == 0)
            {
                return;
            }

            float safe;
            if (safeUntil.TryGetValue(conveyor.m_uid, out safe) && Time.time < safe)
            {
                transfers = Split(transfers);
            }

            busy[conveyor.m_uid] = new Work { Count = transfers.Count, Expires = Time.time + WorkTimeout };
            foreach (Transfer transfer in transfers)
            {
                Run(conveyor.m_uid, transfer);
            }
        }

        private static List<Transfer> Split(List<Transfer> transfers)
        {
            List<Transfer> result = new List<Transfer>();
            foreach (Transfer transfer in transfers)
            {
                foreach (ItemDrop.ItemData item in transfer.Items)
                {
                    Transfer one = new Transfer { Source = transfer.Source, Destination = transfer.Destination };
                    one.Items.Add(item);
                    result.Add(one);
                }
            }

            return result;
        }

        private static bool Blocked(ZDOID conveyor, ItemDrop.ItemData item)
        {
            float until;
            return blockedUntil.Count > 0 && blockedUntil.TryGetValue(conveyor + "|" + item.m_shared.m_name, out until) && Time.time < until;
        }

        private static void Failure(ZDOID conveyor, Transfer transfer, string reason)
        {
            idleSignature.Remove(conveyor);
            penaltyUntil[conveyor] = Time.time + 125f;
            if (transfer.Items.Count > 1)
            {
                safeUntil[conveyor] = Time.time + 120f;
                Plugin.Log.LogWarning("Conveyor " + conveyor + ": move failed (" + reason + "), moving one stack at a time for 2 minutes");
                return;
            }

            string name = transfer.Items[0].m_shared.m_name;
            blockedUntil[conveyor + "|" + name] = Time.time + 60f;
            Plugin.Log.LogWarning("Conveyor " + conveyor + ": move of " + name + " failed (" + reason + "), skipping it for 60 seconds");
        }

        private static void Finish(ZDOID conveyor)
        {
            Work work;
            if (busy.TryGetValue(conveyor, out work) && --work.Count <= 0)
            {
                busy.Remove(conveyor);
            }
        }

        private static void Sweep()
        {
            if (pending.Count == 0)
            {
                return;
            }

            List<long> expired = null;
            foreach (KeyValuePair<long, Pending> entry in pending)
            {
                if (Time.time > entry.Value.Expires)
                {
                    if (expired == null)
                    {
                        expired = new List<long>();
                    }

                    expired.Add(entry.Key);
                }
            }

            if (expired == null)
            {
                return;
            }

            foreach (long key in expired)
            {
                Pending request;
                if (pending.TryGetValue(key, out request))
                {
                    pending.Remove(key);
                    Plugin.Log.LogWarning("Conveyor request " + key + " to " + request.Owner + " timed out");
                    request.Done(new Reply { TimedOut = true });
                }
            }
        }

        private static Kind KindOfPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            Smelter smelter = prefab.GetComponent<Smelter>();
            if (smelter != null)
            {
                return MachineStorage.KindOf(smelter) != null ? new Kind { Width = 3, Height = 2, Machine = smelter } : NoKind;
            }

            if (prefab.GetComponentInChildren<Beehive>(true) != null || prefab.GetComponentInChildren<SapCollector>(true) != null)
            {
                return new Kind { Width = 3, Height = 2, Producer = true };
            }

            Container container = prefab.GetComponent<Container>();
            return container != null ? new Kind { Width = container.m_width, Height = container.m_height } : NoKind;
        }

        private static Box Describe(ZDO zdo)
        {
            if (zdo == null)
            {
                return null;
            }

            int hash = zdo.GetPrefab();
            Kind kind;
            if (!kinds.TryGetValue(hash, out kind))
            {
                kind = KindOfPrefab(ZNetScene.instance.GetPrefab(hash));
                if (kind == null)
                {
                    return null;
                }

                kinds[hash] = kind;
            }

            return kind == NoKind ? null : new Box { Zdo = zdo, Kind = kind };
        }

        private const int ProbeWidth = 40;
        private const int ProbeHeight = 30;

        private static Inventory LoadInventory(Box box)
        {
            int width = box.Kind.Width;
            int height = box.Kind.Height;
            if (box.Kind.Machine == null && !box.Kind.Producer && ZNetScene.instance != null)
            {
                Container live = LiveContainer(box.Zdo.m_uid);
                Inventory liveInventory = live != null ? live.GetInventory() : null;
                if (liveInventory != null)
                {
                    width = liveInventory.GetWidth();
                    height = liveInventory.GetHeight();
                }
            }

            Inventory inventory = new Inventory(box.Zdo.m_uid.ToString(), null, Math.Max(width, ProbeWidth), Math.Max(height, ProbeHeight));
            byte[] bytes = box.Zdo.GetByteArray(ItemsKey, null);
            if (bytes == null || bytes.Length == 0)
            {
                string text = box.Zdo.GetString(ItemsKey, string.Empty);
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        bytes = Convert.FromBase64String(text);
                    }
                    catch (FormatException)
                    {
                        bytes = null;
                    }
                }
            }

            if (bytes == null || bytes.Length == 0)
            {
                inventory.m_width = width;
                inventory.m_height = height;
                return inventory;
            }

            try
            {
                inventory.Load(new ZPackage(bytes));
                foreach (ItemDrop.ItemData stored in inventory.GetAllItems())
                {
                    width = Math.Max(width, stored.m_gridPos.x + 1);
                    height = Math.Max(height, stored.m_gridPos.y + 1);
                }

                inventory.m_width = width;
                inventory.m_height = height;
                return inventory;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Conveyor could not read the contents of " + box.Zdo.m_uid + ": " + e.Message);
                return null;
            }
        }

        private static void SaveInventory(ZDO zdo, Inventory inventory)
        {
            ZPackage pkg = new ZPackage();
            inventory.Save(pkg);
            zdo.Set(ItemsKey, pkg.GetArray());
        }

        private static Container LiveContainer(ZDOID id)
        {
            GameObject go = ZNetScene.instance.FindInstance(id);
            if (go == null)
            {
                return null;
            }

            Container container = go.GetComponent<Container>();
            return container != null ? container : go.GetComponentInChildren<Container>(true);
        }

        private static List<ItemDrop.ItemData> Extractable(Box box, Inventory inventory)
        {
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
            if (box.Kind.Machine != null || box.Kind.Producer)
            {
                foreach (Vector2i slot in MachineStorage.OutputSlots)
                {
                    ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }

                return items;
            }

            items.AddRange(inventory.GetAllItems());
            return items;
        }

        private static int Insert(Box box, Inventory inventory, ItemDrop.ItemData item, int amount)
        {
            if (box.Kind.Producer || amount <= 0)
            {
                return 0;
            }

            if (box.Kind.Machine != null)
            {
                if (!Takes(box, item))
                {
                    return 0;
                }

                ItemDrop.ItemData stack = item.Clone();
                stack.m_stack = amount;
                bool all = MachineStorage.Deposit(inventory, box.Kind.Machine, stack);
                return all ? amount : amount - stack.m_stack;
            }

            for (int k = amount; k >= 1; k--)
            {
                if (inventory.CanAddItem(item, k))
                {
                    ItemDrop.ItemData copy = item.Clone();
                    copy.m_stack = k;
                    return inventory.AddItem(copy) ? k : 0;
                }
            }

            return 0;
        }

        private static bool Same(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            if (a.m_shared.m_name != b.m_shared.m_name || a.m_quality != b.m_quality || a.m_variant != b.m_variant)
            {
                return false;
            }

            if (a.m_shared.m_maxStackSize > 1)
            {
                return true;
            }

            return a.m_crafterID == b.m_crafterID && Mathf.Abs(a.m_durability - b.m_durability) < 0.5f;
        }

        private static int Remove(Box box, Inventory inventory, ItemDrop.ItemData wanted, int amount)
        {
            int removed = 0;
            foreach (ItemDrop.ItemData item in Extractable(box, inventory))
            {
                if (removed >= amount)
                {
                    break;
                }

                if (!Same(item, wanted))
                {
                    continue;
                }

                int take = Mathf.Min(item.m_stack, amount - removed);
                if (take > 0 && inventory.RemoveItem(item, take))
                {
                    removed += take;
                }
            }

            return removed;
        }

        private static int CountOf(Inventory inventory, ItemDrop.ItemData item)
        {
            int total = 0;
            foreach (ItemDrop.ItemData other in inventory.GetAllItems())
            {
                if (other.m_shared.m_name == item.m_shared.m_name)
                {
                    total += other.m_stack;
                }
            }

            return total;
        }

        private static bool Allowed(List<FilterEntry> filter, ItemDrop.ItemData item, Inventory from, Inventory to)
        {
            if (filter.Count == 0)
            {
                return true;
            }

            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            if (prefab == null)
            {
                return false;
            }

            FilterEntry direct = null;
            bool grouped = false;
            ItemInfo info = null;
            foreach (FilterEntry entry in filter)
            {
                if (entry.Id == prefab)
                {
                    direct = entry;
                }
                else if (entry.Id.StartsWith(ItemGroups.Prefix))
                {
                    if (info == null)
                    {
                        info = new ItemInfo { Prefab = prefab, Data = item };
                    }

                    if (ItemGroups.Matches(entry.Id.Substring(ItemGroups.Prefix.Length), info))
                    {
                        grouped = true;
                    }
                }
            }

            if (direct == null)
            {
                return grouped;
            }

            if (direct.Min > 0 && CountOf(from, item) <= direct.Min)
            {
                return false;
            }

            if (direct.Max > 0 && CountOf(to, item) >= direct.Max)
            {
                return false;
            }

            return true;
        }

        private static void CollectSources(Graph graph, Pipe pipe, List<Box> sources, int depth)
        {
            if (pipe == null || depth > MaxDepth)
            {
                return;
            }

            ZDO zdo = AdapterLink.Resolve(ZDOMan.instance.GetZDO(pipe.OutId));
            if (zdo == null)
            {
                return;
            }

            if (zdo.GetPrefab() == CombinerHash)
            {
                for (int k = 0; k < 3; k++)
                {
                    CollectSources(graph, graph.Into(pipe.OutId, k), sources, depth + 1);
                }

                return;
            }

            if (zdo.GetPrefab() == SplitterHash)
            {
                CollectSources(graph, graph.Into(pipe.OutId, 0), sources, depth + 1);
                return;
            }

            Box box = Describe(zdo);
            if (box != null && !sources.Exists(other => other.Zdo.m_uid.Equals(box.Zdo.m_uid)))
            {
                sources.Add(box);
            }
        }

        private static void CollectDestinations(Graph graph, Pipe pipe, List<Box> destinations, int depth)
        {
            if (pipe == null || depth > MaxDepth)
            {
                return;
            }

            ZDO endpoint = ZDOMan.instance.GetZDO(pipe.InId);
            ZDO zdo = AdapterLink.Resolve(endpoint);
            if (zdo == null)
            {
                return;
            }

            if (zdo.GetPrefab() == SplitterHash)
            {
                for (int k = 1; k <= 3; k++)
                {
                    CollectDestinations(graph, graph.OutOf(pipe.InId, k), destinations, depth + 1);
                }

                return;
            }

            Box box = Describe(zdo);
            if (box == null)
            {
                return;
            }

            box.Role = endpoint != null && AdapterLink.IsAdapter(endpoint) ? Mathf.Max(0, endpoint.GetInt(AdapterRole.Key, AdapterRole.Any)) : AdapterRole.Any;
            Box existing = destinations.Find(other => other.Zdo.m_uid.Equals(box.Zdo.m_uid));
            if (existing == null)
            {
                destinations.Add(box);
            }
            else if (existing.Role != box.Role)
            {
                existing.Role = AdapterRole.Any;
            }
        }

        private static Inventory Simulated(Dictionary<ZDOID, Inventory> sim, Box box)
        {
            Inventory inventory;
            if (!sim.TryGetValue(box.Zdo.m_uid, out inventory))
            {
                inventory = WorkingCopy(box);
                if (inventory != null)
                {
                    sim[box.Zdo.m_uid] = inventory;
                }
            }

            return inventory;
        }

        private static void Record(List<Transfer> transfers, ZDOID source, ZDOID destination, ItemDrop.ItemData item)
        {
            Transfer transfer = null;
            foreach (Transfer candidate in transfers)
            {
                if (candidate.Source.Equals(source) && candidate.Destination.Equals(destination))
                {
                    transfer = candidate;
                    break;
                }
            }

            if (transfer == null)
            {
                transfer = new Transfer { Source = source, Destination = destination };
                transfers.Add(transfer);
            }

            foreach (ItemDrop.ItemData existing in transfer.Items)
            {
                if (Same(existing, item) && existing.m_stack < existing.m_shared.m_maxStackSize)
                {
                    existing.m_stack++;
                    return;
                }
            }

            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = 1;
            transfer.Items.Add(copy);
        }

        private static List<Transfer> Plan(ZDO conveyor, Graph graph)
        {
            ZDOID id = conveyor.m_uid;
            if (!ConveyorSwitch.IsOn(conveyor))
            {
                return null;
            }

            Pipe sourcePipe = graph.Into(id, 0);
            Pipe destinationPipe = graph.OutOf(id, 1);
            if (sourcePipe == null || destinationPipe == null)
            {
                SetStatus(conveyor, sourcePipe == null ? ConveyorSwitch.StatusNoInputPipe : ConveyorSwitch.StatusNoOutputPipe);
                Skip(id, sourcePipe == null ? "no pipe on the input" : "no pipe on the output");
                return null;
            }

            List<Box> sources = new List<Box>();
            List<Box> destinations = new List<Box>();
            CollectSources(graph, sourcePipe, sources, 0);
            CollectDestinations(graph, destinationPipe, destinations, 0);
            int foundSources = sources.Count;
            int foundDestinations = destinations.Count;

            ulong signature = Signature(conveyor, sources, destinations);
            ulong previous;
            float penalty;
            bool penalised = penaltyUntil.TryGetValue(id, out penalty) && Time.time < penalty;
            if (!penalised && idleSignature.TryGetValue(id, out previous) && previous == signature)
            {
                return null;
            }

            Dictionary<ZDOID, Inventory> sim = new Dictionary<ZDOID, Inventory>();
            sources.RemoveAll(box => Simulated(sim, box) == null);
            destinations.RemoveAll(box => Simulated(sim, box) == null);
            if (sources.Count == 0 || destinations.Count == 0)
            {
                SetStatus(conveyor, ConveyorSwitch.StatusNoBoxes);
                Skip(id, "sources found " + foundSources + " readable " + sources.Count + ", destinations found " + foundDestinations + " readable " + destinations.Count);
                return null;
            }

            List<Lane> lanes = new List<Lane>();
            foreach (Box source in sources)
            {
                Inventory inventory = sim[source.Zdo.m_uid];
                foreach (ItemDrop.ItemData item in Extractable(source, inventory))
                {
                    lanes.Add(new Lane { Box = source, Inventory = inventory, Item = item });
                }
            }

            if (lanes.Count == 0)
            {
                SetStatus(conveyor, ConveyorSwitch.StatusSourceEmpty);
                Skip(id, "the " + sources.Count + " source box(es) hold nothing it can take");
                idleSignature[id] = signature;
                return null;
            }

            int start;
            rotation.TryGetValue(id, out start);
            rotation[id] = start + 1;

            List<FilterEntry> filter = ConveyorFilterWindow.Read(conveyor);
            List<Transfer> transfers = new List<Transfer>();
            int perSlot = ConveyorServer.ItemsPerSlot;
            int budget = lanes.Count * perSlot;
            int step = 0;
            int failures = 0;
            int maxFailures = destinations.Count * lanes.Count;
            while (budget > 0 && failures < maxFailures)
            {
                Box destination = destinations[(start + step) % destinations.Count];
                Lane lane = lanes[(start + step / destinations.Count) % lanes.Count];
                step++;

                Inventory destinationInventory = sim[destination.Zdo.m_uid];
                if (lane.Moved >= perSlot
                    || lane.Box.Zdo.m_uid.Equals(destination.Zdo.m_uid)
                    || lane.Item.m_stack <= 0
                    || Blocked(id, lane.Item)
                    || !lane.Inventory.ContainsItem(lane.Item)
                    || !Allowed(filter, lane.Item, lane.Inventory, destinationInventory)
                    || Insert(destination, destinationInventory, lane.Item, 1) <= 0)
                {
                    failures++;
                    continue;
                }

                Record(transfers, lane.Box.Zdo.m_uid, destination.Zdo.m_uid, lane.Item);
                lane.Inventory.RemoveItem(lane.Item, 1);
                lane.Moved++;
                budget--;
                failures = 0;
            }

            if (LogMoves && transfers.Count == 0)
            {
                float last;
                if (!lastDiagnosis.TryGetValue(id, out last) || Time.time - last > 20f)
                {
                    lastDiagnosis[id] = Time.time;
                    Plugin.Log.LogInfo("Conveyor " + id + " moved nothing: " + Diagnose(lanes, destinations, sim, filter));
                }
            }

            if (transfers.Count == 0)
            {
                idleSignature[id] = signature;
                SetStatus(conveyor, WhyNothing(lanes, destinations, sim, filter));
            }
            else
            {
                idleSignature.Remove(id);
                SetStatus(conveyor, ConveyorSwitch.StatusMoving);
            }

            return transfers;
        }

        private static void SetStatus(ZDO conveyor, int status)
        {
            if (conveyor.GetInt(ConveyorSwitch.StatusKey, ConveyorSwitch.StatusUnknown) != status)
            {
                conveyor.Set(ConveyorSwitch.StatusKey, status);
            }
        }

        private static int WhyNothing(List<Lane> lanes, List<Box> destinations, Dictionary<ZDOID, Inventory> sim, List<FilterEntry> filter)
        {
            bool noRoom = false;
            bool rejects = false;
            bool filtered = false;
            foreach (Lane lane in lanes)
            {
                foreach (Box destination in destinations)
                {
                    if (lane.Box.Zdo.m_uid.Equals(destination.Zdo.m_uid) || lane.Item.m_stack <= 0)
                    {
                        continue;
                    }

                    if (!Allowed(filter, lane.Item, lane.Inventory, sim[destination.Zdo.m_uid]))
                    {
                        filtered = true;
                    }
                    else if (destination.Kind.Producer || (destination.Kind.Machine != null && !Takes(destination, lane.Item)))
                    {
                        rejects = true;
                    }
                    else
                    {
                        noRoom = true;
                    }
                }
            }

            if (noRoom)
            {
                return ConveyorSwitch.StatusDestinationFull;
            }

            if (rejects)
            {
                return ConveyorSwitch.StatusDestinationRejects;
            }

            return filtered ? ConveyorSwitch.StatusNoMatch : ConveyorSwitch.StatusSourceEmpty;
        }

        private static readonly Dictionary<ZDOID, float> lastDiagnosis = new Dictionary<ZDOID, float>();

        private static void Skip(ZDOID id, string reason)
        {
            if (!LogMoves)
            {
                return;
            }

            float last;
            if (lastDiagnosis.TryGetValue(id, out last) && Time.time - last <= 20f)
            {
                return;
            }

            lastDiagnosis[id] = Time.time;
            Plugin.Log.LogInfo("Conveyor " + id + " skipped: " + reason);
        }

        private static string Diagnose(List<Lane> lanes, List<Box> destinations, Dictionary<ZDOID, Inventory> sim, List<FilterEntry> filter)
        {
            List<string> reasons = new List<string>();
            foreach (Lane lane in lanes)
            {
                string name = lane.Item.m_dropPrefab != null ? lane.Item.m_dropPrefab.name : "(no prefab) " + lane.Item.m_shared.m_name;
                foreach (Box destination in destinations)
                {
                    Inventory target = sim[destination.Zdo.m_uid];
                    string why;
                    if (lane.Box.Zdo.m_uid.Equals(destination.Zdo.m_uid))
                    {
                        why = "same box";
                    }
                    else if (lane.Item.m_stack <= 0)
                    {
                        why = "empty stack";
                    }
                    else if (!Allowed(filter, lane.Item, lane.Inventory, target))
                    {
                        why = "filter";
                    }
                    else if (destination.Kind.Producer)
                    {
                        why = "destination takes nothing";
                    }
                    else if (destination.Kind.Machine != null && !Takes(destination, lane.Item))
                    {
                        why = destination.Role == AdapterRole.Any ? "machine does not accept it" : "this input only takes " + (destination.Role == AdapterRole.Fuel ? "fuel" : "ore");
                    }
                    else
                    {
                        why = "no room";
                    }

                    string line = name + " x" + lane.Item.m_stack + " -> " + why;
                    if (!reasons.Contains(line) && reasons.Count < 6)
                    {
                        reasons.Add(line);
                    }
                }
            }

            return string.Join("; ", reasons.ToArray());
        }

        private static List<ItemDrop.ItemData> Scaled(List<ItemDrop.ItemData> items, int[] counts)
        {
            List<ItemDrop.ItemData> result = new List<ItemDrop.ItemData>();
            for (int i = 0; i < items.Count && i < counts.Length; i++)
            {
                if (counts[i] > 0)
                {
                    ItemDrop.ItemData copy = items[i].Clone();
                    copy.m_stack = counts[i];
                    result.Add(copy);
                }
            }

            return result;
        }

        private static string Summary(List<ItemDrop.ItemData> items)
        {
            List<string> parts = new List<string>();
            foreach (ItemDrop.ItemData item in items)
            {
                parts.Add(item.m_stack + " " + (item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name));
            }

            return string.Join(", ", parts.ToArray());
        }

        private static void Run(ZDOID conveyor, Transfer transfer)
        {
            Send(transfer.Destination, OpGive, transfer.Items, give =>
            {
                if (give.Values == null)
                {
                    if (give.TimedOut)
                    {
                        Plugin.Log.LogWarning("Conveyor " + conveyor + ": no answer from the destination " + transfer.Destination + ", nothing was removed from the source");
                    }
                    else
                    {
                        if (LogMoves)
                        {
                            Plugin.Log.LogInfo("Conveyor " + conveyor + ": the destination " + transfer.Destination + " could not take " + Summary(transfer.Items) + " (error reply)");
                        }

                        Failure(conveyor, transfer, "destination " + transfer.Destination + " error reply");
                    }

                    Finish(conveyor);
                    return;
                }

                List<ItemDrop.ItemData> accepted = Scaled(transfer.Items, give.Values);
                if (accepted.Count == 0)
                {
                    if (LogMoves)
                    {
                        Plugin.Log.LogInfo("Conveyor " + conveyor + ": the destination " + transfer.Destination + " accepted none of " + Summary(transfer.Items));
                    }

                    Finish(conveyor);
                    return;
                }

                Send(transfer.Source, OpTake, accepted, take =>
                {
                    List<ItemDrop.ItemData> back;
                    if (take.Values != null)
                    {
                        List<ItemDrop.ItemData> missing = new List<ItemDrop.ItemData>();
                        for (int i = 0; i < accepted.Count; i++)
                        {
                            int shortfall = accepted[i].m_stack - (i < take.Values.Length ? take.Values[i] : 0);
                            if (shortfall > 0)
                            {
                                ItemDrop.ItemData copy = accepted[i].Clone();
                                copy.m_stack = shortfall;
                                missing.Add(copy);
                            }
                        }

                        back = missing;
                    }
                    else if (take.TimedOut)
                    {
                        Plugin.Log.LogWarning("Conveyor " + conveyor + ": no answer from the source " + transfer.Source + ", items may now exist in both boxes: " + Summary(accepted));
                        back = new List<ItemDrop.ItemData>();
                    }
                    else
                    {
                        if (LogMoves)
                        {
                            Plugin.Log.LogInfo("Conveyor " + conveyor + ": the source " + transfer.Source + " could not give " + Summary(accepted) + " (error reply), giving it back");
                        }

                        Failure(conveyor, transfer, "source " + transfer.Source + " error reply");
                        back = accepted;
                    }

                    if (LogMoves && take.Values != null)
                    {
                        Plugin.Log.LogInfo(System.DateTime.Now.ToString("HH:mm:ss") + " Conveyor " + conveyor + " moved " + Summary(accepted) + " from " + transfer.Source + " to " + transfer.Destination);
                    }

                    if (back.Count == 0)
                    {
                        Finish(conveyor);
                        return;
                    }

                    Send(transfer.Destination, OpTake, back, undo =>
                    {
                        if (undo.Values == null)
                        {
                            Plugin.Log.LogWarning("Conveyor " + conveyor + ": could not take back " + Summary(back) + " from " + transfer.Destination);
                        }

                        Finish(conveyor);
                    });
                });
            });
        }

        private static byte[] SaveCargo(List<ItemDrop.ItemData> items)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Inventory single = new Inventory("cargo", null, 1, 1);
                ItemDrop.ItemData copy = items[i].Clone();
                Vector2i pos = new Vector2i(0, 0);
                copy.m_gridPos = pos;
                single.AddItem(copy, pos);
                ZPackage part = new ZPackage();
                single.Save(part);
                pkg.Write(part.GetArray());
            }

            return pkg.GetArray();
        }

        private static List<ItemDrop.ItemData> LoadCargo(byte[] bytes)
        {
            try
            {
                ZPackage pkg = new ZPackage(bytes);
                int count = pkg.ReadInt();
                if (count < 1 || count > 256)
                {
                    return null;
                }

                List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(count);
                for (int i = 0; i < count; i++)
                {
                    Inventory single = new Inventory("cargo", null, 1, 1);
                    single.Load(new ZPackage(pkg.ReadByteArray()));
                    List<ItemDrop.ItemData> loaded = single.GetAllItems();
                    if (loaded.Count != 1)
                    {
                        Plugin.Log.LogWarning("Conveyor cargo entry " + i + " loaded " + loaded.Count + " items");
                        return null;
                    }

                    items.Add(loaded[0]);
                }

                return items;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Conveyor cargo could not be read: " + e.Message);
                return null;
            }
        }

        private static void Send(ZDOID id, int op, List<ItemDrop.ItemData> items, Action<Reply> done)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(id);
            if (zdo == null || items.Count == 0)
            {
                done(new Reply());
                return;
            }

            long me = ZDOMan.GetSessionID();
            if (!zdo.HasOwner())
            {
                zdo.SetOwner(me);
            }

            byte[] cargo = SaveCargo(items);
            long owner = zdo.GetOwner();
            if (owner == me)
            {
                int[] values = null;
                try
                {
                    values = Apply(op, id, cargo);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Conveyor request failed: " + e.Message);
                }

                done(new Reply { Values = values });
                return;
            }

            long request = ++nextRequest;
            pending[request] = new Pending { Done = done, Expires = Time.time + RequestTimeout, Owner = owner };
            ZPackage pkg = new ZPackage();
            pkg.Write(request);
            pkg.Write(op);
            pkg.Write(id);
            pkg.Write(cargo);
            ZRoutedRpc.instance.InvokeRoutedRPC(owner, RpcApply, pkg);
        }

        private static void OnApply(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID())
            {
                Plugin.Log.LogWarning("Conveyor request ignored, it did not come from the server (sender " + sender + ")");
                return;
            }

            long request = pkg.ReadLong();
            int op = pkg.ReadInt();
            ZDOID id = pkg.ReadZDOID();
            byte[] cargo = pkg.ReadByteArray();

            int[] result = null;
            try
            {
                result = Apply(op, id, cargo);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Conveyor request failed: " + e.Message);
            }

            ZPackage reply = new ZPackage();
            reply.Write(request);
            reply.Write(result == null ? -1 : result.Length);
            if (result != null)
            {
                foreach (int value in result)
                {
                    reply.Write(value);
                }
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcResult, reply);
        }

        private static void OnResult(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }

            long request = pkg.ReadLong();
            int count = pkg.ReadInt();
            Pending waiting;
            if (!pending.TryGetValue(request, out waiting) || waiting.Owner != sender)
            {
                return;
            }

            pending.Remove(request);
            Reply reply = new Reply();
            if (count >= 0)
            {
                reply.Values = new int[count];
                for (int i = 0; i < count; i++)
                {
                    reply.Values[i] = pkg.ReadInt();
                }
            }

            waiting.Done(reply);
        }

        internal static string SelfTest()
        {
            if (ObjectDB.instance == null)
            {
                return "Self-test: the item database is not ready";
            }

            Box box = new Box { Zdo = null, Kind = new Kind { Width = 8, Height = 4 } };
            int tested = 0;
            List<string> failures = new List<string>();
            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null || string.IsNullOrEmpty(drop.m_itemData.m_shared.m_name))
                {
                    continue;
                }

                tested++;
                string problem;
                try
                {
                    problem = TestItem(box, prefab, drop.m_itemData);
                }
                catch (Exception e)
                {
                    problem = "exception " + e.Message;
                }

                if (problem != null)
                {
                    failures.Add(prefab.name + ": " + problem);
                }
            }

            foreach (string failure in failures)
            {
                Plugin.Log.LogWarning("Self-test " + failure);
            }

            return "Self-test: " + tested + " items, " + failures.Count + " problems" + (failures.Count > 0 ? ", first: " + failures[0] + " (all in the log)" : string.Empty);
        }

        private static ItemDrop.ItemData Sample(GameObject prefab, ItemDrop.ItemData template, int stack, int quality, long crafter)
        {
            ItemDrop.ItemData item = template.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = Mathf.Max(1, stack);
            item.m_quality = Mathf.Clamp(quality, 1, Mathf.Max(1, template.m_shared.m_maxQuality));
            item.m_crafterID = crafter;
            item.m_crafterName = crafter == 0 ? string.Empty : "tester";
            return item;
        }

        private static string TestItem(Box box, GameObject prefab, ItemDrop.ItemData template)
        {
            int max = Mathf.Max(1, template.m_shared.m_maxStackSize);
            int half = Mathf.Max(1, max / 2);
            List<List<ItemDrop.ItemData>> cases = new List<List<ItemDrop.ItemData>>
            {
                new List<ItemDrop.ItemData> { Sample(prefab, template, max, 1, 0) },
                new List<ItemDrop.ItemData> { Sample(prefab, template, half, 1, 0), Sample(prefab, template, max - half, 2, 7) },
                new List<ItemDrop.ItemData> { Sample(prefab, template, 1, 1, 0), Sample(prefab, template, max, 1, 0), Sample(prefab, template, 1, 2, 5) }
            };

            for (int c = 0; c < cases.Count; c++)
            {
                string problem = TestCase(box, cases[c]);
                if (problem != null)
                {
                    return "case " + c + ": " + problem;
                }
            }

            return null;
        }

        private static string TestCase(Box box, List<ItemDrop.ItemData> entries)
        {
            List<ItemDrop.ItemData> loaded = LoadCargo(SaveCargo(entries));
            if (loaded == null)
            {
                return "cargo could not be read back";
            }

            if (loaded.Count != entries.Count)
            {
                return "cargo had " + entries.Count + " entries and came back with " + loaded.Count;
            }

            int expected = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (loaded[i].m_stack != entries[i].m_stack || loaded[i].m_shared.m_name != entries[i].m_shared.m_name)
                {
                    return "entry " + i + " changed from " + entries[i].m_stack + " to " + loaded[i].m_stack;
                }

                expected += loaded[i].m_stack;
            }

            Inventory destination = new Inventory("selftest", null, 8, 4);
            int inserted = 0;
            foreach (ItemDrop.ItemData item in loaded)
            {
                inserted += Insert(box, destination, item, item.m_stack);
            }

            if (inserted != expected)
            {
                return "destination accepted " + inserted + " of " + expected;
            }

            Inventory source = new Inventory("selftest", null, 8, 4);
            foreach (ItemDrop.ItemData item in entries)
            {
                source.AddItem(item.Clone());
            }

            int removed = 0;
            foreach (ItemDrop.ItemData item in loaded)
            {
                removed += Remove(box, source, item, item.m_stack);
            }

            return removed != expected ? "source gave " + removed + " of " + expected : null;
        }

        private static int[] ApplyFailed(ZDOID id, string reason)
        {
            if (LogMoves)
            {
                Plugin.Log.LogWarning("Conveyor apply on " + id + " failed: " + reason);
            }

            return null;
        }

        private static int[] Apply(int op, ZDOID id, byte[] cargoBytes)
        {
            if (ZDOMan.instance == null || ZNetScene.instance == null)
            {
                return ApplyFailed(id, "world not ready");
            }

            ZDO zdo = ZDOMan.instance.GetZDO(id);
            if (zdo == null)
            {
                return ApplyFailed(id, "box no longer exists");
            }

            if (!zdo.IsOwner())
            {
                return ApplyFailed(id, "not the owner of the box (owner " + zdo.GetOwner() + ", me " + ZDOMan.GetSessionID() + ")");
            }

            Box box = Describe(zdo);
            List<ItemDrop.ItemData> items = LoadCargo(cargoBytes);
            if (box == null)
            {
                return ApplyFailed(id, "box type not recognized");
            }

            if (items == null)
            {
                return ApplyFailed(id, "cargo could not be read");
            }

            Container live = LiveContainer(id);
            Inventory inventory = live != null ? live.GetInventory() : LoadInventory(box);
            if (inventory == null)
            {
                return ApplyFailed(id, "box contents could not be read");
            }

            int[] result = new int[items.Count];
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                result[i] = op == OpGive ? Insert(box, inventory, items[i], items[i].m_stack) : Remove(box, inventory, items[i], items[i].m_stack);
                changed |= result[i] > 0;
            }

            if (changed && live == null)
            {
                SaveInventory(zdo, inventory);
            }

            return result;
        }
    }
}

namespace VRSAutomation
{
    internal static class Perf
    {
        private sealed class Stat
        {
            public int Count;
            public double Total;
            public double Max;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Stat> stats = new System.Collections.Generic.Dictionary<string, Stat>();

        internal static long Begin()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        internal static void End(string name, long start)
        {
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Stat stat;
            if (!stats.TryGetValue(name, out stat))
            {
                stat = new Stat();
                stats[name] = stat;
            }

            stat.Count++;
            stat.Total += ms;
            if (ms > stat.Max)
            {
                stat.Max = ms;
            }
        }

        internal static void Report()
        {
            foreach (System.Collections.Generic.KeyValuePair<string, Stat> pair in stats)
            {
                Plugin.Log.LogInfo("Timing " + pair.Key + ": " + pair.Value.Count + " runs, average " + (pair.Value.Total / pair.Value.Count).ToString("F2") + " ms, worst " + pair.Value.Max.ToString("F2") + " ms");
            }

            stats.Clear();
        }
    }
}
