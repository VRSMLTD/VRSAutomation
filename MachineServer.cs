using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRSAutomation
{
    internal static partial class MachineServer
    {
        internal static bool Enabled = true;
        internal static float Interval = 30f;

        private const string ItemsKey = "items";
        private const float MaxElapsed = 7f * 24f * 3600f;
        private const float StartDelay = 15f;

        internal static float WindmillPower = 0.5f;
        internal static readonly int RootPosKey = "vrs_root_pos".GetStableHashCode();
        internal static readonly int RootPrefabKey = "vrs_root_prefab".GetStableHashCode();

        internal static readonly string[] SmelterPrefabs = { "smelter", "blastfurnace", "charcoal_kiln", "eitrrefinery", "piece_spinningwheel", "piece_FrostKiln", "windmill" };
        internal static readonly string[] HivePrefabs = { "piece_beehive", "piece_birdnest", "piece_sapcollector" };

        private static readonly List<ZDO> buffer = new List<ZDO>();
        private static float readySince = -1f;
        private static float nextRun;
        private static float nextReport;
        private static int caughtUp;
        private static int skippedLoaded;
        private static int skippedOwned;
        private static long lastOwner;

        internal static void Tick()
        {
            if (!Enabled || !Ready() || Time.time < nextRun)
            {
                return;
            }

            nextRun = Time.time + Mathf.Max(5f, Interval);
            long pass = Perf.Begin();
            try
            {
                float ageMinutes = TakeAgeRequest();
                WorldIndex.Refresh();
                buffer.Clear();
                buffer.AddRange(WorldIndex.Machines);
                foreach (ZDO zdo in buffer)
                {
                    Safely(zdo, ageMinutes, true);
                }

                buffer.Clear();
                buffer.AddRange(WorldIndex.Hives);
                foreach (ZDO zdo in buffer)
                {
                    Safely(zdo, ageMinutes, false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Machine catch-up failed: " + e.Message);
            }

            Perf.End("machine catch-up: whole pass (10 world scans)", pass);

            if (ConveyorServer.LogMoves && Time.time >= nextReport)
            {
                nextReport = Time.time + 300f;
                Plugin.Log.LogInfo(DateTime.Now.ToString("HH:mm:ss") + " Machine catch-up heartbeat: " + caughtUp + " machines advanced, " + skippedOwned + " owned by a player (last owner " + lastOwner + "), " + skippedLoaded + " with a live instance on the server");
                caughtUp = 0;
                skippedLoaded = 0;
                skippedOwned = 0;
            }
        }

        private static bool Ready()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZNetScene.instance == null || ZoneSystem.instance == null || ObjectDB.instance == null || WorldGenerator.instance == null)
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

            return PipeServer.Resolved && Time.time - readySince >= StartDelay;
        }

        static partial void AgeRequest(ref float minutes);

        private static float TakeAgeRequest()
        {
            float minutes = 0f;
            AgeRequest(ref minutes);
            return minutes;
        }

        private static bool Free(ZDO zdo, bool count)
        {
            long owner = zdo.GetOwner();
            bool foreignOwner = owner != 0L && owner != ZDOMan.GetSessionID() && ZDOMan.instance.IsInPeerActiveArea(zdo.GetPosition(), owner);
            bool hasInstance = ZNetScene.instance.FindInstance(zdo.m_uid) != null;
            if (foreignOwner || hasInstance)
            {
                if (count)
                {
                    if (foreignOwner)
                    {
                        skippedOwned++;
                        lastOwner = owner;
                    }

                    if (hasInstance)
                    {
                        skippedLoaded++;
                    }
                }

                return false;
            }

            if (owner != ZDOMan.GetSessionID())
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
            }

            return true;
        }

        private static void Safely(ZDO zdo, float ageMinutes, bool smelter)
        {
            try
            {
                Eligible(zdo, ageMinutes, smelter);
            }
            catch (Exception e)
            {
                ConveyorServer.Report("Machine " + zdo.m_uid, e);
            }
        }

        private static void Eligible(ZDO zdo, float ageMinutes, bool smelter)
        {
            if (!Free(zdo, true))
            {
                return;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            if (prefab == null)
            {
                return;
            }

            if (smelter)
            {
                Smelter component = prefab.GetComponent<Smelter>();
                if (component != null && MachineStorage.KindOf(component) != null)
                {
                    CatchUpSmelter(zdo, component, ageMinutes);
                }

                return;
            }

            Beehive hive = prefab.GetComponentInChildren<Beehive>(true);
            if (hive != null)
            {
                CatchUpHive(zdo, hive, ageMinutes);
                return;
            }

            SapCollector sap = prefab.GetComponentInChildren<SapCollector>(true);
            if (sap != null)
            {
                CatchUpSap(zdo, sap, ageMinutes);
            }
        }

        private static ZDO FindRoot(string name, Vector3 position)
        {
            return WorldIndex.Near(name, position, 3f);
        }

        private static void CatchUpSap(ZDO zdo, SapCollector sap, float ageMinutes)
        {
            string rootName = zdo.GetString(RootPrefabKey, string.Empty);
            if (rootName.Length == 0 || sap.m_spawnItem == null || sap.m_secPerUnit <= 0f)
            {
                return;
            }

            DateTime now = ZNet.instance.GetTime();
            long lastTicks = zdo.GetLong(ZDOVars.s_lastTime, 0L);
            if (lastTicks == 0L)
            {
                zdo.Set(ZDOVars.s_lastTime, now.Ticks);
                return;
            }

            if (ageMinutes > 0f)
            {
                lastTicks -= (long)(ageMinutes * 60f * TimeSpan.TicksPerSecond);
                zdo.Set(ZDOVars.s_lastTime, lastTicks);
            }

            double elapsed = (now - new DateTime(lastTicks)).TotalSeconds;
            if (elapsed < 1.0)
            {
                return;
            }

            if (elapsed > MaxElapsed)
            {
                elapsed = MaxElapsed;
            }

            ZDO root = FindRoot(rootName, zdo.GetVec3(RootPosKey, Vector3.zero));
            if (root == null || !Free(root, false))
            {
                return;
            }

            GameObject rootPrefab = ZNetScene.instance.GetPrefab(root.GetPrefab());
            ResourceRoot rootData = rootPrefab != null ? rootPrefab.GetComponentInChildren<ResourceRoot>(true) : null;
            if (rootData == null)
            {
                return;
            }

            float rootLevel = root.GetFloat(ZDOVars.s_level, rootData.m_maxLevel);
            long rootLast = root.GetLong(ZDOVars.s_lastTime, now.Ticks);
            double rootElapsed = Math.Max(0.0, (now - new DateTime(rootLast)).TotalSeconds);
            rootLevel = Mathf.Clamp((float)(rootLevel + rootData.m_regenPerSec * rootElapsed), 0f, rootData.m_maxLevel);

            zdo.Set(ZDOVars.s_lastTime, now.Ticks);
            root.Set(ZDOVars.s_lastTime, now.Ticks);
            caughtUp++;

            int level = zdo.GetInt(ZDOVars.s_level, 0);
            double total = zdo.GetFloat(ZDOVars.s_product, 0f) + elapsed;
            int byTime = (int)(total / sap.m_secPerUnit);
            zdo.Set(ZDOVars.s_product, (float)(total - byTime * (double)sap.m_secPerUnit));

            int units = rootLevel > 1f ? Mathf.Min(byTime, (int)rootLevel) : 0;
            int stored = 0;
            int toLevel = 0;
            if (units > 0)
            {
                Inventory inventory = Load(zdo, 3, 2);
                if (inventory != null)
                {
                    int left = MachineStorage.AddOutput(inventory, sap.m_spawnItem, units);
                    stored = units - left;
                    if (stored > 0)
                    {
                        Save(zdo, inventory);
                    }

                    toLevel = Mathf.Min(left, Mathf.Max(0, sap.m_maxLevel - level));
                    if (toLevel > 0)
                    {
                        zdo.Set(ZDOVars.s_level, level + toLevel);
                    }
                }
            }

            int produced = stored + toLevel;
            root.Set(ZDOVars.s_level, rootLevel - produced);

            if (ConveyorServer.LogMoves)
            {
                Plugin.Log.LogInfo(DateTime.Now.ToString("HH:mm:ss") + " Machine " + zdo.m_uid + " (" + sap.m_spawnItem.gameObject.name + ") caught up " + ((int)elapsed) + " s: "
                    + byTime + " due, " + stored + " into slots, " + toLevel + " kept in the game counter, root level " + rootLevel.ToString("F1") + " to " + (rootLevel - produced).ToString("F1"));
            }
        }

        private static Inventory Load(ZDO zdo, int width, int height)
        {
            Inventory inventory = new Inventory(zdo.m_uid.ToString(), null, width, height);
            byte[] bytes = zdo.GetByteArray(ItemsKey, null);
            if (bytes == null || bytes.Length == 0)
            {
                return inventory;
            }

            try
            {
                inventory.Load(new ZPackage(bytes));
                return inventory;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Machine catch-up could not read the contents of " + zdo.m_uid + ": " + e.Message);
                return null;
            }
        }

        private static void Save(ZDO zdo, Inventory inventory)
        {
            ZPackage package = new ZPackage();
            inventory.Save(package);
            zdo.Set(ItemsKey, package.GetArray());
        }

        private static void CatchUpSmelter(ZDO zdo, Smelter smelter, float ageMinutes)
        {
            MachineStorage.MachineKind kind = MachineStorage.KindOf(smelter);
            DateTime now = ZNet.instance.GetTime();
            long startTicks = zdo.GetLong(ZDOVars.s_startTime, 0L);
            if (startTicks == 0L)
            {
                zdo.Set(ZDOVars.s_startTime, now.Ticks);
                return;
            }

            if (ageMinutes > 0f && IsRunning(zdo, kind))
            {
                startTicks -= (long)(ageMinutes * 60f * TimeSpan.TicksPerSecond);
                zdo.Set(ZDOVars.s_startTime, startTicks);
            }

            if (!IsRunning(zdo, kind) && TryResumeOffline(zdo, smelter, kind, now))
            {
                startTicks = now.Ticks;
            }

            if (!IsRunning(zdo, kind))
            {
                return;
            }

            double elapsed = (now - new DateTime(startTicks)).TotalSeconds;
            if (elapsed < 1.0)
            {
                return;
            }

            if (elapsed > MaxElapsed)
            {
                elapsed = MaxElapsed;
            }

            if (smelter.m_windmill != null)
            {
                elapsed *= WindmillPower;
            }

            float seconds = smelter.m_secPerProduct;
            if (seconds <= 0f)
            {
                zdo.Set(ZDOVars.s_startTime, now.Ticks);
                return;
            }

            Inventory inventory = Load(zdo, 3, 2);
            if (inventory == null)
            {
                return;
            }

            bool needsFuel = kind.HasFuel;
            bool noInputs = kind.Inputs.Length == 0;
            int fuelPer = Mathf.Max(1, smelter.m_fuelPerProduct);
            float progress = zdo.GetFloat(ZDOVars.s_bakeTimer, 0f);
            string queued = zdo.GetInt(ZDOVars.s_queued, 0) > 0 ? zdo.GetString(ZDOVars.s_item0, string.Empty) : string.Empty;

            double time = elapsed;
            int cycles = 0;
            bool outOfFuel = false;
            while (time > 0.0 && cycles < 5000)
            {
                string ore = null;
                Smelter.ItemConversion conversion = null;
                if (noInputs)
                {
                    conversion = smelter.m_conversion.Count > 0 ? smelter.m_conversion[0] : null;
                }
                else
                {
                    ore = FindOre(inventory, smelter, kind, queued);
                    if (ore == null)
                    {
                        progress = 0f;
                        break;
                    }

                    conversion = smelter.GetItemConversion(ore);
                }

                if (conversion == null || conversion.m_to == null)
                {
                    break;
                }

                if (needsFuel && FuelAmount(inventory, smelter, kind) < fuelPer)
                {
                    outOfFuel = true;
                    break;
                }

                double need = seconds - progress;
                if (time < need)
                {
                    progress += (float)time;
                    time = 0.0;
                    break;
                }

                if (MachineStorage.AddOutput(inventory, conversion.m_to, 1) > 0)
                {
                    break;
                }

                time -= need;
                progress = 0f;
                if (ore != null)
                {
                    TakeOre(inventory, kind, ore);
                }

                if (needsFuel)
                {
                    TakeFuel(inventory, smelter, kind, fuelPer);
                }

                queued = string.Empty;
                cycles++;
            }

            if (outOfFuel && kind.Toggle)
            {
                zdo.Set(MachineStorage.OnKey, 0);
                zdo.Set(MachineStorage.ResumeKey, 1);
                zdo.Set(ZDOVars.s_fuel, 0f);
            }

            if (cycles > 0)
            {
                zdo.Set(ZDOVars.s_queued, 0);
                zdo.Set(ZDOVars.s_item0, string.Empty);
                Save(zdo, inventory);
            }

            zdo.Set(ZDOVars.s_bakeTimer, progress);
            zdo.Set(ZDOVars.s_accTime, 0f);
            zdo.Set(ZDOVars.s_startTime, now.Ticks);
            caughtUp++;

            if (ConveyorServer.LogMoves && (cycles > 0 || outOfFuel))
            {
                Plugin.Log.LogInfo(DateTime.Now.ToString("HH:mm:ss") + " Machine " + kind.Title + " " + zdo.m_uid + " caught up " + ((int)elapsed) + " s: " + cycles + " cycles" + (outOfFuel ? ", out of fuel, switched off" : string.Empty));
            }
        }

        private static bool TryResumeOffline(ZDO zdo, Smelter smelter, MachineStorage.MachineKind kind, DateTime now)
        {
            if (!MachineStorage.AutoResume || !kind.Toggle || !kind.HasFuel || zdo.GetInt(MachineStorage.ResumeKey, 0) == 0)
            {
                return false;
            }

            Inventory inventory = Load(zdo, 3, 2);
            if (inventory == null || FuelAmount(inventory, smelter, kind) < Mathf.Max(1, smelter.m_fuelPerProduct))
            {
                return false;
            }

            zdo.Set(MachineStorage.OnKey, 1);
            zdo.Set(MachineStorage.ResumeKey, 0);
            zdo.Set(ZDOVars.s_startTime, now.Ticks);
            if (ConveyorServer.LogMoves)
            {
                Plugin.Log.LogInfo(DateTime.Now.ToString("HH:mm:ss") + " Machine " + kind.Title + " " + zdo.m_uid + " restarted: fuel is back");
            }

            return true;
        }

        private static bool IsRunning(ZDO zdo, MachineStorage.MachineKind kind)
        {
            return !kind.Toggle || zdo.GetInt(MachineStorage.OnKey, 0) != 0;
        }

        private static string FindOre(Inventory inventory, Smelter smelter, MachineStorage.MachineKind kind, string preferred)
        {
            string first = null;
            foreach (Vector2i slot in kind.Inputs)
            {
                ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                if (item == null || item.m_dropPrefab == null || !smelter.IsItemAllowed(item.m_dropPrefab.name))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(preferred) && item.m_dropPrefab.name == preferred)
                {
                    return preferred;
                }

                if (first == null)
                {
                    first = item.m_dropPrefab.name;
                }
            }

            return first;
        }

        private static void TakeOre(Inventory inventory, MachineStorage.MachineKind kind, string ore)
        {
            foreach (Vector2i slot in kind.Inputs)
            {
                ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                if (item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == ore)
                {
                    inventory.RemoveItem(item, 1);
                    return;
                }
            }
        }

        private static int FuelAmount(Inventory inventory, Smelter smelter, MachineStorage.MachineKind kind)
        {
            ItemDrop.ItemData fuel = inventory.GetItemAt(kind.FuelSlot.x, kind.FuelSlot.y);
            return fuel != null && smelter.m_fuelItem != null && fuel.m_shared.m_name == smelter.m_fuelItem.m_itemData.m_shared.m_name ? fuel.m_stack : 0;
        }

        private static void TakeFuel(Inventory inventory, Smelter smelter, MachineStorage.MachineKind kind, int amount)
        {
            ItemDrop.ItemData fuel = inventory.GetItemAt(kind.FuelSlot.x, kind.FuelSlot.y);
            if (fuel != null)
            {
                inventory.RemoveItem(fuel, Mathf.Min(fuel.m_stack, amount));
            }
        }

        private static void CatchUpHive(ZDO zdo, Beehive hive, float ageMinutes)
        {
            DateTime now = ZNet.instance.GetTime();
            long lastTicks = zdo.GetLong(ZDOVars.s_lastTime, 0L);
            if (lastTicks == 0L)
            {
                zdo.Set(ZDOVars.s_lastTime, now.Ticks);
                return;
            }

            if (ageMinutes > 0f)
            {
                lastTicks -= (long)(ageMinutes * 60f * TimeSpan.TicksPerSecond);
                zdo.Set(ZDOVars.s_lastTime, lastTicks);
            }

            double elapsed = (now - new DateTime(lastTicks)).TotalSeconds;
            if (elapsed < 1.0)
            {
                return;
            }

            if (elapsed > MaxElapsed)
            {
                elapsed = MaxElapsed;
            }

            zdo.Set(ZDOVars.s_lastTime, now.Ticks);
            caughtUp++;

            bool biomeOk = ((int)WorldGenerator.instance.GetBiome(zdo.GetPosition()) & (int)hive.m_biome) != 0;
            if (!biomeOk || hive.m_secPerUnit <= 0f || hive.m_honeyItem == null)
            {
                return;
            }

            double total = zdo.GetFloat(ZDOVars.s_product, 0f) + elapsed;
            int units = (int)(total / hive.m_secPerUnit);
            float rest = (float)(total - units * (double)hive.m_secPerUnit);
            zdo.Set(ZDOVars.s_product, rest);
            if (units <= 0)
            {
                return;
            }

            Inventory inventory = Load(zdo, 3, 2);
            if (inventory == null)
            {
                return;
            }

            int left = MachineStorage.AddOutput(inventory, hive.m_honeyItem, units);
            int stored = units - left;
            if (stored > 0)
            {
                Save(zdo, inventory);
            }

            int level = zdo.GetInt(ZDOVars.s_level, 0);
            int room = Mathf.Max(0, hive.m_maxHoney - level);
            int toLevel = Mathf.Min(left, room);
            if (toLevel > 0)
            {
                zdo.Set(ZDOVars.s_level, level + toLevel);
            }

            if (ConveyorServer.LogMoves)
            {
                Plugin.Log.LogInfo(DateTime.Now.ToString("HH:mm:ss") + " Machine " + zdo.m_uid + " (" + hive.m_honeyItem.gameObject.name + ") caught up " + ((int)elapsed) + " s: " + units + " made, " + stored + " into slots, " + toLevel + " kept in the game counter, " + (left - toLevel) + " lost (full)");
            }
        }
    }
}
