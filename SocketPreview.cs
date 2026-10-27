using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VRSAutomation
{
    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class PlayerAwakePatch
    {
        internal static void Postfix(Player __instance)
        {
            if (__instance.GetComponent<SocketPreview>() == null)
            {
                __instance.gameObject.AddComponent<SocketPreview>();
            }
        }
    }

    internal class SocketRef
    {
        public Piece Owner;
        public int Index;
        public Vector3 Center;
        public Vector3 Normal;
        public bool IsOutput;
        public HashSet<string> Items;
    }

    internal class SocketPreview : MonoBehaviour
    {
        private const string ToolPrefabName = "VRS_LinkTool";
        private const float AimRange = 50f;
        private const int Segments = 24;
        private const float PairDistance = 0.6f;
        private const float PairHalfOffset = 0.14f;
        private const float MachineRingRadius = 0.11f;
        private const float SnapRange = 1f;
        private const float OutputInset = 0.38f;
        private const float RingWidth = 0.02f;
        private const float HighlightWidth = 0.05f;
        private const float HighlightRadius = 0.3f;

        internal static readonly HashSet<string> SupportedNames = new HashSet<string>
        {
            "piece_chest_wood",
            "piece_chest",
            "piece_chest_blackmetal",
            "smelter",
            "blastfurnace",
            "charcoal_kiln",
            "windmill",
            "piece_spinningwheel",
            "eitrrefinery",
            "piece_FrostKiln",
            "piece_beehive",
            "piece_birdnest",
            "piece_sapcollector",
            Conveyor.PieceName,
            Conveyor.SplitterName,
            Conveyor.CombinerName,
            Conveyor.AdapterDualName,
            Conveyor.AdapterInName,
            Conveyor.AdapterOutName
        };

        internal static bool IsKiln(string name)
        {
            string clean = CleanName(name);
            return clean == "charcoal_kiln" || clean == "piece_FrostKiln";
        }

        private static readonly Dictionary<int, Vector3[]> kilnSpots = new Dictionary<int, Vector3[]>();
        private const float KilnShift = -0.075f;

        internal static bool TryKilnSpot(Piece piece, out Vector3 position, out Vector3 normal)
        {
            position = Vector3.zero;
            normal = Vector3.up;
            Vector3[] cached;
            if (kilnSpots.TryGetValue(piece.GetInstanceID(), out cached))
            {
                position = cached[0];
                normal = cached[1];
                return true;
            }

            Smelter smelter = piece.GetComponent<Smelter>();
            Material material = GetMaterial();
            if (smelter == null || material == null)
            {
                return false;
            }

            int before = Sockets.Count;
            Piece savedOwner = buildOwner;
            int savedIndex = buildIndex;
            HashSet<string> savedItems = buildItems;
            buildOwner = piece;
            buildIndex = 0;
            buildItems = null;
            GameObject holder = BuildMachineRings(piece, smelter, material, LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid"));
            buildOwner = savedOwner;
            buildIndex = savedIndex;
            buildItems = savedItems;

            bool found = Sockets.Count - before >= 2;
            if (found)
            {
                position = (Sockets[before].Center + Sockets[before + 1].Center) * 0.5f;
                normal = Sockets[before].Normal;
                Vector3 across = Vector3.Cross(Vector3.up, -normal);
                if (across.sqrMagnitude > 0.01f && CleanName(piece.gameObject.name) == "charcoal_kiln")
                {
                    position += across.normalized * KilnShift;
                }

                kilnSpots[piece.GetInstanceID()] = new[] { position, normal };
            }

            Sockets.RemoveRange(before, Sockets.Count - before);
            Object.Destroy(holder);
            return found;
        }

        internal class Spot
        {
            public Vector3 Center;
            public Vector3 Normal;
            public bool IsOutput;
            public HashSet<string> Items;
        }

        private static readonly Dictionary<int, List<Spot>> machineSpots = new Dictionary<int, List<Spot>>();
        private static bool collecting;

        internal static List<Spot> MachineSpots(Piece piece)
        {
            List<Spot> cached;
            if (machineSpots.TryGetValue(piece.GetInstanceID(), out cached))
            {
                return cached;
            }

            List<Spot> spots = new List<Spot>();
            int before = Sockets.Count;
            Piece savedOwner = buildOwner;
            int savedIndex = buildIndex;
            HashSet<string> savedItems = buildItems;
            collecting = true;
            GameObject holder = BuildRings(piece, LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid"));
            collecting = false;
            buildOwner = savedOwner;
            buildIndex = savedIndex;
            buildItems = savedItems;

            for (int i = before; i < Sockets.Count; i++)
            {
                spots.Add(new Spot { Center = Sockets[i].Center, Normal = Sockets[i].Normal, IsOutput = Sockets[i].BaseColor == OutputColor, Items = Sockets[i].Items });
            }

            Sockets.RemoveRange(before, Sockets.Count - before);
            if (holder != null)
            {
                Object.Destroy(holder);
            }

            if (spots.Count > 0)
            {
                machineSpots[piece.GetInstanceID()] = spots;
            }

            return spots;
        }

        internal static bool IsHost(string name)
        {
            string clean = CleanName(name);
            return SupportedNames.Contains(clean) && clean != Conveyor.PieceName && !Conveyor.IsJunction(clean) && !Conveyor.IsAdapter(clean);
        }

        private static readonly Color InputColor = new Color(0.2f, 0.9f, 0.3f);
        private static readonly Color OutputColor = new Color(1f, 0.55f, 0.1f);

        private static readonly Vector3[] SnapDirections = BuildSnapDirections();

        private static readonly Dictionary<string, Vector2[]> InputNudges = new Dictionary<string, Vector2[]>
        {
            { "blastfurnace", new Vector2[] { new Vector2(-0.04f, 0.03f), new Vector2(0.06f, 0.03f) } },
            { "piece_spinningwheel", new Vector2[] { new Vector2(0f, 0.125f) } }
        };

        private static readonly Dictionary<string, Vector2> OutputNudges = new Dictionary<string, Vector2>
        {
            { "blastfurnace", new Vector2(0.05f, 0.2f) },
            { "piece_spinningwheel", Vector2.zero },
            { "eitrrefinery", Vector2.zero }
        };

        private static readonly Dictionary<string, float[]> InputPush = new Dictionary<string, float[]>
        {
            { "eitrrefinery", new float[] { 0f, 0.03f } }
        };

        private static readonly Dictionary<string, int[]> FlatInputs = new Dictionary<string, int[]>
        {
            { "piece_spinningwheel", new int[] { 0 } },
            { "eitrrefinery", new int[] { 0 } }
        };

        private static readonly Dictionary<string, float> ExactOutputs = new Dictionary<string, float>
        {
            { "eitrrefinery", 0.025f }
        };

        private static readonly HashSet<string> PairedOutputs = new HashSet<string>
        {
            "piece_FrostKiln"
        };

        private static readonly Dictionary<string, Vector3[]> LocalOutputs = new Dictionary<string, Vector3[]>
        {
            { "windmill", new Vector3[] { new Vector3(-0.033f, 1.03f, 2f), Vector3.forward } }
        };

        private static readonly List<Socket> Sockets = new List<Socket>();

        internal static SocketRef Aimed;
        internal static System.Func<SocketRef, bool> CanHighlight;
        internal static bool PipeActive;
        private static Piece buildOwner;
        private static int buildIndex;
        private static HashSet<string> buildItems;

        private static Material ringMaterial;

        private Player player;
        private Piece current;
        private GameObject ringRoot;
        private int layerMask;
        private int highlighted = -1;

        private class Socket
        {
            public Vector3 Center;
            public Vector3 Normal;
            public LineRenderer Line;
            public Color BaseColor;
            public Piece Owner;
            public int Index;
            public HashSet<string> Items;
        }

        private void Awake()
        {
            player = GetComponent<Player>();
            layerMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Update()
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            if (!HoldingTool())
            {
                PipeActive = false;
                Clear();
                return;
            }

            PipeActive = PipeSelected();

            if (current == null && ringRoot != null)
            {
                Clear();
            }

            Piece target = AimedPiece();
            if (target != current)
            {
                Clear();
                if (target != null)
                {
                    current = target;
                    ringRoot = BuildRings(target, layerMask);
                }
            }

            UpdateHighlight();
        }

        private static GUIStyle labelStyle;

        private void OnGUI()
        {
            SpotLabels.Draw();
            SocketRef socket = Aimed;
            if (!PipeActive || socket == null || socket.Owner == null || !Conveyor.IsAdapter(socket.Owner.gameObject.name) || Camera.main == null || SpotLabels.MenuOpen())
            {
                return;
            }

            Vector3 screen = Camera.main.WorldToScreenPoint(socket.Center + Vector3.up * 0.18f);
            if (screen.z <= 0f)
            {
                return;
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 24;
                labelStyle.fontStyle = FontStyle.Bold;
            }

            string title = socket.IsOutput ? "Output" : "Input";
            string state = PipeTool.PipeAt(socket, null) != null ? "Connected" : "Empty";
            Color color = socket.IsOutput ? OutputColor : InputColor;
            Rect top = new Rect(screen.x - 120f, Screen.height - screen.y - 34f, 240f, 32f);
            Rect bottom = new Rect(screen.x - 120f, top.y + 28f, 240f, 28f);

            GUI.color = Color.black;
            GUI.Label(new Rect(top.x + 2f, top.y + 2f, top.width, top.height), title, labelStyle);
            GUI.color = color;
            GUI.Label(top, title, labelStyle);
            labelStyle.fontSize = 18;
            GUI.color = Color.black;
            GUI.Label(new Rect(bottom.x + 2f, bottom.y + 2f, bottom.width, bottom.height), state, labelStyle);
            GUI.color = Color.white;
            GUI.Label(bottom, state, labelStyle);
            labelStyle.fontSize = 24;
            GUI.color = Color.white;
        }

        private bool PipeSelected()
        {
            return player.m_buildPieces != null && Plugin.IsPipePiece(player.m_buildPieces.GetSelectedPiece());
        }

        private void UpdateHighlight()
        {
            int best = -1;
            if (ringRoot != null && PipeSelected() && GameCamera.instance != null)
            {
                Transform cam = GameCamera.instance.transform;
                float bestDistance = HighlightRadius;
                for (int i = 0; i < Sockets.Count; i++)
                {
                    Vector3 toSocket = Sockets[i].Center - cam.position;
                    if (Vector3.Dot(toSocket, cam.forward) <= 0f)
                    {
                        continue;
                    }

                    if (CanHighlight != null && !CanHighlight(ToRef(Sockets[i])))
                    {
                        continue;
                    }

                    float distance = Vector3.Cross(cam.forward, toSocket).magnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
            }

            if (best == highlighted)
            {
                return;
            }

            if (highlighted >= 0 && highlighted < Sockets.Count)
            {
                SetHighlight(Sockets[highlighted], false);
            }

            if (best >= 0)
            {
                SetHighlight(Sockets[best], true);
            }

            highlighted = best;
            Aimed = highlighted >= 0 && highlighted < Sockets.Count ? ToRef(Sockets[highlighted]) : null;
        }

        private static SocketRef ToRef(Socket socket)
        {
            return new SocketRef { Owner = socket.Owner, Index = socket.Index, Center = socket.Center, Normal = socket.Normal, IsOutput = socket.BaseColor == OutputColor, Items = socket.Items };
        }

        private static void SetHighlight(Socket socket, bool on)
        {
            Color color = on ? Color.Lerp(socket.BaseColor, Color.white, 0.55f) : socket.BaseColor;
            socket.Line.startColor = color;
            socket.Line.endColor = color;
            socket.Line.widthMultiplier = on ? HighlightWidth : RingWidth;
        }

        private void Clear()
        {
            if (ringRoot != null)
            {
                Destroy(ringRoot);
            }

            ringRoot = null;
            current = null;
            highlighted = -1;
            Aimed = null;
            Sockets.Clear();
        }

        private bool HoldingTool()
        {
            ItemDrop.ItemData item = player.GetRightItem();
            return item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == ToolPrefabName;
        }

        private Piece AimedPiece()
        {
            if (GameCamera.instance == null)
            {
                return null;
            }

            Transform cam = GameCamera.instance.transform;
            RaycastHit[] all = Physics.RaycastAll(cam.position, cam.forward, AimRange, layerMask, QueryTriggerInteraction.Ignore);
            if (all.Length == 0)
            {
                return null;
            }

            System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
            RaycastHit hit = all[0];
            foreach (RaycastHit candidate in all)
            {
                if (candidate.distance - all[0].distance > 0.8f)
                {
                    break;
                }

                Piece behind = candidate.collider.GetComponentInParent<Piece>();
                if (behind != null && Conveyor.IsAdapter(behind.gameObject.name))
                {
                    return behind;
                }
            }

            Piece piece = hit.collider.GetComponentInParent<Piece>();
            if (piece == null || !SupportedNames.Contains(CleanName(piece.gameObject.name)))
            {
                return null;
            }

            return piece;
        }

        private static string CleanName(string name)
        {
            int index = name.IndexOf("(Clone)");
            return index >= 0 ? name.Substring(0, index) : name;
        }

        private static Vector3[] BuildSnapDirections()
        {
            List<Vector3> list = new List<Vector3>();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x != 0 || y != 0 || z != 0)
                        {
                            list.Add(new Vector3(x, y, z).normalized);
                        }
                    }
                }
            }

            return list.ToArray();
        }

        private static Material GetMaterial()
        {
            if (ringMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                }

                if (shader == null)
                {
                    Plugin.Log.LogError("No shader found for socket rings");
                    return null;
                }

                ringMaterial = new Material(shader);
            }

            return ringMaterial;
        }

        internal static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                MeshRenderer meshRenderer = filter.GetComponent<MeshRenderer>();
                if (meshRenderer == null || !meshRenderer.enabled)
                {
                    continue;
                }

                Vector3 min = filter.sharedMesh.bounds.min;
                Vector3 max = filter.sharedMesh.bounds.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));

                    if (!found)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return found;
        }

        private static GameObject BuildRings(Piece piece, int mask)
        {
            Material material = GetMaterial();
            if (material == null)
            {
                return null;
            }

            buildOwner = piece;
            buildIndex = 0;
            buildItems = null;

            if (!collecting && IsHost(piece.gameObject.name))
            {
                return null;
            }

            if (CleanName(piece.gameObject.name) == Conveyor.PieceName)
            {
                return BuildConveyorRings(piece, material);
            }

            string cleanName = CleanName(piece.gameObject.name);
            if (Conveyor.IsAdapter(cleanName))
            {
                return BuildAdapterRings(piece, GetJunctionMaterial(), cleanName);
            }

            if (cleanName == Conveyor.SplitterName || cleanName == Conveyor.CombinerName)
            {
                return BuildJunctionRings(piece, GetJunctionMaterial(), cleanName == Conveyor.SplitterName);
            }

            Smelter smelter = piece.GetComponent<Smelter>();
            if (smelter != null)
            {
                return BuildMachineRings(piece, smelter, material, mask);
            }

            Beehive hive = piece.GetComponent<Beehive>();
            if (hive != null)
            {
                buildItems = ItemSet(hive.m_honeyItem);
                if (cleanName.StartsWith("piece_birdnest") && hive.m_spawnPoint != null)
                {
                    Vector3 local = piece.transform.InverseTransformPoint(hive.m_spawnPoint.position);
                    Bounds bounds;
                    Vector3 nestPoint = hive.m_spawnPoint.position;
                    if (TryGetLocalBounds(piece.transform, out bounds))
                    {
                        Vector3 face = bounds.center;
                        if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
                        {
                            face.x = bounds.center.x + Mathf.Sign(local.x) * bounds.extents.x;
                        }
                        else
                        {
                            face.z = bounds.center.z + Mathf.Sign(local.z) * bounds.extents.z;
                        }

                        nestPoint = piece.transform.TransformPoint(face);
                    }

                    return BuildOutputOnlyRings(piece, material, mask, nestPoint, new Vector2(0.0046f, -0.22f));
                }

                return BuildHiveRings(piece, material, mask);
            }

            SapCollector sap = piece.GetComponent<SapCollector>();
            if (sap != null && sap.m_spawnPoint != null)
            {
                buildItems = ItemSet(sap.m_spawnItem);
                return BuildOutputOnlyRings(piece, material, mask, sap.m_spawnPoint.position);
            }

            return BuildChestRings(piece, material);
        }

        private static HashSet<string> ItemSet(ItemDrop item)
        {
            HashSet<string> set = new HashSet<string>();
            if (item != null)
            {
                set.Add(item.gameObject.name);
            }

            return set;
        }

        private static GameObject BuildMachineRings(Piece piece, Smelter smelter, Material material, int mask)
        {
            GameObject holder = new GameObject("VRS_SocketPreview");
            Transform root = piece.transform;

            HashSet<string> oreItems = new HashSet<string>();
            HashSet<string> producedItems = new HashSet<string>();
            foreach (var conversion in smelter.m_conversion)
            {
                if (conversion.m_from != null)
                {
                    oreItems.Add(conversion.m_from.gameObject.name);
                }

                if (conversion.m_to != null)
                {
                    producedItems.Add(conversion.m_to.gameObject.name);
                }
            }

            HashSet<string> fuelItems = ItemSet(smelter.m_fuelItem);

            List<Vector3> points = new List<Vector3>();
            List<HashSet<string>> accepts = new List<HashSet<string>>();
            if (smelter.m_addOreSwitch != null)
            {
                points.Add(smelter.m_addOreSwitch.transform.position);
                accepts.Add(oreItems);
            }

            if (smelter.m_addWoodSwitch != null)
            {
                points.Add(smelter.m_addWoodSwitch.transform.position);
                accepts.Add(fuelItems);
            }

            int count = points.Count;
            Vector3[] normals = new Vector3[count];
            Vector3[] centers = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                normals[i] = FaceNormal(root, points[i]);
                centers[i] = points[i] + normals[i] * 0.06f;
            }

            bool[] grouped = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (grouped[i])
                {
                    continue;
                }

                List<int> group = new List<int> { i };
                for (int j = i + 1; j < count; j++)
                {
                    if (!grouped[j] && Vector3.Dot(normals[i], normals[j]) > 0.99f)
                    {
                        group.Add(j);
                    }
                }

                if (group.Count < 2)
                {
                    continue;
                }

                AlignGroup(piece, points, normals, centers, group, mask);
                foreach (int index in group)
                {
                    grouped[index] = true;
                }
            }

            Vector2[] nudges;
            if (InputNudges.TryGetValue(CleanName(piece.gameObject.name), out nudges))
            {
                for (int i = 0; i < count && i < nudges.Length; i++)
                {
                    Vector3 across = Vector3.Cross(Vector3.up, -normals[i]).normalized;
                    centers[i] += across * nudges[i].x + Vector3.up * nudges[i].y;
                }
            }

            string machineName = CleanName(piece.gameObject.name);

            float[] pushes;
            if (InputPush.TryGetValue(machineName, out pushes))
            {
                for (int i = 0; i < count && i < pushes.Length; i++)
                {
                    centers[i] -= normals[i] * pushes[i];
                }
            }

            int[] flat;
            if (FlatInputs.TryGetValue(machineName, out flat))
            {
                foreach (int flatIndex in flat)
                {
                    if (flatIndex < count)
                    {
                        normals[flatIndex] = Vector3.up;
                    }
                }
            }

            bool hasOutput = smelter.m_outputPoint != null;
            Vector3 outputPoint = hasOutput ? smelter.m_outputPoint.position : Vector3.zero;

            int pairedInput = -1;
            if (hasOutput)
            {
                float best = PairDistance;
                for (int i = 0; i < count; i++)
                {
                    float distance = Vector3.Distance(points[i], outputPoint);
                    if (distance < best)
                    {
                        best = distance;
                        pairedInput = i;
                    }
                }
            }

            if (PairedOutputs.Contains(machineName) && count > 0)
            {
                pairedInput = 0;
            }

            for (int i = 0; i < count; i++)
            {
                buildItems = accepts[i];
                if (i == pairedInput)
                {
                    Vector3 right = Vector3.Cross(Vector3.up, -normals[i]).normalized;
                    AddRingOnSurface(holder.transform, material, centers[i] - right * PairHalfOffset, normals[i], InputColor);
                    buildItems = producedItems;
                    AddRingOnSurface(holder.transform, material, centers[i] + right * PairHalfOffset, normals[i], OutputColor);
                }
                else
                {
                    AddRingOnSurface(holder.transform, material, centers[i], normals[i], InputColor);
                }
            }

            buildItems = producedItems;

            Vector3[] localOutput;
            float exactRaise;
            if (LocalOutputs.TryGetValue(machineName, out localOutput))
            {
                AddRingOnSurface(holder.transform, material, root.TransformPoint(localOutput[0]), root.TransformDirection(localOutput[1]).normalized, OutputColor);
            }
            else if (hasOutput && ExactOutputs.TryGetValue(machineName, out exactRaise))
            {
                AddRingOnSurface(holder.transform, material, outputPoint + Vector3.up * exactRaise, Vector3.up, OutputColor);
            }
            else if (hasOutput && pairedInput < 0)
            {
                Vector2 outputNudge;
                if (OutputNudges.TryGetValue(machineName, out outputNudge))
                {
                    Vector3 outputStart = smelter.m_emptyOreSwitch != null ? smelter.m_emptyOreSwitch.transform.position : outputPoint;
                    AddFaceOutputRing(holder.transform, piece, material, outputStart, FaceNormal(root, outputStart), outputNudge, mask);
                }
                else
                {
                    AddSnappedOutputRing(holder.transform, piece, material, outputPoint, mask);
                }
            }

            return holder;
        }

        private static GameObject BuildHiveRings(Piece piece, Material material, int mask)
        {
            GameObject holder = new GameObject("VRS_SocketPreview");
            Transform root = piece.transform;
            Vector3 normal = root.TransformDirection(Vector3.left).normalized;
            Vector3 start = root.TransformPoint(new Vector3(-1.2f, 0.48f, 0f));
            AddFaceOutputRing(holder.transform, piece, material, start, normal, Vector2.zero, mask);
            return holder;
        }

        private static GameObject BuildOutputOnlyRings(Piece piece, Material material, int mask, Vector3 spawn, Vector2 nudge = default(Vector2))
        {
            GameObject holder = new GameObject("VRS_SocketPreview");
            AddFaceOutputRing(holder.transform, piece, material, spawn, FaceNormal(piece.transform, spawn), nudge, mask);
            return holder;
        }

        private static Vector3 FaceNormal(Transform root, Vector3 worldPoint)
        {
            Vector3 local = root.InverseTransformPoint(worldPoint);
            Vector3 direction = Mathf.Abs(local.x) > Mathf.Abs(local.z)
                ? new Vector3(Mathf.Sign(local.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(local.z));
            return root.TransformDirection(direction).normalized;
        }

        private static bool IsSurface(RaycastHit hit, Piece piece)
        {
            return hit.collider.GetComponentInParent<Piece>() == piece && hit.collider.GetComponentInParent<Switch>() == null;
        }

        private static bool TrySurfaceDepth(Piece piece, Vector3 origin, Vector3 normal, int mask, out float depth)
        {
            depth = 0f;
            RaycastHit[] hits = Physics.RaycastAll(origin + normal * 1.5f, -normal, 3f, mask, QueryTriggerInteraction.Ignore);

            bool found = false;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance < nearest && IsSurface(hit, piece))
                {
                    nearest = hit.distance;
                    depth = Vector3.Dot(hit.point, normal);
                    found = true;
                }
            }

            return found;
        }

        private static bool TryFaceDepth(Piece piece, Vector3 point, Vector3 normal, int mask, int half, float percentile, out float face)
        {
            face = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, -normal).normalized;
            List<float> depths = new List<float>();

            for (int a = -half; a <= half; a++)
            {
                for (int b = -half; b <= half; b++)
                {
                    Vector3 origin = point + right * (a * 0.05f) + Vector3.up * (b * 0.05f);
                    float depth;
                    if (TrySurfaceDepth(piece, origin, normal, mask, out depth))
                    {
                        depths.Add(depth);
                    }
                }
            }

            if (depths.Count == 0)
            {
                return false;
            }

            depths.Sort();
            face = depths[(int)(depths.Count * percentile)];
            return true;
        }

        private static void AlignGroup(Piece piece, List<Vector3> points, Vector3[] normals, Vector3[] centers, List<int> group, int mask)
        {
            Vector3 normal = normals[group[0]];
            float plane = 0f;

            foreach (int index in group)
            {
                float face;
                if (!TryFaceDepth(piece, points[index], normal, mask, 9, 0.7f, out face))
                {
                    return;
                }

                plane += face;
            }

            plane = plane / group.Count + 0.02f;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float sumY = 0f;
            foreach (int index in group)
            {
                minY = Mathf.Min(minY, points[index].y);
                maxY = Mathf.Max(maxY, points[index].y);
                sumY += points[index].y;
            }

            bool level = maxY - minY < 0.15f;
            float averageY = sumY / group.Count;

            foreach (int index in group)
            {
                Vector3 center = points[index];
                if (level)
                {
                    center.y = averageY;
                }

                centers[index] = center + normal * (plane - Vector3.Dot(center, normal));
            }
        }

        private static Vector3 OutwardNormal(Transform root, Vector3 worldPoint)
        {
            Vector3 local = root.InverseTransformPoint(worldPoint);
            Vector3 flat = new Vector3(local.x, 0f, local.z);
            Vector3 localNormal = flat.sqrMagnitude > 0.0025f ? flat.normalized : Vector3.forward;
            return root.TransformDirection(localNormal).normalized;
        }

        private static void AddSnappedOutputRing(Transform parent, Piece piece, Material material, Vector3 point, int mask)
        {
            Vector3 inward = -OutwardNormal(piece.transform, point);

            bool found = false;
            float nearest = SnapRange;
            Vector3 hitPoint = point;
            Vector3 hitNormal = Vector3.up;

            foreach (Vector3 direction in SnapDirections)
            {
                RaycastHit[] hits = Physics.RaycastAll(point, direction, SnapRange, mask, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.distance < nearest && hit.collider.GetComponentInParent<Piece>() == piece)
                    {
                        nearest = hit.distance;
                        hitPoint = hit.point;
                        hitNormal = hit.normal;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                AddRingOnSurface(parent, material, point + inward * OutputInset, OutwardNormal(piece.transform, point), OutputColor);
                return;
            }

            AddRingOnSurface(parent, material, hitPoint + hitNormal * 0.03f + inward * OutputInset, hitNormal, OutputColor);
        }

        private static void AddFaceOutputRing(Transform parent, Piece piece, Material material, Vector3 start, Vector3 normal, Vector2 nudge, int mask)
        {
            float face;
            float plane = TryFaceDepth(piece, start, normal, mask, 6, 0.5f, out face) ? face + 0.02f : Vector3.Dot(start, normal);
            Vector3 across = Vector3.Cross(Vector3.up, -normal).normalized;
            Vector3 center = start + normal * (plane - Vector3.Dot(start, normal)) + across * nudge.x + Vector3.up * nudge.y;
            AddRingOnSurface(parent, material, center, normal, OutputColor);
        }

        private static void AddRingOnSurface(Transform parent, Material material, Vector3 center, Vector3 normal, Color color)
        {
            Vector3 u = Vector3.Cross(Vector3.up, normal);
            if (u.sqrMagnitude < 0.01f)
            {
                u = Vector3.right;
            }

            u.Normalize();
            Vector3 v = Vector3.Cross(normal, u).normalized;
            AddRing(parent, material, center, u, v, MachineRingRadius, color, normal);
        }

        private static GameObject BuildConveyorRings(Piece piece, Material material)
        {
            GameObject holder = new GameObject("VRS_SocketPreview");
            Transform root = piece.transform;
            AddConveyorRing(holder.transform, root, material, new Vector3(0f, 0.10f, -0.182f), Vector3.back, InputColor);
            AddConveyorRing(holder.transform, root, material, new Vector3(0f, 0.10f, 0.182f), Vector3.forward, OutputColor);
            AddPortArrow(holder.transform, root, new Vector3(0f, 0.10f, -0.182f), Vector3.back, true, InputColor);
            AddPortArrow(holder.transform, root, new Vector3(0f, 0.10f, 0.182f), Vector3.forward, false, OutputColor);
            return holder;
        }

        private static GameObject BuildAdapterRings(Piece piece, Material material, string name)
        {
            GameObject holder = new GameObject("VRS_SocketPreview");
            Transform root = piece.transform;
            if (name == Conveyor.AdapterDualName)
            {
                AddJunctionRing(holder.transform, root, material, new Vector3(0.16f, 0.105f, 0f), Vector3.right, InputColor);
                AddJunctionRing(holder.transform, root, material, new Vector3(-0.16f, 0.105f, 0f), Vector3.left, OutputColor);
                AddPortArrow(holder.transform, root, new Vector3(0.16f, 0.105f, 0f), Vector3.right, true, InputColor);
                AddPortArrow(holder.transform, root, new Vector3(-0.16f, 0.105f, 0f), Vector3.left, false, OutputColor);
            }
            else if (name == Conveyor.AdapterInName)
            {
                AddJunctionRing(holder.transform, root, material, new Vector3(0.135f, 0.08f, 0f), Vector3.right, InputColor);
                AddPortArrow(holder.transform, root, new Vector3(0.135f, 0.08f, 0f), Vector3.right, true, InputColor);
            }
            else
            {
                AddJunctionRing(holder.transform, root, material, new Vector3(-0.135f, 0.08f, 0f), Vector3.left, OutputColor);
                AddPortArrow(holder.transform, root, new Vector3(-0.135f, 0.08f, 0f), Vector3.left, false, OutputColor);
            }

            return holder;
        }

        private static void AddPortArrow(Transform parent, Transform root, Vector3 localMouth, Vector3 localOut, bool inward, Color color)
        {
            BuildArrow(parent, root.TransformPoint(localMouth), root.TransformDirection(localOut).normalized, root.up, inward, color, true);
        }

        internal static void AttachGhostArrows(GameObject ghost, string name)
        {
            if (ghost.transform.Find("VRS_GhostArrows") != null)
            {
                return;
            }

            string clean = CleanName(name);
            GameObject holder = new GameObject("VRS_GhostArrows");
            holder.transform.SetParent(ghost.transform, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            PortArrows.Build(holder.transform, clean);
        }

        internal static void BuildArrow(Transform parent, Vector3 mouth, Vector3 outward, Vector3 side, bool inward, Color color, bool worldSpace)
        {
            Vector3 near = mouth + outward * 0.10f;
            Vector3 far = mouth + outward * 0.32f;
            Vector3 tail = inward ? far : near;
            Vector3 tip = inward ? near : far;
            Vector3 direction = (tip - tail).normalized;

            GameObject go = new GameObject("Arrow");
            go.transform.SetParent(parent, false);
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, side.normalized)) > 0.99f ? Vector3.right : side;
            Quaternion rotation = Quaternion.LookRotation(direction, up);
            if (worldSpace)
            {
                go.transform.position = tail;
                go.transform.rotation = rotation;
            }
            else
            {
                go.transform.localPosition = tail;
                go.transform.localRotation = rotation;
            }

            Mesh mesh = BuildArrowMesh(go.transform, color);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetArrowMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.AddComponent<ArrowMeshOwner>().Mesh = mesh;
        }

        private static Material arrowMaterial;

        private static Material GetArrowMaterial()
        {
            if (arrowMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    return GetJunctionMaterial();
                }

                arrowMaterial = new Material(shader);
                arrowMaterial.SetInt("_SrcBlend", 5);
                arrowMaterial.SetInt("_DstBlend", 10);
                arrowMaterial.SetInt("_Cull", 2);
                arrowMaterial.SetInt("_ZWrite", 0);
                arrowMaterial.SetInt("_ZTest", 8);
                arrowMaterial.renderQueue = 4001;
            }

            return arrowMaterial;
        }

        private static Mesh BuildArrowMesh(Transform frame, Color color)
        {
            const int segments = 12;
            const float length = 0.22f;
            const float headLength = 0.075f;
            const float shaftRadius = 0.010f;
            const float headRadius = 0.030f;
            float shaftEnd = length - headLength;
            Vector3 light = new Vector3(0.35f, 0.8f, 0.45f).normalized;

            System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Color> colors = new System.Collections.Generic.List<Color>();
            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

            System.Func<Vector3, Vector3, int> add = (position, normal) =>
            {
                Vector3 world = frame.TransformDirection(normal).normalized;
                float k = Vector3.Dot(world, light) * 0.5f + 0.5f;
                float shade = 0.45f + 0.65f * k;
                vertices.Add(position);
                colors.Add(new Color(Mathf.Min(1f, color.r * shade), Mathf.Min(1f, color.g * shade), Mathf.Min(1f, color.b * shade), color.a));
                return vertices.Count - 1;
            };

            Vector3 back = new Vector3(0f, 0f, -1f);
            int[] shaftBack = new int[segments];
            int[] shaftFront = new int[segments];
            int[] capRing = new int[segments];
            int[] innerRing = new int[segments];
            int[] outerRing = new int[segments];
            int[] coneBase = new int[segments];
            int[] coneTip = new int[segments];
            int capCenter = add(Vector3.zero, back);
            for (int j = 0; j < segments; j++)
            {
                float angle = Mathf.PI * 2f * j / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Vector3 radial = new Vector3(cos, sin, 0f);
                shaftBack[j] = add(radial * shaftRadius, radial);
                shaftFront[j] = add(radial * shaftRadius + new Vector3(0f, 0f, shaftEnd), radial);
                capRing[j] = add(radial * shaftRadius, back);
                innerRing[j] = add(radial * shaftRadius + new Vector3(0f, 0f, shaftEnd), back);
                outerRing[j] = add(radial * headRadius + new Vector3(0f, 0f, shaftEnd), back);
                Vector3 slant = new Vector3(cos * headLength, sin * headLength, headRadius).normalized;
                coneBase[j] = add(radial * headRadius + new Vector3(0f, 0f, shaftEnd), slant);
                float mid = Mathf.PI * 2f * (j + 0.5f) / segments;
                Vector3 midSlant = new Vector3(Mathf.Cos(mid) * headLength, Mathf.Sin(mid) * headLength, headRadius).normalized;
                coneTip[j] = add(new Vector3(0f, 0f, length), midSlant);
            }

            for (int j = 0; j < segments; j++)
            {
                int n = (j + 1) % segments;
                triangles.Add(shaftBack[j]); triangles.Add(shaftBack[n]); triangles.Add(shaftFront[j]);
                triangles.Add(shaftBack[n]); triangles.Add(shaftFront[n]); triangles.Add(shaftFront[j]);
                triangles.Add(capCenter); triangles.Add(capRing[n]); triangles.Add(capRing[j]);
                triangles.Add(innerRing[j]); triangles.Add(outerRing[n]); triangles.Add(outerRing[j]);
                triangles.Add(innerRing[j]); triangles.Add(innerRing[n]); triangles.Add(outerRing[n]);
                triangles.Add(coneBase[j]); triangles.Add(coneBase[n]); triangles.Add(coneTip[j]);
            }

            Mesh mesh = new Mesh();
            mesh.name = "VRS_Arrow";
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material junctionMaterial;

        internal static Material GetJunctionMaterial()
        {
            if (junctionMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    return GetMaterial();
                }

                junctionMaterial = new Material(shader);
                junctionMaterial.SetInt("_SrcBlend", 5);
                junctionMaterial.SetInt("_DstBlend", 10);
                junctionMaterial.SetInt("_Cull", 0);
                junctionMaterial.SetInt("_ZWrite", 0);
                junctionMaterial.SetInt("_ZTest", 8);
                junctionMaterial.renderQueue = 4000;
            }

            return junctionMaterial;
        }

        private static GameObject BuildJunctionRings(Piece piece, Material material, bool splitter)
        {
            GameObject holder = new GameObject("VRS_SocketPreview");
            Transform root = piece.transform;
            float axis = Conveyor.JunctionAxis;
            Vector3 back = new Vector3(0f, axis, Conveyor.JunctionBackZ);
            Vector3[] barrels = { new Vector3(-Conveyor.JunctionBarrelX, axis, Conveyor.JunctionFrontZ), new Vector3(0f, axis, Conveyor.JunctionFrontZ), new Vector3(Conveyor.JunctionBarrelX, axis, Conveyor.JunctionFrontZ) };

            if (splitter)
            {
                AddJunctionRing(holder.transform, root, material, back, Vector3.back, InputColor);
                AddPortArrow(holder.transform, root, back, Vector3.back, true, InputColor);
                foreach (Vector3 barrel in barrels)
                {
                    AddJunctionRing(holder.transform, root, material, barrel, Vector3.forward, OutputColor);
                    AddPortArrow(holder.transform, root, barrel, Vector3.forward, false, OutputColor);
                }
            }
            else
            {
                foreach (Vector3 barrel in barrels)
                {
                    AddJunctionRing(holder.transform, root, material, barrel, Vector3.forward, InputColor);
                    AddPortArrow(holder.transform, root, barrel, Vector3.forward, true, InputColor);
                }

                AddJunctionRing(holder.transform, root, material, back, Vector3.back, OutputColor);
                AddPortArrow(holder.transform, root, back, Vector3.back, false, OutputColor);
            }

            return holder;
        }

        private static void AddJunctionRing(Transform parent, Transform root, Material material, Vector3 localCenter, Vector3 localNormal, Color color)
        {
            Vector3 normal = root.TransformDirection(localNormal).normalized;
            Vector3 center = root.TransformPoint(localCenter);
            Vector3 u = Vector3.Cross(normal, Vector3.up);
            if (u.sqrMagnitude < 0.01f)
            {
                u = root.right;
            }

            u.Normalize();
            Vector3 v = Vector3.Cross(normal, u).normalized;
            AddRing(parent, material, center, u, v, 0.05f, color, normal);
        }

        private static void AddConveyorRing(Transform parent, Transform root, Material material, Vector3 localCenter, Vector3 localNormal, Color color)
        {
            Vector3 normal = root.TransformDirection(localNormal).normalized;
            Vector3 center = root.TransformPoint(localCenter);
            Vector3 u = Vector3.Cross(normal, Vector3.up);
            if (u.sqrMagnitude < 0.01f)
            {
                u = root.right;
            }

            u.Normalize();
            Vector3 v = Vector3.Cross(normal, u).normalized;
            AddRing(parent, material, center, u, v, MachineRingRadius, color, normal);
        }

        private static GameObject BuildChestRings(Piece piece, Material material)
        {
            buildItems = null;
            Transform root = piece.transform;
            if (!TryGetLocalBounds(root, out Bounds bounds))
            {
                return null;
            }

            GameObject holder = new GameObject("VRS_SocketPreview");
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;

            AddFace(holder.transform, root, material, c + new Vector3(e.x, 0f, 0f), Vector3.right);
            AddFace(holder.transform, root, material, c - new Vector3(e.x, 0f, 0f), Vector3.left);
            AddFace(holder.transform, root, material, c + new Vector3(0f, 0f, e.z), Vector3.forward);
            AddFace(holder.transform, root, material, c - new Vector3(0f, 0f, e.z), Vector3.back);
            AddFace(holder.transform, root, material, c + new Vector3(0f, e.y, 0f), Vector3.up);

            return holder;
        }

        private static void AddFace(Transform parent, Transform root, Material material, Vector3 faceCenterLocal, Vector3 normalLocal)
        {
            Vector3 center = root.TransformPoint(faceCenterLocal);
            Vector3 normal = root.TransformDirection(normalLocal).normalized;

            Vector3 right = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f
                ? root.right
                : Vector3.Cross(Vector3.up, -normal).normalized;
            Vector3 v = Vector3.Cross(normal, right).normalized;

            Vector3 lifted = center + normal * 0.03f;

            AddRing(parent, material, lifted - right * PairHalfOffset, right, v, MachineRingRadius, InputColor, normal);
            AddRing(parent, material, lifted + right * PairHalfOffset, right, v, MachineRingRadius, OutputColor, normal);
        }

        private static void AddRing(Transform parent, Material material, Vector3 center, Vector3 u, Vector3 v, float radius, Color color, Vector3 normal)
        {
            GameObject go = new GameObject("Ring");
            go.transform.SetParent(parent, false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = RingWidth;
            line.material = material;
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius);
            }

            Sockets.Add(new Socket { Center = center, Normal = normal, Line = line, BaseColor = color, Owner = buildOwner, Index = buildIndex++, Items = buildItems });
        }
    }
}
namespace VRSAutomation
{
    internal class ArrowMeshOwner : UnityEngine.MonoBehaviour
    {
        internal UnityEngine.Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh != null)
            {
                Destroy(Mesh);
            }
        }
    }
}
