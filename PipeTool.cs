using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VRSAutomation
{
    internal class Pipe
    {
        public GameObject Object;
        public MeshRenderer Renderer;
        public List<Vector3> Points;
        public Vector3 OutNormal;
        public Vector3 InNormal;
        public Piece OutPiece;
        public Piece InPiece;
        public ZDOID OutId;
        public ZDOID InId;
        public int OutIndex;
        public int InIndex;
        public HashSet<string> OutItems;
        public HashSet<string> InItems;
        public Vector3 OutPos;
        public Vector3 InPos;
        public int OutPrefab;
        public int InPrefab;
        public bool OutFlange = true;
        public bool InFlange = true;
        public int ColorIndex;
    }

    internal class MeshBuilder
    {
        public const float Tile = 3.84f;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        private static Vector3 Perpendicular(Vector3 axis)
        {
            Vector3 p = Vector3.Cross(axis, Vector3.up);
            if (p.sqrMagnitude < 0.01f)
            {
                p = Vector3.Cross(axis, Vector3.right);
            }

            return p.normalized;
        }

        private void Triangle(int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(face, normals[a]) < 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public void Tube(Vector3 from, Vector3 to, float radius, int sides, float distance)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.0001f)
            {
                return;
            }

            axis /= length;
            Vector3 p = Perpendicular(axis);
            Vector3 q = Vector3.Cross(axis, p);
            int start = vertices.Count;

            for (int i = 0; i <= sides; i++)
            {
                float t = (float)i / sides;
                float angle = t * Mathf.PI * 2f;
                Vector3 n = p * Mathf.Cos(angle) + q * Mathf.Sin(angle);
                float u = t * (Mathf.PI * 2f * radius / Tile);

                vertices.Add(from + n * radius);
                normals.Add(n);
                uvs.Add(new Vector2(u, distance / Tile));

                vertices.Add(to + n * radius);
                normals.Add(n);
                uvs.Add(new Vector2(u, (distance + length) / Tile));
            }

            for (int i = 0; i < sides; i++)
            {
                int a = start + i * 2;
                Triangle(a, a + 1, a + 2);
                Triangle(a + 1, a + 3, a + 2);
            }
        }

        public void Disc(Vector3 center, Vector3 axis, float radius, int sides)
        {
            Vector3 p = Perpendicular(axis);
            Vector3 q = Vector3.Cross(axis, p);
            int start = vertices.Count;

            vertices.Add(center);
            normals.Add(axis);
            uvs.Add(new Vector2(center.x / Tile, center.z / Tile));

            for (int i = 0; i < sides; i++)
            {
                float angle = (float)i / sides * Mathf.PI * 2f;
                Vector3 position = center + (p * Mathf.Cos(angle) + q * Mathf.Sin(angle)) * radius;
                vertices.Add(position);
                normals.Add(axis);
                uvs.Add(new Vector2(position.x / Tile, position.z / Tile));
            }

            for (int i = 0; i < sides; i++)
            {
                Triangle(start, start + 1 + i, start + 1 + (i + 1) % sides);
            }
        }

        public void Sphere(Vector3 center, float radius)
        {
            const int rings = 6;
            const int sides = 10;
            int start = vertices.Count;

            for (int r = 0; r <= rings; r++)
            {
                float lat = (float)r / rings * Mathf.PI;
                for (int s = 0; s <= sides; s++)
                {
                    float lon = (float)s / sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                    vertices.Add(center + n * radius);
                    normals.Add(n);
                    uvs.Add(new Vector2(lon * radius / Tile, lat * radius / Tile));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = start + r * (sides + 1) + s;
                    int b = a + sides + 1;
                    Triangle(a, b, a + 1);
                    Triangle(a + 1, b, b + 1);
                }
            }
        }

        public void Sweep(List<Vector3> path, float radius, int sides)
        {
            List<Vector3> clean = new List<Vector3>();
            foreach (Vector3 point in path)
            {
                if (clean.Count == 0 || (point - clean[clean.Count - 1]).sqrMagnitude > 0.00000001f)
                {
                    clean.Add(point);
                }
            }

            if (clean.Count < 2)
            {
                return;
            }

            int start = vertices.Count;
            Vector3 frame = Vector3.zero;
            float distance = 0f;
            for (int i = 0; i < clean.Count; i++)
            {
                Vector3 before = i > 0 ? (clean[i] - clean[i - 1]).normalized : Vector3.zero;
                Vector3 after = i + 1 < clean.Count ? (clean[i + 1] - clean[i]).normalized : Vector3.zero;
                Vector3 tangent = before + after;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = after.sqrMagnitude > 0f ? after : before;
                }

                tangent.Normalize();
                if (i > 0)
                {
                    distance += Vector3.Distance(clean[i], clean[i - 1]);
                }

                Vector3 p = i == 0 ? Perpendicular(tangent) : frame - tangent * Vector3.Dot(frame, tangent);
                if (p.sqrMagnitude < 0.000001f)
                {
                    p = Perpendicular(tangent);
                }

                p.Normalize();
                frame = p;
                Vector3 q = Vector3.Cross(tangent, p);
                for (int k = 0; k <= sides; k++)
                {
                    float t = (float)k / sides;
                    float angle = t * Mathf.PI * 2f;
                    Vector3 n = p * Mathf.Cos(angle) + q * Mathf.Sin(angle);
                    vertices.Add(clean[i] + n * radius);
                    normals.Add(n);
                    uvs.Add(new Vector2(t * (Mathf.PI * 2f * radius / Tile), distance / Tile));
                }
            }

            int row = sides + 1;
            for (int i = 0; i + 1 < clean.Count; i++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * row + k;
                    int b = a + row;
                    Triangle(a, b, a + 1);
                    Triangle(a + 1, b, b + 1);
                }
            }
        }

        public void Flange(Vector3 position, Vector3 normal)
        {
            const float thickness = 0.03f;
            Vector3 top = position + normal * thickness;

            Tube(position, top, 0.1f, 16, 0f);
            Disc(top, normal, 0.1f, 16);

            Vector3 p = Perpendicular(normal);
            Vector3 q = Vector3.Cross(normal, p);
            for (int i = 0; i < 6; i++)
            {
                float angle = (float)i / 6 * Mathf.PI * 2f;
                Vector3 bolt = top + (p * Mathf.Cos(angle) + q * Mathf.Sin(angle)) * 0.075f;
                Tube(bolt, bolt + normal * 0.015f, 0.012f, 6, 0f);
                Disc(bolt + normal * 0.015f, normal, 0.012f, 6);
            }
        }

        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class PipeAwakePatch
    {
        private static bool menuPatched;

        internal static void Postfix(Player __instance)
        {
            if (!menuPatched)
            {
                menuPatched = true;
                try
                {
                    MethodInfo target = AccessTools.Method(typeof(Hud), "TogglePieceSelection");
                    MethodInfo prefix = AccessTools.Method(typeof(PipeMenuBlock), "Prefix");
                    if (target != null && prefix != null)
                    {
                        new Harmony(Plugin.HarmonyId + ".pipemenu").Patch(target, new HarmonyMethod(prefix));
                    }
                    else
                    {
                        Plugin.Log.LogWarning("Build menu toggle not found, right-click will also open the build menu");
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Build menu block failed: " + e.Message);
                }
            }

            if (__instance.GetComponent<PipeTool>() == null)
            {
                __instance.gameObject.AddComponent<PipeTool>();
            }
        }
    }

    internal static class PipeMenuBlock
    {
        private static bool Prefix()
        {
            return !PipeTool.BlockMenu();
        }
    }

    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    internal static class PipeClickPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, Piece piece)
        {
            if (__instance != Player.m_localPlayer || !Plugin.IsPipePiece(piece))
            {
                return;
            }

            PipeTool tool = __instance.GetComponent<PipeTool>();
            if (tool != null)
            {
                tool.HandleClick();
            }
        }
    }

    internal class PipeTool : MonoBehaviour
    {
        private const float AimRange = 50f;
        private const float Radius = 0.05f;
        private const float Stub = 0.15f;
        private const float BendRadius = 0.0625f;
        private const int Sides = 10;
        internal static float MaxPipeLength = 50f;
        private const float TapSeconds = 0.25f;
        private const float HoldSeconds = 2f;
        private const float RingRadius = 30f;

        private static readonly Color PipeInvalidColor = new Color32(0xC8, 0x10, 0x1F, 255);
        private static readonly Color HoverTint = new Color(1.7f, 1.7f, 1.7f, 1f);

        internal static readonly List<Pipe> Pipes = new List<Pipe>();
        private static readonly List<Vector3> points = new List<Vector3>();

        private static bool shiftActive;
        private static Quaternion shiftFrame;
        private static int shiftAxis = -1;
        private static int shiftPointCount;
        private static bool drafting;
        private static bool startIsOutput;
        private static Piece startPiece;
        private static ZDOID startId;
        private static int startIndex;
        private static Vector3 startNormal;
        private static HashSet<string> startItems;
        private static Pipe regrabbed;
        private static Vector3 draftEnd;
        private static bool draftValid;
        private static GameObject draftObject;
        private static MeshFilter draftFilter;
        private static MeshRenderer draftRenderer;
        private static Vector3 builtEnd;
        private static int builtCount = -1;
        private static bool builtValid;
        private static bool builtSnapped;

        private static Texture2D pipeTexture;
        private static Material pipeMaterial;
        private static Material invalidMaterial;

        private static bool rightActive;
        private static float rightStart;
        private static Pipe rightPipe;
        private static SocketRef rightSocket;
        private static float holdProgress;
        private static Pipe hovered;

        private Player player;
        private int draftMask;
        private float lastClick;
        private Texture2D white;

        private void Awake()
        {
            player = GetComponent<Player>();
            draftMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain");
            draftMaskStatic = draftMask;
            SocketPreview.CanHighlight = Eligible;
        }

        private void OnDestroy()
        {
            CancelDraft();
        }

        private static Vector3 SoundPosition()
        {
            return Player.m_localPlayer != null ? Player.m_localPlayer.transform.position + Vector3.up : Vector3.zero;
        }

        private static void PlaySocket(bool output)
        {
            if (output)
            {
                Sounds.Output(SoundPosition());
            }
            else
            {
                Sounds.Input(SoundPosition());
            }
        }

        internal static bool HopEnabled = true;
        internal static KeyCode HopKey = KeyCode.Z;
        internal static string HopReason;
        private static GUIStyle noteStyle;
        private static bool builtBase;
        private static bool builtHop;
        private static int builtPipes = -1;
        private static int draftMaskStatic;
        private const float OverlapDistance = 0.095f;
        private const float HopSeparation = 0.12f;
        private const float RiserOffset = 0.112f;
        private const float HopGroup = 0.4f;
        private const float HopRoom = 0.05f;

        private struct HopCross
        {
            public float T;
            public Vector3 Old;
            public Vector3 Dir;
        }

        private static bool Crossings(Vector3 a, Vector3 b, List<Vector3> own, List<HopCross> found)
        {
            float length = Vector3.Distance(a, b);
            if (length < 0.01f)
            {
                return true;
            }

            Vector3 d = (b - a) / length;
            foreach (Pipe other in Pipes)
            {
                if (other.Object == null || !other.Object.activeSelf || other.Points == null)
                {
                    continue;
                }

                for (int i = 0; i + 1 < other.Points.Count; i++)
                {
                    if (!CheckSegment(a, b, d, length, other.Points[i], other.Points[i + 1], found))
                    {
                        return false;
                    }
                }
            }

            for (int i = 0; i + 2 < own.Count; i++)
            {
                if (!CheckSegment(a, b, d, length, own[i], own[i + 1], found))
                {
                    return false;
                }
            }

            found.Sort((x, y) => x.T.CompareTo(y.T));
            return true;
        }

        private static bool CheckSegment(Vector3 a, Vector3 b, Vector3 d, float length, Vector3 p, Vector3 q, List<HopCross> found)
        {
            float u;
            float v;
            ClosestOnSegments(a, b, p, q, out u, out v);
            Vector3 onNew = a + (b - a) * u;
            Vector3 onOld = p + (q - p) * v;
            if ((onNew - onOld).magnitude >= OverlapDistance)
            {
                return true;
            }

            Vector3 e = q - p;
            if (e.sqrMagnitude < 0.000001f)
            {
                return true;
            }

            Vector3 direction = e.normalized;
            if (Mathf.Abs(Vector3.Dot(d, direction)) > 0.9f)
            {
                HopReason = "Pipes would overlap side by side";
                return false;
            }

            found.Add(new HopCross { T = u * length, Old = onOld, Dir = direction });
            return true;
        }

        private static void ClosestOnSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            s = 0f;
            t = 0f;
            if (a <= 1e-6f && e <= 1e-6f)
            {
                return;
            }

            if (a <= 1e-6f)
            {
                t = Mathf.Clamp01(f / e);
                return;
            }

            float c = Vector3.Dot(d1, r);
            if (e <= 1e-6f)
            {
                s = Mathf.Clamp01(-c / a);
                return;
            }

            float b = Vector3.Dot(d1, d2);
            float denominator = a * e - b * b;
            s = denominator > 1e-6f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
            t = (b * s + f) / e;
            if (t < 0f)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else if (t > 1f)
            {
                t = 1f;
                s = Mathf.Clamp01((b - c) / a);
            }
        }

        private static Vector3 SurfaceNormalAt(Vector3 point)
        {
            Vector3[] directions = { Vector3.down, Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            float best = float.MaxValue;
            Vector3 normal = Vector3.zero;
            foreach (Vector3 direction in directions)
            {
                RaycastHit hit;
                if (Physics.Raycast(point, direction, out hit, 0.25f, draftMaskStatic, QueryTriggerInteraction.Ignore) && hit.distance < best)
                {
                    best = hit.distance;
                    normal = hit.normal;
                }
            }

            return normal;
        }

        internal static List<Vector3> ApplyHops(List<Vector3> route)
        {
            HopReason = null;
            if (!HopEnabled || (Pipes.Count == 0 && route.Count < 4))
            {
                return route;
            }

            List<Vector3> result = new List<Vector3>();
            result.Add(route[0]);
            for (int s = 0; s + 1 < route.Count; s++)
            {
                Vector3 a = route[s];
                Vector3 b = route[s + 1];
                List<HopCross> found = new List<HopCross>();
                if (!Crossings(a, b, result, found))
                {
                    return null;
                }

                if (found.Count > 0 && !AddHops(result, a, b, found))
                {
                    return null;
                }

                result.Add(b);
            }

            return result;
        }

        private static bool AddHops(List<Vector3> result, Vector3 a, Vector3 b, List<HopCross> found)
        {
            float length = Vector3.Distance(a, b);
            Vector3 d = (b - a) / length;
            Vector3 normal = SurfaceNormalAt(a + d * found[0].T);
            Vector3 h = normal.sqrMagnitude > 0.01f ? normal : Vector3.up;
            h -= d * Vector3.Dot(h, d);
            if (h.sqrMagnitude < 0.25f)
            {
                h = Vector3.up - d * Vector3.Dot(Vector3.up, d);
                if (h.sqrMagnitude < 0.25f)
                {
                    return true;
                }
            }

            h.Normalize();

            List<float> starts = new List<float>();
            List<float> ends = new List<float>();
            List<float> lifts = new List<float>();
            foreach (HopCross cross in found)
            {
                Vector3 flat = cross.Dir - h * Vector3.Dot(cross.Dir, h);
                if (flat.sqrMagnitude < 0.0625f)
                {
                    HopReason = "Cannot hop over a pipe that runs the same way";
                    return false;
                }

                float sine = Vector3.Cross(d, flat.normalized).magnitude;
                if (sine < 0.25f)
                {
                    HopReason = "Pipes would overlap side by side";
                    return false;
                }

                float lift = Vector3.Dot(cross.Old - (a + d * cross.T), h) + HopSeparation;
                if (lift <= 0.02f)
                {
                    continue;
                }

                float offset = RiserOffset / sine;
                starts.Add(cross.T - offset);
                ends.Add(cross.T + offset);
                lifts.Add(lift);
            }

            int index = 0;
            while (index < starts.Count)
            {
                float start = starts[index];
                float end = ends[index];
                float lift = lifts[index];
                index++;
                while (index < starts.Count && starts[index] - end < HopGroup)
                {
                    end = Mathf.Max(end, ends[index]);
                    lift = Mathf.Max(lift, lifts[index]);
                    index++;
                }

                if (start < HopRoom || end > length - HopRoom)
                {
                    HopReason = "Not enough room to hop here";
                    return false;
                }

                Vector3 baseA = a + d * start;
                Vector3 baseB = a + d * end;
                Vector3 topA = baseA + h * lift;
                Vector3 topB = baseB + h * lift;
                float clear = Radius * 0.9f;
                if (Physics.CheckCapsule(baseA, topA, clear, draftMaskStatic, QueryTriggerInteraction.Ignore)
                    || Physics.CheckCapsule(topA, topB, clear, draftMaskStatic, QueryTriggerInteraction.Ignore)
                    || Physics.CheckCapsule(topB, baseB, clear, draftMaskStatic, QueryTriggerInteraction.Ignore))
                {
                    HopReason = "Something is in the way of the hop";
                    return false;
                }

                result.Add(baseA);
                result.Add(topA);
                result.Add(topB);
                result.Add(baseB);
            }

            return true;
        }

        private static bool Eligible(SocketRef socket)
        {
            return !drafting || CanFinish(socket);
        }

        private static bool CanFinish(SocketRef socket)
        {
            if (socket.IsOutput == startIsOutput || socket.Owner == startPiece)
            {
                return false;
            }

            if (PipeAt(socket, regrabbed) != null)
            {
                return false;
            }

            if (Vector3.Distance(points[0], socket.Center) > MaxPipeLength)
            {
                return false;
            }

            return Compatible(socket);
        }

        private static bool Compatible(SocketRef socket)
        {
            HashSet<string> outItems = startIsOutput ? startItems : socket.Items;
            HashSet<string> inItems = startIsOutput ? socket.Items : startItems;
            if (outItems == null || inItems == null)
            {
                return true;
            }

            foreach (string item in outItems)
            {
                if (inItems.Contains(item))
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            if (!SocketPreview.PipeActive)
            {
                CancelDraft();
                rightActive = false;
                holdProgress = 0f;
                SetHover(null);
                PipeColors.Close();
                return;
            }

            PipeFlow.Draw();

            if (PipeColors.Step())
            {
                SetHover(null);
                return;
            }

            if (Input.GetKeyDown(HopKey))
            {
                HopEnabled = !HopEnabled;
                player.Message(MessageHud.MessageType.TopLeft, "Pipe hop: " + (HopEnabled ? "on" : "off"));
            }

            UpdateRight();
            UpdateDraft();
            SetHover(drafting ? null : (CtrlHeld() ? (PipeUnderCrosshair() ?? AimedEnd()) : AimedEnd()));
        }

        private static bool CtrlHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static Pipe PipeUnderCrosshair()
        {
            if (GameCamera.instance == null)
            {
                return null;
            }

            Transform cam = GameCamera.instance.transform;
            Pipe best = null;
            float bestDistance = 0.12f;
            foreach (Pipe pipe in Pipes)
            {
                if (pipe.Object == null || !pipe.Object.activeSelf)
                {
                    continue;
                }

                for (int i = 0; i + 1 < pipe.Points.Count; i++)
                {
                    float along;
                    float distance = RaySegmentDistance(cam.position, cam.forward, pipe.Points[i], pipe.Points[i + 1], out along);
                    if (along > 0f && along < 14f && distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = pipe;
                    }
                }
            }

            return best;
        }

        private static float RaySegmentDistance(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, out float along)
        {
            Vector3 u = b - a;
            Vector3 w = origin - a;
            float dd = Vector3.Dot(direction, direction);
            float du = Vector3.Dot(direction, u);
            float uu = Vector3.Dot(u, u);
            float dw = Vector3.Dot(direction, w);
            float uw = Vector3.Dot(u, w);
            float denominator = dd * uu - du * du;
            float s;
            float t;
            if (denominator < 1e-6f)
            {
                s = 0f;
                t = uu > 1e-6f ? Mathf.Clamp01(uw / uu) : 0f;
            }
            else
            {
                s = (du * uw - uu * dw) / denominator;
                t = Mathf.Clamp01((dd * uw - du * dw) / denominator);
                s = Mathf.Max(0f, (t * du - dw) / dd);
                if (s <= 0f)
                {
                    t = uu > 1e-6f ? Mathf.Clamp01(uw / uu) : 0f;
                }
            }

            along = s;
            return (w + direction * s - u * t).magnitude;
        }

        internal static bool BlockMenu()
        {
            return SocketPreview.PipeActive && (drafting || AimedEnd() != null);
        }

        private static Pipe AimedEnd()
        {
            SocketRef socket = SocketPreview.Aimed;
            if (socket == null)
            {
                return null;
            }

            Pipe pipe = PipeAt(socket, null);
            return pipe != null && pipe.Object.activeSelf ? pipe : null;
        }

        private static void UpdateRight()
        {
            if (Input.GetMouseButtonDown(1))
            {
                rightActive = false;
                rightPipe = null;
                if (drafting)
                {
                    rightActive = true;
                }
                else
                {
                    Pipe pipe = AimedEnd();
                    if (pipe != null)
                    {
                        rightActive = true;
                        rightPipe = pipe;
                        rightSocket = SocketPreview.Aimed;
                    }
                }

                rightStart = Time.time;
                holdProgress = 0f;
            }

            if (!rightActive)
            {
                return;
            }

            float elapsed = Time.time - rightStart;
            holdProgress = elapsed >= TapSeconds ? elapsed / HoldSeconds : 0f;

            if (elapsed >= HoldSeconds)
            {
                rightActive = false;
                holdProgress = 0f;
                if (drafting)
                {
                    ClearDraft();
                    Sounds.Disconnect(SoundPosition());
                }
                else if (rightPipe != null)
                {
                    Remove(rightPipe);
                    Sounds.Disconnect(SoundPosition());
                    Plugin.Log.LogInfo("Pipe deleted");
                }

                return;
            }

            if (Input.GetMouseButtonUp(1))
            {
                rightActive = false;
                holdProgress = 0f;
                if (elapsed < TapSeconds)
                {
                    if (drafting)
                    {
                        Undo();
                    }
                    else if (rightPipe != null)
                    {
                        Regrab(rightPipe, rightSocket);
                    }
                }
            }
        }

        internal void HandleClick()
        {
            if (Time.time - lastClick < 0.2f || !SocketPreview.PipeActive)
            {
                return;
            }

            lastClick = Time.time;
            SocketRef aimed = SocketPreview.Aimed;

            if (!drafting && CtrlHeld())
            {
                Pipe target = PipeUnderCrosshair();
                if (target != null)
                {
                    if (target.ColorIndex != PipeColors.Selected && PipeLine.Recolor(target, PipeColors.Selected))
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Pipe color: " + PipeColors.Names[PipeColors.Selected]);
                        Sounds.Snap(SoundPosition());
                    }

                    return;
                }
            }

            if (!drafting)
            {
                if (aimed != null && PipeAt(aimed, null) == null)
                {
                    points.Clear();
                    points.Add(aimed.Center);
                    points.Add(aimed.Center + aimed.Normal * Stub);
                    startIsOutput = aimed.IsOutput;
                    startPiece = aimed.Owner;
                    startId = IdOf(aimed.Owner);
                    startIndex = aimed.Index;
                    startNormal = aimed.Normal;
                    startItems = aimed.Items;
                    regrabbed = null;
                    draftEnd = points[1];
                    builtCount = -1;
                    drafting = true;
                    PlaySocket(aimed.IsOutput);
                    Plugin.Log.LogInfo("Pipe started at " + aimed.Center.ToString("F2") + " facing " + aimed.Normal.ToString("F2"));
                }

                return;
            }

            if (aimed != null)
            {
                if (CanFinish(aimed))
                {
                    Finish(aimed);
                }

                return;
            }

            if (draftValid)
            {
                points.Add(draftEnd);
                Sounds.Snap(SoundPosition());
                Plugin.Log.LogInfo("Pipe bend at " + draftEnd.ToString("F2"));
            }
        }

        private static void Finish(SocketRef end)
        {
            List<Vector3> route = new List<Vector3>(points);
            route.Add(end.Center + end.Normal * Stub);
            route.Add(end.Center);

            List<Vector3> hopped = ApplyHops(route);
            if (hopped == null)
            {
                if (Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "The pipe has no room to pass over another pipe here");
                }

                return;
            }

            route = hopped;
            Pipe pipe = new Pipe();
            if (startIsOutput)
            {
                pipe.OutPiece = startPiece;
                pipe.OutId = startId;
                pipe.OutIndex = startIndex;
                pipe.OutNormal = startNormal;
                pipe.OutItems = startItems;
                pipe.InPiece = end.Owner;
                pipe.InId = IdOf(end.Owner);
                pipe.InIndex = end.Index;
                pipe.InNormal = end.Normal;
                pipe.InItems = end.Items;
            }
            else
            {
                route.Reverse();
                pipe.OutPiece = end.Owner;
                pipe.OutId = IdOf(end.Owner);
                pipe.OutIndex = end.Index;
                pipe.OutNormal = end.Normal;
                pipe.OutItems = end.Items;
                pipe.InPiece = startPiece;
                pipe.InId = startId;
                pipe.InIndex = startIndex;
                pipe.InNormal = startNormal;
                pipe.InItems = startItems;
            }

            FillEnd(pipe.OutPiece, out pipe.OutPos, out pipe.OutPrefab);
            FillEnd(pipe.InPiece, out pipe.InPos, out pipe.InPrefab);
            pipe.OutFlange = !IsJunctionPiece(pipe.OutPiece);
            pipe.InFlange = !IsJunctionPiece(pipe.InPiece);
            pipe.Points = route;
            pipe.ColorIndex = PipeColors.Selected;
            PipeLine.Create(pipe);

            if (regrabbed != null)
            {
                Remove(regrabbed);
                regrabbed = null;
            }

            drafting = false;
            points.Clear();
            if (draftObject != null)
            {
                draftObject.SetActive(false);
            }

            PlaySocket(end.IsOutput);
            Plugin.Log.LogInfo("Pipe finished, route " + string.Join(" | ", route.ConvertAll(v => v.ToString("F2")).ToArray()));
        }

        internal static void CancelDraft()
        {
            if (!drafting)
            {
                return;
            }

            if (regrabbed != null)
            {
                regrabbed.Object.SetActive(true);
                regrabbed = null;
            }

            drafting = false;
            points.Clear();
            if (draftObject != null)
            {
                draftObject.SetActive(false);
            }

            Plugin.Log.LogInfo("Pipe cancelled");
        }

        private static void ClearDraft()
        {
            if (!drafting)
            {
                return;
            }

            Pipe doomed = regrabbed;
            regrabbed = null;
            if (doomed != null)
            {
                Remove(doomed);
                Plugin.Log.LogInfo("Pipe cleared");
            }

            CancelDraft();
        }

        private static void Undo()
        {
            if (points.Count > 2)
            {
                points.RemoveAt(points.Count - 1);
                Sounds.Undo(SoundPosition());
            }
        }

        private static void Regrab(Pipe pipe, SocketRef socket)
        {
            ZDOID id = IdOf(socket.Owner);
            bool grabInput = pipe.InId.Equals(id) && pipe.InIndex == socket.Index;

            List<Vector3> route = new List<Vector3>(pipe.Points);
            if (!grabInput)
            {
                route.Reverse();
            }

            route.RemoveAt(route.Count - 1);
            route.RemoveAt(route.Count - 1);

            points.Clear();
            points.AddRange(route);
            startIsOutput = grabInput;
            startPiece = grabInput ? pipe.OutPiece : pipe.InPiece;
            startId = grabInput ? pipe.OutId : pipe.InId;
            startIndex = grabInput ? pipe.OutIndex : pipe.InIndex;
            startNormal = grabInput ? pipe.OutNormal : pipe.InNormal;
            startItems = grabInput ? pipe.OutItems : pipe.InItems;
            regrabbed = pipe;
            pipe.Object.SetActive(false);
            draftEnd = points[points.Count - 1];
            builtCount = -1;
            drafting = true;
            Plugin.Log.LogInfo("Pipe grabbed");
        }

        private void UpdateDraft()
        {
            if (!drafting)
            {
                return;
            }

            if (draftObject == null)
            {
                draftObject = new GameObject("VRS_PipeDraft");
                draftFilter = draftObject.AddComponent<MeshFilter>();
                draftRenderer = draftObject.AddComponent<MeshRenderer>();
            }

            draftObject.SetActive(true);

            SocketRef snap = SocketPreview.Aimed;
            bool snapped = snap != null && CanFinish(snap);
            if (snapped)
            {
                draftEnd = snap.Center;
                draftValid = true;
            }
            else
            {
                Transform cam = GameCamera.instance.transform;
                bool hit = Physics.Raycast(cam.position, cam.forward, out RaycastHit info, AimRange, draftMask, QueryTriggerInteraction.Ignore);
                if (hit)
                {
                    Vector3 target = info.point + info.normal * Radius;
                    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (!shift)
                    {
                        shiftActive = false;
                    }
                    else
                    {
                        if (!shiftActive || shiftPointCount != points.Count)
                        {
                            Piece surface = info.collider.GetComponentInParent<Piece>();
                            shiftFrame = surface != null ? surface.transform.rotation : Quaternion.identity;
                            shiftAxis = -1;
                            shiftPointCount = points.Count;
                            shiftActive = true;
                        }

                        Vector3 last = points[points.Count - 1];
                        Vector3 delta = target - last;
                        Vector3[] axes = { shiftFrame * Vector3.right, shiftFrame * Vector3.up, shiftFrame * Vector3.forward };
                        float[] lengths = { Vector3.Dot(delta, axes[0]), Vector3.Dot(delta, axes[1]), Vector3.Dot(delta, axes[2]) };
                        int strongest = 0;
                        for (int i = 1; i < 3; i++)
                        {
                            if (Mathf.Abs(lengths[i]) > Mathf.Abs(lengths[strongest]))
                            {
                                strongest = i;
                            }
                        }

                        if (shiftAxis < 0 || (strongest != shiftAxis && Mathf.Abs(lengths[strongest]) > Mathf.Abs(lengths[shiftAxis]) * 1.5f))
                        {
                            shiftAxis = strongest;
                        }

                        target = last + axes[shiftAxis] * lengths[shiftAxis];
                    }

                    draftEnd = target;
                }

                draftValid = hit && Vector3.Distance(points[0], draftEnd) <= MaxPipeLength;
            }

            if (builtCount == points.Count && builtBase == draftValid && builtSnapped == snapped && (builtEnd - draftEnd).sqrMagnitude < 0.0001f && builtHop == HopEnabled && builtPipes == Pipes.Count)
            {
                draftValid = builtValid;
                return;
            }

            builtCount = points.Count;
            builtBase = draftValid;
            builtSnapped = snapped;
            builtEnd = draftEnd;
            builtHop = HopEnabled;
            builtPipes = Pipes.Count;

            List<Vector3> route = new List<Vector3>(points);
            if (snapped)
            {
                route.Add(snap.Center + snap.Normal * Stub);
                route.Add(snap.Center);
            }
            else
            {
                route.Add(draftEnd);
            }

            List<Vector3> hopped = ApplyHops(route);
            if (hopped == null)
            {
                draftValid = false;
            }
            else
            {
                route = hopped;
            }

            builtValid = draftValid;
            Mesh mesh;
            if (snapped)
            {
                bool endJunction = IsJunctionPiece(snap.Owner);
                mesh = BuildMesh(route, startNormal, !IsJunctionPiece(startPiece), snap.Normal, !endJunction, !endJunction);
            }
            else
            {
                mesh = BuildMesh(route, startNormal, !IsJunctionPiece(startPiece), Vector3.zero, false, true);
            }

            if (draftFilter.sharedMesh != null)
            {
                Destroy(draftFilter.sharedMesh);
            }

            draftFilter.sharedMesh = mesh;
            draftRenderer.sharedMaterial = draftValid ? PipeColors.Material(PipeColors.Selected) : GetInvalidMaterial();
        }

        private static void FillEnd(Piece piece, out Vector3 position, out int prefab)
        {
            position = Vector3.zero;
            prefab = 0;
            ZNetView view = piece != null ? piece.GetComponent<ZNetView>() : null;
            ZDO zdo = view != null ? view.GetZDO() : null;
            if (zdo != null)
            {
                position = zdo.GetPosition();
                prefab = zdo.GetPrefab();
            }
        }

        internal static bool IsJunctionPiece(Piece piece)
        {
            return piece != null && (Conveyor.IsJunction(piece.gameObject.name) || Conveyor.IsAdapter(piece.gameObject.name));
        }

        internal static Mesh BuildMesh(List<Vector3> route, Vector3 firstNormal, bool firstFlange, Vector3 lastNormal, bool lastFlange, bool capLast)
        {
            MeshBuilder builder = new MeshBuilder();
            List<Vector3> path = new List<Vector3>();
            path.Add(route[0]);
            for (int i = 1; i + 1 < route.Count; i++)
            {
                Vector3 corner = route[i];
                Vector3 incoming = corner - route[i - 1];
                Vector3 outgoing = route[i + 1] - corner;
                float lengthIn = incoming.magnitude;
                float lengthOut = outgoing.magnitude;
                if (lengthIn < 0.001f || lengthOut < 0.001f)
                {
                    path.Add(corner);
                    continue;
                }

                incoming /= lengthIn;
                outgoing /= lengthOut;
                float cos = Mathf.Clamp(Vector3.Dot(incoming, outgoing), -1f, 1f);
                float theta = Mathf.Acos(cos);
                if (theta < 0.05f)
                {
                    path.Add(corner);
                    continue;
                }

                float tangent = Mathf.Tan(theta * 0.5f);
                float trim = Mathf.Min(BendRadius * tangent, 0.48f * Mathf.Min(lengthIn, lengthOut));
                float bend = trim / tangent;
                if (theta > 2.6f || bend < Radius * 1.05f)
                {
                    path.Add(corner);
                    builder.Sweep(path, Radius, Sides);
                    builder.Sphere(corner, Radius);
                    path.Clear();
                    path.Add(corner);
                    continue;
                }

                Vector3 start = corner - incoming * trim;
                Vector3 side = (outgoing - incoming * cos).normalized;
                int steps = Mathf.Clamp(Mathf.CeilToInt(theta / 0.35f), 2, 8);
                for (int k = 0; k <= steps; k++)
                {
                    float phi = theta * k / steps;
                    path.Add(start + side * (bend * (1f - Mathf.Cos(phi))) + incoming * (bend * Mathf.Sin(phi)));
                }
            }

            path.Add(route[route.Count - 1]);
            builder.Sweep(path, Radius, Sides);

            if (firstFlange)
            {
                builder.Flange(route[0], firstNormal);
            }

            if (lastFlange)
            {
                builder.Flange(route[route.Count - 1], lastNormal);
            }
            else if (capLast)
            {
                builder.Sphere(route[route.Count - 1], Radius);
            }

            return builder.ToMesh();
        }

        internal static Texture2D GetTexture()
        {
            if (pipeTexture == null)
            {
                pipeTexture = LoadTexture();
            }

            return pipeTexture;
        }

        private static Material GetFallbackMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader);
            material.mainTexture = GetTexture();
            material.color = Color.white;
            return material;
        }

        internal static Material GetPipeMaterial()
        {
            if (pipeMaterial != null)
            {
                return pipeMaterial;
            }

            Material source = null;
            if (ZNetScene.instance != null)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab("wood_floor");
                if (prefab != null)
                {
                    foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Material candidate = renderer.sharedMaterial;
                        if (candidate == null)
                        {
                            continue;
                        }

                        bool snow = candidate.shader.name.IndexOf("snow", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || candidate.name.IndexOf("snow", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!snow)
                        {
                            source = candidate;
                            break;
                        }
                    }
                }
            }

            if (source == null)
            {
                Plugin.Log.LogWarning("No usable wood floor material found, using a plain material");
                pipeMaterial = GetFallbackMaterial();
                return pipeMaterial;
            }

            Plugin.Log.LogInfo("Pipe uses material " + source.name + " shader " + source.shader.name);
            pipeMaterial = new Material(source);
            pipeMaterial.mainTexture = GetTexture();
            pipeMaterial.SetTexture("_BumpMap", null);
            pipeMaterial.color = Color.white;
            pipeMaterial.DisableKeyword("_VALUENOISEVERTEX_ON");
            if (pipeMaterial.HasProperty("_ValueNoiseVertex"))
            {
                pipeMaterial.SetFloat("_ValueNoiseVertex", 0f);
            }

            if (pipeMaterial.HasProperty("_RippleDistance"))
            {
                pipeMaterial.SetFloat("_RippleDistance", 0f);
            }

            return pipeMaterial;
        }

        private static Material GetInvalidMaterial()
        {
            if (invalidMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                invalidMaterial = new Material(shader);
                invalidMaterial.color = PipeInvalidColor;
            }

            return invalidMaterial;
        }

        private static Texture2D LoadTexture()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            byte[] data = Assets.ReadBytes("VRS_pipe.png");
            if (data != null && TryLoadPng(texture, data))
            {
                Plugin.Log.LogInfo("Pipe texture loaded " + texture.width + "x" + texture.height);
            }
            else
            {
                Plugin.Log.LogWarning("VRS_pipe.png not found next to the mod, using a plain color");
                texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, new Color32(0x50, 0x35, 0x24, 255));
                texture.Apply();
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.anisoLevel = 0;
            return texture;
        }

        internal static Sprite LoadToolIcon()
        {
            byte[] data = Assets.ReadBytes("VRS_tool_icon.png");
            if (data == null)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!TryLoadPng(texture, data))
            {
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        internal static bool TryLoadPng(Texture2D texture, byte[] bytes)
        {
            System.Type type = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (type == null)
            {
                return false;
            }

            MethodInfo method = type.GetMethod("LoadImage", new System.Type[] { typeof(Texture2D), typeof(byte[]) });
            return method != null && (bool)method.Invoke(null, new object[] { texture, bytes });
        }

        private static void SetHover(Pipe pipe)
        {
            if (hovered == pipe)
            {
                return;
            }

            if (hovered != null && hovered.Renderer != null)
            {
                hovered.Renderer.material.color = PipeColors.Tint(hovered.ColorIndex);
            }

            hovered = pipe;
            if (hovered != null && hovered.Renderer != null)
            {
                hovered.Renderer.material.color = PipeColors.Tint(hovered.ColorIndex) * HoverTint;
            }
        }

        internal static Pipe PipeAt(SocketRef socket, Pipe ignore)
        {
            ZDOID id = IdOf(socket.Owner);
            foreach (Pipe pipe in Pipes)
            {
                if (pipe == ignore)
                {
                    continue;
                }

                if ((pipe.OutId.Equals(id) && pipe.OutIndex == socket.Index) || (pipe.InId.Equals(id) && pipe.InIndex == socket.Index))
                {
                    return pipe;
                }
            }

            return null;
        }

        private static void Remove(Pipe pipe)
        {
            if (hovered == pipe)
            {
                hovered = null;
            }

            Pipes.Remove(pipe);
            PipeLine.Delete(pipe);
        }

        private static ZDOID IdOf(Piece piece)
        {
            if (piece == null)
            {
                return default(ZDOID);
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            ZDO zdo = view != null ? view.GetZDO() : null;
            return zdo != null ? zdo.m_uid : default(ZDOID);
        }

        private void OnGUI()
        {
            if (drafting && !draftValid && !string.IsNullOrEmpty(HopReason) && HopEnabled)
            {
                if (noteStyle == null)
                {
                    noteStyle = new GUIStyle(GUI.skin.label);
                    noteStyle.alignment = TextAnchor.MiddleCenter;
                    noteStyle.fontSize = 20;
                    noteStyle.fontStyle = FontStyle.Bold;
                }

                Rect note = new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.5f + 60f, 500f, 36f);
                GUI.color = Color.black;
                GUI.Label(new Rect(note.x + 2f, note.y + 2f, note.width, note.height), HopReason, noteStyle);
                GUI.color = new Color(1f, 0.55f, 0.5f);
                GUI.Label(note, HopReason, noteStyle);
                GUI.color = Color.white;
            }

            if (!rightActive || holdProgress < 0.05f)
            {
                return;
            }

            if (white == null)
            {
                white = new Texture2D(1, 1);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            int segments = 48;
            Matrix4x4 saved = GUI.matrix;
            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;
                float angle = (t * 360f - 90f) * Mathf.Deg2Rad;
                Vector2 position = new Vector2(cx + Mathf.Cos(angle) * RingRadius, cy + Mathf.Sin(angle) * RingRadius);
                GUI.color = t <= holdProgress ? PipeInvalidColor : new Color(0.1f, 0.1f, 0.1f, 0.6f);
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(t * 360f, position);
                GUI.DrawTexture(new Rect(position.x - 1.5f, position.y - 4f, 3f, 8f), white);
            }

            GUI.matrix = saved;
            GUI.color = Color.white;
        }
    }

    internal static class PipeFlow
    {
        private const float Speed = 1.2f;
        private const float Spacing = 1.8f;
        private const float MaxRange = 35f;
        private const int MaxPulses = 400;

        private static Mesh mesh;
        private static Material material;
        private static readonly Dictionary<Pipe, float[]> lengths = new Dictionary<Pipe, float[]>();

        internal static bool Enabled = true;

        private static void Prepare()
        {
            if (mesh == null)
            {
                MeshBuilder builder = new MeshBuilder();
                builder.Tube(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.5f, 14, 0f);
                mesh = builder.ToMesh();
            }

            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                material = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Diffuse"));
                material.color = new Color(1f, 0.82f, 0.35f, 1f);
            }
        }

        private static float[] Cumulative(Pipe pipe)
        {
            float[] cumulative;
            if (lengths.TryGetValue(pipe, out cumulative) && cumulative.Length == pipe.Points.Count)
            {
                return cumulative;
            }

            cumulative = new float[pipe.Points.Count];
            for (int i = 1; i < pipe.Points.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(pipe.Points[i], pipe.Points[i - 1]);
            }

            lengths[pipe] = cumulative;
            return cumulative;
        }

        internal static void Draw()
        {
            Camera cam = Camera.main;
            if (!Enabled || cam == null)
            {
                return;
            }

            Prepare();
            Vector3 eye = cam.transform.position;
            Vector3 look = cam.transform.forward;
            float phase = (Time.time * Speed) % Spacing;
            int count = 0;
            foreach (Pipe pipe in PipeTool.Pipes)
            {
                if (pipe.Object == null || !pipe.Object.activeSelf || pipe.Points == null || pipe.Points.Count < 2)
                {
                    continue;
                }

                Vector3 toMiddle = pipe.Points[pipe.Points.Count / 2] - eye;
                if (toMiddle.sqrMagnitude > MaxRange * MaxRange || Vector3.Dot(toMiddle, look) < -3f)
                {
                    continue;
                }

                float[] cumulative = Cumulative(pipe);
                float total = cumulative[cumulative.Length - 1];
                int segment = 0;
                for (float s = phase; s < total && count < MaxPulses; s += Spacing)
                {
                    while (segment + 2 < cumulative.Length && cumulative[segment + 1] < s)
                    {
                        segment++;
                    }

                    float run = cumulative[segment + 1] - cumulative[segment];
                    if (run < 0.0001f)
                    {
                        continue;
                    }

                    Vector3 from = pipe.Points[segment];
                    Vector3 to = pipe.Points[segment + 1];
                    Vector3 position = Vector3.Lerp(from, to, Mathf.Clamp01((s - cumulative[segment]) / run));
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
                    Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, rotation, new Vector3(0.118f, 0.12f, 0.118f)), material, 0);
                    count++;
                }
            }
        }
    }

    internal static class PipeColors
    {
        internal static readonly string[] Names = { "Copper", "Rust", "Moss", "Slate blue", "Ochre", "Charcoal", "Bone", "Dark red" };

        private static readonly Color[] Tints =
        {
            Color.white,
            new Color(0.78f, 0.38f, 0.26f),
            new Color(0.47f, 0.64f, 0.32f),
            new Color(0.38f, 0.55f, 0.74f),
            new Color(0.88f, 0.68f, 0.28f),
            new Color(0.42f, 0.42f, 0.45f),
            new Color(0.92f, 0.88f, 0.76f),
            new Color(0.66f, 0.2f, 0.17f)
        };

        private static readonly Material[] materials = new Material[8];
        private static Texture2D gray;
        private static Texture2D circle;

        internal static int Selected;
        internal static bool WheelOpen;

        internal static Color Tint(int index)
        {
            return index > 0 && index < Tints.Length ? Tints[index] : Color.white;
        }

        private static Color Swatch(int index)
        {
            return index == 0 ? new Color(0.72f, 0.42f, 0.24f) : Tints[index];
        }

        internal static Material Material(int index)
        {
            Material source = PipeTool.GetPipeMaterial();
            if (index <= 0 || index >= Tints.Length)
            {
                return source;
            }

            if (materials[index] == null)
            {
                Material tinted = new Material(source);
                tinted.mainTexture = Gray();
                tinted.color = Tints[index];
                materials[index] = tinted;
            }

            return materials[index];
        }

        private static Texture2D Gray()
        {
            if (gray != null)
            {
                return gray;
            }

            Texture2D source = PipeTool.GetTexture();
            Color32[] pixels;
            try
            {
                pixels = source.GetPixels32();
            }
            catch (System.Exception)
            {
                return source;
            }

            float[] luminance = new float[pixels.Length];
            float sum = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                luminance[i] = (0.299f * pixels[i].r + 0.587f * pixels[i].g + 0.114f * pixels[i].b) / 255f;
                sum += luminance[i];
            }

            float mean = Mathf.Max(0.05f, sum / Mathf.Max(1, pixels.Length));
            Color32[] result = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = (byte)(Mathf.Clamp01(luminance[i] / mean * 0.8f) * 255f);
                result[i] = new Color32(value, value, value, 255);
            }

            gray = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true);
            gray.SetPixels32(result);
            gray.Apply();
            gray.filterMode = source.filterMode;
            gray.wrapMode = TextureWrapMode.Repeat;
            gray.anisoLevel = 0;
            return gray;
        }

        internal static Texture2D Circle()
        {
            if (circle != null)
            {
                return circle;
            }

            const int size = 64;
            circle = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float middle = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - middle) * (x - middle) + (y - middle) * (y - middle));
                    circle.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(size * 0.5f - distance)));
                }
            }

            circle.Apply();
            circle.filterMode = FilterMode.Bilinear;
            return circle;
        }

        internal static void Close()
        {
            WheelOpen = false;
        }

        private static bool nativeOpen;
        private static bool nativeSuppress;
        private static float nativeStarted;
        private static Sprite dot;

        private static bool OpenNative()
        {
            if (dot == null)
            {
                dot = Sprite.Create(Circle(), new Rect(0f, 0f, Circle().width, Circle().height), new Vector2(0.5f, 0.5f), 64f);
            }

            System.Collections.Generic.List<RadialItem> items = new System.Collections.Generic.List<RadialItem>();
            for (int i = 0; i < Names.Length; i++)
            {
                int index = i;
                items.Add(new RadialItem
                {
                    Name = Names[i],
                    Description = i == Selected ? "Current pipe color" : "Use this pipe color",
                    Icon = dot,
                    Tint = Swatch(i),
                    Action = () =>
                    {
                        Selected = index;
                        if (Player.m_localPlayer != null)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Pipe color: " + Names[index]);
                        }

                        return true;
                    }
                });
            }

            return NativeRadial.Open("Pipe color", dot, items, () => Input.GetKey(KeyCode.R));
        }

        internal static bool Step()
        {
            if (nativeOpen)
            {
                if (NativeRadial.Active || Time.time - nativeStarted < 0.25f)
                {
                    return true;
                }

                nativeOpen = false;
                WheelOpen = false;
                nativeSuppress = true;
            }

            if (!Input.GetKey(KeyCode.R))
            {
                nativeSuppress = false;
            }
            else if (nativeSuppress)
            {
                return true;
            }
            else if (!WheelOpen && OpenNative())
            {
                nativeOpen = true;
                nativeStarted = Time.time;
                WheelOpen = true;
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class PipeWheelCursorPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (PipeColors.WheelOpen)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class PipeWheelInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (PipeColors.WheelOpen || (SocketPreview.PipeActive && Input.GetKey(KeyCode.R)))
            {
                __result = false;
            }
        }
    }
}
