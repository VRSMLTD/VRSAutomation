using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VRSAutomation
{
    internal static class PipePrefab
    {
        internal const string Name = "VRS_PipeLine";

        private static GameObject container;
        private static GameObject prefab;

        internal static GameObject Get()
        {
            if (prefab != null)
            {
                return prefab;
            }

            container = new GameObject("VRS_PrefabContainer");
            container.SetActive(false);
            Object.DontDestroyOnLoad(container);

            prefab = new GameObject(Name);
            prefab.transform.SetParent(container.transform, false);
            ZNetView view = prefab.AddComponent<ZNetView>();
            view.m_persistent = true;
            view.m_distant = false;
            prefab.AddComponent<PipeLine>();
            return prefab;
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class PipeRegisterPatch
    {
        internal static void Postfix(ZNetScene __instance)
        {
            IconRenderer.Run(__instance);
            ToolModel.Refresh();
            GameObject pipePrefab = PipePrefab.Get();
            if (!__instance.m_prefabs.Contains(pipePrefab))
            {
                __instance.m_prefabs.Add(pipePrefab);
            }

            Dictionary<int, GameObject> named = Traverse.Create(__instance).Field("m_namedPrefabs").GetValue<Dictionary<int, GameObject>>();
            if (named != null)
            {
                named[PipePrefab.Name.GetStableHashCode()] = pipePrefab;
                Plugin.Log.LogInfo("Pipe object registered");
            }
            else
            {
                Plugin.Log.LogWarning("Could not register the pipe object, named prefab table not found");
            }
        }
    }

    internal static class PipeRef
    {
        internal static bool Resolve(Pipe pipe)
        {
            bool changed = false;
            ZDOID found;
            if (Stale(pipe.OutId, pipe.OutPos, pipe.OutPrefab) && Find(pipe.OutPos, pipe.OutPrefab, out found))
            {
                pipe.OutId = found;
                changed = true;
            }

            if (Stale(pipe.InId, pipe.InPos, pipe.InPrefab) && Find(pipe.InPos, pipe.InPrefab, out found))
            {
                pipe.InId = found;
                changed = true;
            }

            return changed;
        }

        internal static bool Stale(ZDOID id, Vector3 position, int prefab)
        {
            if (id.Equals(default(ZDOID)) || ZDOMan.instance == null)
            {
                return false;
            }

            ZDO zdo = ZDOMan.instance.GetZDO(id);
            if (zdo == null)
            {
                return true;
            }

            return prefab != 0 && (zdo.GetPrefab() != prefab || (zdo.GetPosition() - position).sqrMagnitude >= 0.0004f);
        }

        private static bool Find(Vector3 position, int prefab, out ZDOID result)
        {
            result = default(ZDOID);
            if (prefab == 0 || ZDOMan.instance == null)
            {
                return false;
            }

            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                if (zdo.GetPrefab() == prefab && (zdo.GetPosition() - position).sqrMagnitude < 0.0004f)
                {
                    result = zdo.m_uid;
                    return true;
                }
            }

            return false;
        }
    }

    internal static class PipeServer
    {
        internal static float StartupDelay = 4f;
        internal static float MissingGrace = 0.9f;
        internal static bool Resolved;

        private static readonly List<ZDO> buffer = new List<ZDO>();
        private static readonly Dictionary<ZDOID, float> missingSince = new Dictionary<ZDOID, float>();
        private static float readySince = -1f;
        private static int checkedVersion = -1;
        private static float lastCheck;

        private static bool Gone(Pipe pipe, bool outEnd)
        {
            return outEnd ? PipeRef.Stale(pipe.OutId, pipe.OutPos, pipe.OutPrefab) : PipeRef.Stale(pipe.InId, pipe.InPos, pipe.InPrefab);
        }


        private static readonly HashSet<ZDOID> removedAdapters = new HashSet<ZDOID>();

        private static void CheckAdapters()
        {
            removedAdapters.Clear();
            foreach (ZDO adapter in WorldIndex.Adapters)
            {
                try
                {
                    ZDOID recorded = adapter.GetZDOID(AdapterLink.HostKey);
                    if (recorded.Equals(default(ZDOID)))
                    {
                        continue;
                    }

                    ZDO host = AdapterLink.Host(adapter);
                    if (host != null)
                    {
                        missingSince.Remove(adapter.m_uid);
                        if (!host.m_uid.Equals(recorded))
                        {
                            adapter.SetOwner(ZDOMan.GetSessionID());
                            AdapterLink.Link(adapter, host);
                            Plugin.Log.LogInfo("Adapter " + adapter.m_uid + " re-linked to " + host.m_uid + " after load");
                        }

                        continue;
                    }

                    removedAdapters.Add(adapter.m_uid);
                    adapter.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(adapter);
                    Plugin.Log.LogInfo("Adapter " + adapter.m_uid + " removed: its box or machine is gone");
                }
                catch (System.Exception e)
                {
                    ConveyorServer.Report("Adapter " + adapter.m_uid, e);
                }
            }
        }

        private static void CheckPipe(ZDO zdo)
        {
            byte[] bytes = zdo.GetByteArray("vrs_pipe", null);
            Pipe pipe = bytes != null && bytes.Length > 0 ? PipeLine.Read(bytes) : null;
            if (pipe != null && PipeRef.Resolve(pipe))
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
                zdo.Set("vrs_pipe", PipeLine.Serialize(pipe));
                WorldIndex.MarkDirty();
                Plugin.Log.LogInfo("Pipe " + zdo.m_uid + " ends re-linked after load");
            }

            if (pipe != null && (removedAdapters.Contains(pipe.OutId) || removedAdapters.Contains(pipe.InId)))
            {
                missingSince.Remove(zdo.m_uid);
                zdo.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(zdo);
                Plugin.Log.LogInfo("Pipe " + zdo.m_uid + " removed with its adapter");
                return;
            }

            if (pipe == null || !(Gone(pipe, true) || Gone(pipe, false)))
            {
                missingSince.Remove(zdo.m_uid);
                return;
            }

            float since;
            if (!missingSince.TryGetValue(zdo.m_uid, out since))
            {
                missingSince[zdo.m_uid] = Time.time;
                Plugin.Log.LogInfo("Pipe " + zdo.m_uid + " flagged: out end " + pipe.OutId + (Gone(pipe, true) ? " missing" : " present") + ", in end " + pipe.InId + (Gone(pipe, false) ? " missing" : " present"));
            }
            else if (Time.time - since >= MissingGrace)
            {
                missingSince.Remove(zdo.m_uid);
                zdo.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(zdo);
                Plugin.Log.LogInfo("Pipe " + zdo.m_uid + " removed: out end " + pipe.OutId + (Gone(pipe, true) ? " missing" : " present") + ", in end " + pipe.InId + (Gone(pipe, false) ? " missing" : " present"));
            }
        }

        internal static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZoneSystem.instance == null || ZNetScene.instance == null)
            {
                readySince = -1f;
                Resolved = false;
                return;
            }

            if (!ZNet.instance.IsDedicated() && Player.m_localPlayer == null)
            {
                readySince = -1f;
                Resolved = false;
                return;
            }

            if (readySince < 0f)
            {
                readySince = Time.time;
            }

            if (Time.time - readySince < StartupDelay)
            {
                return;
            }

            WorldIndex.Refresh();
            if (Resolved && missingSince.Count == 0 && WorldIndex.Version == checkedVersion && Time.time - lastCheck < 30f)
            {
                return;
            }

            long whole = Perf.Begin();
            checkedVersion = WorldIndex.Version;
            lastCheck = Time.time;
            CheckAdapters();
            buffer.Clear();
            buffer.AddRange(WorldIndex.Pipes);
            foreach (ZDO zdo in buffer)
            {
                try
                {
                    CheckPipe(zdo);
                }
                catch (System.Exception e)
                {
                    ConveyorServer.Report("Pipe " + zdo.m_uid, e);
                }
            }

            Perf.End("pipe server: check pass", whole);
            Resolved = true;
        }
    }

    internal class PipeLine : MonoBehaviour
    {
        private const string Key = "vrs_pipe";
        private const int Version = 4;
        private const int MaxPoints = 1024;
        private const int MaxItems = 4096;

        private ZNetView view;
        private Pipe data;
        private Mesh mesh;
        private byte[] lastBytes;

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return;
            }

            byte[] bytes = view.GetZDO().GetByteArray(Key, null);
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            Pipe pipe = Read(bytes);
            if (pipe == null)
            {
                Plugin.Log.LogWarning("A saved pipe could not be read");
                return;
            }

            bool relinked = PipeRef.Resolve(pipe);
            lastBytes = bytes;
            Build(pipe);
            if (relinked && view.GetZDO().IsOwner())
            {
                Save(pipe);
            }

            InvokeRepeating(nameof(Refresh), 3f, 3f);
        }

        private void OnDestroy()
        {
            if (data != null)
            {
                PipeTool.Pipes.Remove(data);
            }

            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        internal static PipeLine Create(Pipe pipe)
        {
            GameObject go = Instantiate(PipePrefab.Get(), pipe.Points[0], Quaternion.identity);
            PipeLine line = go.GetComponent<PipeLine>();
            if (line.view == null || !line.view.IsValid())
            {
                Plugin.Log.LogWarning("Pipe object has no network id, it will not be saved");
            }
            else
            {
                line.Save(pipe);
            }

            line.Build(pipe);
            return line;
        }

        internal static void Delete(Pipe pipe)
        {
            if (pipe.Object == null)
            {
                return;
            }

            ZNetView v = pipe.Object.GetComponent<ZNetView>();
            if (v != null && v.IsValid() && ZNetScene.instance != null)
            {
                if (!v.IsOwner())
                {
                    v.ClaimOwnership();
                }

                ZNetScene.instance.Destroy(pipe.Object);
            }
            else
            {
                Destroy(pipe.Object);
            }
        }

        private void Build(Pipe pipe)
        {
            data = pipe;
            pipe.Object = gameObject;

            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                return;
            }

            Vector3 origin = pipe.Points[0];
            List<Vector3> local = new List<Vector3>(pipe.Points.Count);
            foreach (Vector3 point in pipe.Points)
            {
                local.Add(point - origin);
            }

            mesh = PipeTool.BuildMesh(local, pipe.OutNormal, pipe.OutFlange, pipe.InNormal, pipe.InFlange, pipe.InFlange);
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            pipe.Renderer = gameObject.AddComponent<MeshRenderer>();
            pipe.Renderer.sharedMaterial = PipeColors.Material(pipe.ColorIndex);

            pipe.OutPiece = PieceOf(pipe.OutId);
            pipe.InPiece = PieceOf(pipe.InId);
            PipeTool.Pipes.Add(pipe);
        }

        private static Piece PieceOf(ZDOID id)
        {
            if (ZNetScene.instance == null || id.Equals(default(ZDOID)))
            {
                return null;
            }

            GameObject go = ZNetScene.instance.FindInstance(id);
            return go != null ? go.GetComponent<Piece>() : null;
        }

        internal static byte[] Serialize(Pipe pipe)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(Version);
            pkg.Write(pipe.Points.Count);
            foreach (Vector3 point in pipe.Points)
            {
                pkg.Write(point);
            }

            pkg.Write(pipe.OutNormal);
            pkg.Write(pipe.InNormal);
            pkg.Write(pipe.OutId);
            pkg.Write(pipe.InId);
            pkg.Write(pipe.OutIndex);
            pkg.Write(pipe.InIndex);
            WriteItems(pkg, pipe.OutItems);
            WriteItems(pkg, pipe.InItems);
            pkg.Write(pipe.OutFlange);
            pkg.Write(pipe.InFlange);
            pkg.Write(pipe.OutPos);
            pkg.Write(pipe.InPos);
            pkg.Write(pipe.OutPrefab);
            pkg.Write(pipe.InPrefab);
            pkg.Write(pipe.ColorIndex);
            return pkg.GetArray();
        }

        private void Save(Pipe pipe)
        {
            lastBytes = Serialize(pipe);
            view.GetZDO().Set(Key, lastBytes);
        }

        internal static bool Recolor(Pipe pipe, int colorIndex)
        {
            PipeLine line = pipe.Object != null ? pipe.Object.GetComponent<PipeLine>() : null;
            if (line == null || line.view == null || !line.view.IsValid())
            {
                return false;
            }

            if (!line.view.IsOwner())
            {
                line.view.ClaimOwnership();
            }

            pipe.ColorIndex = colorIndex;
            if (pipe.Renderer != null)
            {
                pipe.Renderer.sharedMaterial = PipeColors.Material(colorIndex);
            }

            line.Save(pipe);
            return true;
        }

        private void Refresh()
        {
            if (view == null || !view.IsValid() || data == null)
            {
                return;
            }

            byte[] bytes = view.GetZDO().GetByteArray(Key, null);
            if (bytes == null || bytes.Length == 0 || (lastBytes != null && System.Linq.Enumerable.SequenceEqual(bytes, lastBytes)))
            {
                return;
            }

            Pipe fresh = Read(bytes);
            if (fresh == null)
            {
                return;
            }

            lastBytes = bytes;
            data.OutId = fresh.OutId;
            data.InId = fresh.InId;
            data.OutPos = fresh.OutPos;
            data.InPos = fresh.InPos;
            data.OutPrefab = fresh.OutPrefab;
            data.InPrefab = fresh.InPrefab;
            data.OutPiece = PieceOf(data.OutId);
            data.InPiece = PieceOf(data.InId);
            if (fresh.ColorIndex != data.ColorIndex)
            {
                data.ColorIndex = fresh.ColorIndex;
                if (data.Renderer != null)
                {
                    data.Renderer.sharedMaterial = PipeColors.Material(data.ColorIndex);
                }
            }
        }

        internal static Pipe Read(byte[] bytes)
        {
            try
            {
                ZPackage pkg = new ZPackage(bytes);
                int version = pkg.ReadInt();
                if (version < 1 || version > 4)
                {
                    return null;
                }

                Pipe pipe = new Pipe();
                int count = pkg.ReadInt();
                if (count < 2 || count > MaxPoints)
                {
                    return null;
                }

                pipe.Points = new List<Vector3>(count);
                for (int i = 0; i < count; i++)
                {
                    pipe.Points.Add(pkg.ReadVector3());
                }

                pipe.OutNormal = pkg.ReadVector3();
                pipe.InNormal = pkg.ReadVector3();
                pipe.OutId = pkg.ReadZDOID();
                pipe.InId = pkg.ReadZDOID();
                pipe.OutIndex = pkg.ReadInt();
                pipe.InIndex = pkg.ReadInt();
                pipe.OutItems = ReadItems(pkg);
                pipe.InItems = ReadItems(pkg);
                if (version >= 2)
                {
                    pipe.OutFlange = pkg.ReadBool();
                    pipe.InFlange = pkg.ReadBool();
                }
                if (version >= 3)
                {
                    pipe.OutPos = pkg.ReadVector3();
                    pipe.InPos = pkg.ReadVector3();
                    pipe.OutPrefab = pkg.ReadInt();
                    pipe.InPrefab = pkg.ReadInt();
                }
                if (version >= 4)
                {
                    pipe.ColorIndex = pkg.ReadInt();
                }


                return pipe.Points.Count >= 2 ? pipe : null;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Pipe data was corrupt: " + e.Message);
                return null;
            }
        }

        private static void WriteItems(ZPackage pkg, HashSet<string> items)
        {
            if (items == null)
            {
                pkg.Write(-1);
                return;
            }

            pkg.Write(items.Count);
            foreach (string item in items)
            {
                pkg.Write(item);
            }
        }

        private static HashSet<string> ReadItems(ZPackage pkg)
        {
            int count = pkg.ReadInt();
            if (count < 0)
            {
                return null;
            }

            if (count > MaxItems)
            {
                throw new System.IO.InvalidDataException("too many items in a pipe");
            }

            HashSet<string> items = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                items.Add(pkg.ReadString());
            }

            return items;
        }
    }
}