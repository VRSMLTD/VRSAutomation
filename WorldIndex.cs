using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VRSAutomation
{
    internal static class WorldIndex
    {
        private const float SafetySeconds = 30f;

        internal static readonly List<ZDO> Pipes = new List<ZDO>();
        internal static readonly List<ZDO> Conveyors = new List<ZDO>();
        internal static readonly List<ZDO> Adapters = new List<ZDO>();
        internal static readonly List<ZDO> Machines = new List<ZDO>();
        internal static readonly List<ZDO> Hives = new List<ZDO>();

        internal static int Version;

        private static readonly Dictionary<int, List<ZDO>> targets = new Dictionary<int, List<ZDO>>();
        private static readonly Dictionary<string, List<ZDO>> extra = new Dictionary<string, List<ZDO>>();
        private static readonly HashSet<int> creationWatch = new HashSet<int>();
        private static readonly HashSet<int> destructionWatch = new HashSet<int>();

        private static bool dirty = true;
        private static float lastScan = -1000f;

        static WorldIndex()
        {
            Add(PipePrefab.Name, Pipes, true);
            Add(Conveyor.PieceName, Conveyors, true);
            Add(Conveyor.AdapterDualName, Adapters, true);
            Add(Conveyor.AdapterInName, Adapters, true);
            Add(Conveyor.AdapterOutName, Adapters, true);
            Add(Conveyor.SplitterName, null, true);
            Add(Conveyor.CombinerName, null, true);
            foreach (string name in MachineServer.SmelterPrefabs)
            {
                Add(name, Machines, true);
            }

            foreach (string name in MachineServer.HivePrefabs)
            {
                Add(name, Hives, true);
            }

            foreach (string name in SocketPreview.SupportedNames)
            {
                destructionWatch.Add(name.GetStableHashCode());
            }
        }

        private static void Add(string name, List<ZDO> list, bool watchCreation)
        {
            int hash = name.GetStableHashCode();
            if (list != null)
            {
                targets[hash] = list;
            }

            if (watchCreation)
            {
                creationWatch.Add(hash);
            }

            destructionWatch.Add(hash);
        }

        internal static void WantPrefab(string name)
        {
            if (string.IsNullOrEmpty(name) || extra.ContainsKey(name))
            {
                return;
            }

            List<ZDO> list = new List<ZDO>();
            extra[name] = list;
            targets[name.GetStableHashCode()] = list;
            dirty = true;
        }

        internal static ZDO Near(string name, Vector3 position, float radius)
        {
            List<ZDO> list;
            if (!extra.TryGetValue(name, out list))
            {
                WantPrefab(name);
                return null;
            }

            ZDO best = null;
            float bestDistance = radius;
            foreach (ZDO candidate in list)
            {
                float distance = Vector3.Distance(candidate.GetPosition(), position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        internal static void MarkDirty()
        {
            dirty = true;
            Version++;
        }

        internal static bool WatchesCreation(int prefab)
        {
            return creationWatch.Contains(prefab);
        }

        internal static bool WatchesDestruction(int prefab)
        {
            return destructionWatch.Contains(prefab);
        }

        internal static void Refresh()
        {
            ZDOMan man = ZDOMan.instance;
            if (man == null || (!dirty && Time.time - lastScan < SafetySeconds))
            {
                return;
            }

            bool expected = dirty;
            int before = Pipes.Count + Conveyors.Count + Adapters.Count + Machines.Count + Hives.Count;
            long start = Perf.Begin();
            foreach (List<ZDO> list in targets.Values)
            {
                list.Clear();
            }

            List<ZDO>[] sectors = man.m_objectsBySector;
            if (sectors != null)
            {
                foreach (List<ZDO> sector in sectors)
                {
                    if (sector != null)
                    {
                        Walk(sector);
                    }
                }
            }

            if (man.m_portalObjects != null)
            {
                foreach (List<ZDO> sector in man.m_portalObjects.Values)
                {
                    Walk(sector);
                }
            }

            dirty = false;
            lastScan = Time.time;
            int after = Pipes.Count + Conveyors.Count + Adapters.Count + Machines.Count + Hives.Count;
            if (!expected && after != before)
            {
                Version++;
                Plugin.Log.LogInfo("World index: a safety rescan found " + (after - before) + " object(s) the change hooks missed");
            }

            Perf.End("world index: full rescan", start);
        }

        private static void Walk(List<ZDO> sector)
        {
            for (int i = 0; i < sector.Count; i++)
            {
                ZDO zdo = sector[i];
                List<ZDO> list;
                if (targets.TryGetValue(zdo.GetPrefab(), out list))
                {
                    list.Add(zdo);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
    internal static class WorldIndexDeserializePatch
    {
        private static void Prefix(ZDO __instance, out bool __state)
        {
            __state = __instance.GetPrefab() == 0;
        }

        private static void Postfix(ZDO __instance, bool __state)
        {
            if (__state && WorldIndex.WatchesCreation(__instance.GetPrefab()))
            {
                WorldIndex.MarkDirty();
            }
        }
    }

    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.CreateNewZDO), new[] { typeof(Vector3), typeof(int) })]
    internal static class WorldIndexCreatePatch
    {
        private static void Postfix(int prefabHash)
        {
            if (WorldIndex.WatchesCreation(prefabHash))
            {
                WorldIndex.MarkDirty();
            }
        }
    }

    [HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO")]
    internal static class WorldIndexDestroyPatch
    {
        private static void Prefix(ZDOID uid)
        {
            ZDOMan man = ZDOMan.instance;
            ZDO zdo = man != null ? man.GetZDO(uid) : null;
            if (zdo != null && WorldIndex.WatchesDestruction(zdo.GetPrefab()))
            {
                WorldIndex.MarkDirty();
            }
        }
    }
}
