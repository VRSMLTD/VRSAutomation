using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VRSAutomation
{
    internal class Conveyor : MonoBehaviour
    {
        internal const string PieceName = "VRS_Conveyor";


        internal const string SplitterName = "VRS_Splitter";
        internal const string CombinerName = "VRS_Combiner";

        internal const string AdapterDualName = "VRS_AdapterDual";
        internal const string AdapterInName = "VRS_AdapterIn";
        internal const string AdapterOutName = "VRS_AdapterOut";

        internal static bool IsJunction(string name)
        {
            return name.StartsWith(SplitterName) || name.StartsWith(CombinerName);
        }

        internal static bool IsAdapter(string name)
        {
            return name.StartsWith(AdapterDualName) || name.StartsWith(AdapterInName) || name.StartsWith(AdapterOutName);
        }

        internal static void Register(ConfigFile config, Sprite icon)
        {
            UvPack.Begin("conveyor");
            Mesh conveyorMesh = BuildMesh();
            UvPack.Finish();
            UvPack.Begin("junction");
            Mesh junctionMesh = BuildJunctionMesh();
            UvPack.Finish();
            UvPack.Begin("adapter");
            Mesh dualMesh = BuildAdapterMesh(0);
            Mesh inMesh = BuildAdapterMesh(1);
            Mesh outMesh = BuildAdapterMesh(-1);
            UvPack.Finish();

            RegisterPiece(config, PieceName, "Conveyor", "Moves items from the box or machine on its input pipe to the one on its output pipe.", "Bronze:2,Wood:2", conveyorMesh, new Vector3(0.32f, 0.27f, 0.46f), new Vector3(0f, 0.135f, 0f), icon);
            RegisterPiece(config, SplitterName, "Splitter", "Shares a conveyor's items between up to three outputs.", "Bronze:3,Wood:3", junctionMesh, new Vector3(0.46f, 0.19f, 0.36f), new Vector3(0f, 0.09f, 0.035f), icon);
            RegisterPiece(config, CombinerName, "Combiner", "Brings up to three inputs together into one output for a conveyor.", "Bronze:3,Wood:3", junctionMesh, new Vector3(0.46f, 0.19f, 0.36f), new Vector3(0f, 0.09f, 0.035f), icon);
            RegisterPiece(config, AdapterDualName, "Storage adapter", "Bolts onto a box so pipes can connect to it: one input and one output.", "Bronze:1,Wood:1", dualMesh, new Vector3(0.34f, 0.24f, 0.26f), new Vector3(0f, 0.12f, 0f), icon);
            RegisterPiece(config, AdapterInName, "Input adapter", "Bolts onto a machine so a pipe can bring items in.", "Bronze:1,Wood:1", inMesh, new Vector3(0.28f, 0.17f, 0.17f), new Vector3(0.0375f, 0.085f, 0f), icon);
            RegisterPiece(config, AdapterOutName, "Output adapter", "Bolts onto a machine so a pipe can take items out.", "Bronze:1,Wood:1", outMesh, new Vector3(0.28f, 0.17f, 0.17f), new Vector3(-0.0375f, 0.085f, 0f), icon);
        }

        private static Mesh BuildAdapterMesh(int side)
        {
            System.Collections.Generic.List<Vector3> v = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uv = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> t = new System.Collections.Generic.List<int>();

            if (side == 0)
            {
                UvPack.Tag = "dual base plate";
                AddBox(v, uv, t, new Vector3(0f, 0.01f, 0f), new Vector3(0.30f, 0.02f, 0.24f));
                UvPack.Tag = "dual bolts";
                float[] bx = { -0.125f, 0.125f };
                float[] bz = { -0.095f, 0.095f };
                foreach (float x in bx)
                {
                    foreach (float z in bz)
                    {
                        AddCylinder(v, uv, t, new Vector3(x, 0.026f, z), Vector3.up, 0.012f, 0.012f, 6);
                    }
                }

                UvPack.Tag = "dual body";
                AddBox(v, uv, t, new Vector3(0f, 0.105f, 0f), new Vector3(0.20f, 0.17f, 0.18f));
                UvPack.Tag = "dual front panel frame";
                AddBox(v, uv, t, new Vector3(0f, 0.105f, 0.095f), new Vector3(0.15f, 0.12f, 0.01f));
                UvPack.Tag = "dual grill slats";
                for (int i = 0; i < 7; i++)
                {
                    AddBox(v, uv, t, new Vector3(0f, 0.058f + i * 0.0155f, 0.103f), new Vector3(0.13f, 0.008f, 0.008f));
                }

                UvPack.Tag = "dual ports";
                foreach (float s in new[] { -1f, 1f })
                {
                    AddCylinder(v, uv, t, new Vector3(s * 0.1075f, 0.105f, 0f), new Vector3(s, 0f, 0f), 0.075f, 0.015f, 18);
                    AddCylinder(v, uv, t, new Vector3(s * 0.13f, 0.105f, 0f), new Vector3(s, 0f, 0f), 0.058f, 0.06f, 16);
                }
            }
            else
            {
                float s = side;
                UvPack.Tag = "single base plate";
                AddBox(v, uv, t, new Vector3(0f, 0.01f, 0f), new Vector3(0.16f, 0.02f, 0.16f));
                UvPack.Tag = "single bolts";
                float[] corners = { -0.062f, 0.062f };
                foreach (float x in corners)
                {
                    foreach (float z in corners)
                    {
                        AddCylinder(v, uv, t, new Vector3(x, 0.025f, z), Vector3.up, 0.01f, 0.01f, 6);
                    }
                }

                UvPack.Tag = "single body";
                AddBox(v, uv, t, new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.12f, 0.12f));
                UvPack.Tag = "single panel frame";
                AddBox(v, uv, t, new Vector3(0f, 0.08f, 0.063f), new Vector3(0.09f, 0.09f, 0.006f));
                UvPack.Tag = "single grill slats";
                for (int i = 0; i < 5; i++)
                {
                    AddBox(v, uv, t, new Vector3(0f, 0.052f + i * 0.0145f, 0.068f), new Vector3(0.08f, 0.007f, 0.006f));
                }

                UvPack.Tag = "single port";
                AddCylinder(v, uv, t, new Vector3(s * 0.0675f, 0.08f, 0f), new Vector3(s, 0f, 0f), 0.07f, 0.015f, 16);
                AddCylinder(v, uv, t, new Vector3(s * 0.105f, 0.08f, 0f), new Vector3(s, 0f, 0f), 0.058f, 0.06f, 16);
            }

            Mesh mesh = new Mesh();
            mesh.name = side == 0 ? "VRS_AdapterDual" : "VRS_AdapterSingle";
            mesh.SetVertices(v);
            UvPack.Track(mesh, uv, t);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void RegisterPiece(ConfigFile config, string name, string title, string description, string defaultCost, Mesh mesh, Vector3 colliderSize, Vector3 colliderCenter, Sprite icon)
        {
            ConfigEntry<string> cost = Costs.Bind(name, title, defaultCost, "build a " + title.ToLower());

            PieceConfig pieceConfig = new PieceConfig();
            pieceConfig.Name = title;
            pieceConfig.Description = description;
            pieceConfig.PieceTable = PieceTables.Hammer;
            pieceConfig.Category = Plugin.CategoryName;
            pieceConfig.Icon = icon != null ? icon : FallbackIcon();
            foreach (KeyValuePair<string, int> part in Costs.Parse(cost.Value, defaultCost))
            {
                pieceConfig.AddRequirement(new RequirementConfig(part.Key, part.Value, 0, true));
            }

            CustomPiece custom = new CustomPiece(name, true, pieceConfig);
            GameObject prefab = custom.PiecePrefab;
            int layer = LayerMask.NameToLayer("piece_nonsolid");
            prefab.layer = layer;

            foreach (MeshRenderer defaultRenderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                DestroyImmediate(defaultRenderer);
            }

            foreach (MeshFilter defaultFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                DestroyImmediate(defaultFilter);
            }

            foreach (Collider defaultCollider in prefab.GetComponentsInChildren<Collider>(true))
            {
                DestroyImmediate(defaultCollider);
            }

            ZNetView view = prefab.GetComponent<ZNetView>();
            if (view != null)
            {
                view.m_persistent = true;
            }

            GameObject model = new GameObject("model");
            model.layer = layer;
            model.transform.SetParent(prefab.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = model.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            if (shader != null)
            {
                Material placeholder = new Material(shader);
                placeholder.color = new Color(0.35f, 0.33f, 0.3f);
                meshRenderer.sharedMaterial = placeholder;
            }

            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = colliderCenter;

            prefab.AddComponent<Conveyor>();
            if (IsJunction(name))
            {
                prefab.AddComponent<JunctionHover>();
            }

            if (IsAdapter(name))
            {
                prefab.AddComponent<AdapterHost>();
            }

            if (name == PieceName)
            {
                prefab.AddComponent<ConveyorInteract>();
            }

            PieceManager.Instance.AddPiece(custom);
        }

        private static Sprite FallbackIcon()
        {
            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color gray = new Color(0.35f, 0.33f, 0.3f, 1f);
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    texture.SetPixel(x, y, gray);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f));
        }

        private void Awake()
        {
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                return;
            }

            MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                Material material = PieceTextures.For(gameObject.name);
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            if (gameObject.name.StartsWith(PieceName) && GetComponent<ConveyorLight>() == null)
            {
                gameObject.AddComponent<ConveyorLight>();
            }

            if (gameObject.name.StartsWith(AdapterDualName) && GetComponent<AdapterScreen>() == null)
            {
                gameObject.AddComponent<AdapterScreen>();
            }

            if ((gameObject.name.StartsWith(PieceName) || IsJunction(gameObject.name)) && GetComponent<MountFeet>() == null)
            {
                gameObject.AddComponent<MountFeet>();
            }
        }

        private static Mesh BuildMesh()
        {
            System.Collections.Generic.List<Vector3> v = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uv = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> t = new System.Collections.Generic.List<int>();

            UvPack.Tag = "conveyor body";
            AddBox(v, uv, t, new Vector3(0f, 0.085f, 0f), new Vector3(0.30f, 0.17f, 0.34f));
            UvPack.Tag = "conveyor top step";
            AddBox(v, uv, t, new Vector3(0f, 0.205f, 0f), new Vector3(0.24f, 0.07f, 0.28f));

            UvPack.Tag = "conveyor collars";
            AddCylinder(v, uv, t, new Vector3(0f, 0.10f, 0.176f), Vector3.forward, 0.108f, 0.012f, 16);
            AddCylinder(v, uv, t, new Vector3(0f, 0.10f, -0.176f), Vector3.back, 0.108f, 0.012f, 16);

            UvPack.Tag = "conveyor bolts";
            float[] xs = { -0.085f, 0.085f };
            float[] zs = { -0.105f, 0.105f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    AddCylinder(v, uv, t, new Vector3(x, 0.246f, z), Vector3.up, 0.014f, 0.012f, 6);
                }
            }

            UvPack.Tag = "conveyor arrow";
            AddBox(v, uv, t, new Vector3(0f, 0.245f, -0.03f), new Vector3(0.03f, 0.01f, 0.10f));
            AddPrism(v, uv, t, new Vector3(0f, 0.24f, 0.02f), 0.05f, 0.07f, 0.01f);

            Mesh mesh = new Mesh();
            mesh.name = "VRS_Conveyor";
            mesh.SetVertices(v);
            UvPack.Track(mesh, uv, t);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal const float JunctionAxis = 0.075f;
        internal const float JunctionBackZ = -0.13f;
        internal const float JunctionFrontZ = 0.20f;
        internal const float JunctionBarrelX = 0.16f;

        private static Mesh BuildJunctionMesh()
        {
            System.Collections.Generic.List<Vector3> v = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uv = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> t = new System.Collections.Generic.List<int>();
            float a = JunctionAxis;

            UvPack.Tag = "junction body";
            AddCylinder(v, uv, t, new Vector3(0f, a, -0.025f), Vector3.forward, 0.075f, 0.15f, 20);
            AddCylinder(v, uv, t, new Vector3(0f, a, -0.08f), Vector3.forward, 0.083f, 0.016f, 20);
            AddCylinder(v, uv, t, new Vector3(0f, a, JunctionBackZ + 0.015f), Vector3.back, 0.05f, 0.03f, 14);

            UvPack.Tag = "junction ribs";
            float[] xs = { -0.025f, 0f, 0.025f };
            foreach (float x in xs)
            {
                AddBox(v, uv, t, new Vector3(x, a + 0.079f, -0.03f), new Vector3(0.012f, 0.01f, 0.05f));
            }

            UvPack.Tag = "junction bent pipes";
            AddElbow(v, uv, t, -1f, a);
            AddElbow(v, uv, t, 1f, a);

            UvPack.Tag = "junction barrels";
            float[] barrels = { -JunctionBarrelX, 0f, JunctionBarrelX };
            foreach (float x in barrels)
            {
                AddCylinder(v, uv, t, new Vector3(x, a, 0.11f), Vector3.forward, 0.068f, 0.12f, 18);
                AddCylinder(v, uv, t, new Vector3(x, a, 0.08f), Vector3.forward, 0.074f, 0.012f, 18);
                AddCylinder(v, uv, t, new Vector3(x, a, JunctionFrontZ - 0.015f), Vector3.forward, 0.05f, 0.03f, 14);
            }

            Mesh mesh = new Mesh();
            mesh.name = "VRS_Junction";
            mesh.SetVertices(v);
            UvPack.Track(mesh, uv, t);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddElbow(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, float side, float axisY)
        {
            const float bendRadius = 0.09f;
            const float pipeRadius = 0.05f;
            const float zStart = -0.06f;
            const int steps = 8;
            float xEnd = JunctionBarrelX;

            Vector3 runStart = new Vector3(side * 0.07f, axisY, zStart);
            Vector3 runEnd = new Vector3(side * (xEnd - bendRadius), axisY, zStart);
            if ((runEnd - runStart).magnitude > 0.001f)
            {
                AddCylinder(v, uv, t, (runStart + runEnd) * 0.5f, (runEnd - runStart).normalized, pipeRadius, (runEnd - runStart).magnitude, 10);
            }

            Vector3 center = new Vector3(side * (xEnd - bendRadius), axisY, zStart + bendRadius);
            Vector3 previousPoint = runEnd;
            Vector3 previousTangent = new Vector3(side, 0f, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float phi = (float)i / steps * Mathf.PI * 0.5f;
                Vector3 point = center + bendRadius * new Vector3(side * Mathf.Sin(phi), 0f, -Mathf.Cos(phi));
                Vector3 tangent = new Vector3(side * Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                AddBendSegment(v, uv, t, previousPoint, previousTangent, point, tangent, pipeRadius, 10);
                previousPoint = point;
                previousTangent = tangent;
            }

            Vector3 tailStart = new Vector3(side * xEnd, axisY, zStart + bendRadius);
            Vector3 tailEnd = new Vector3(side * xEnd, axisY, 0.06f);
            AddCylinder(v, uv, t, (tailStart + tailEnd) * 0.5f, (tailEnd - tailStart).normalized, pipeRadius, (tailEnd - tailStart).magnitude, 10);
        }

        private static void AddBendSegment(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 p0, Vector3 t0, Vector3 p1, Vector3 t1, float radius, int segments)
        {
            Vector3 n0 = Vector3.Cross(t0, Vector3.up).normalized;
            Vector3 n1 = Vector3.Cross(t1, Vector3.up).normalized;
            float circumference = Mathf.PI * 2f * radius;
            float length = Vector3.Distance(p0, p1);
            int start = v.Count;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 d00 = Vector3.up * Mathf.Cos(a0) + n0 * Mathf.Sin(a0);
                Vector3 d01 = Vector3.up * Mathf.Cos(a1) + n0 * Mathf.Sin(a1);
                Vector3 d10 = Vector3.up * Mathf.Cos(a0) + n1 * Mathf.Sin(a0);
                Vector3 d11 = Vector3.up * Mathf.Cos(a1) + n1 * Mathf.Sin(a1);
                AddQuadUv(v, uv, t, p0 + d00 * radius, p0 + d01 * radius, p1 + d11 * radius, p1 + d10 * radius, d00 + d01 + d10 + d11,
                    new Vector2((float)i / segments * circumference, 0f), new Vector2((float)(i + 1) / segments * circumference, 0f),
                    new Vector2((float)(i + 1) / segments * circumference, length), new Vector2((float)i / segments * circumference, length));
            }

            UvPack.Add(start, v.Count - start);
        }

        private static void AddQuadUv(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int start = v.Count;
            v.Add(a);
            v.Add(b);
            v.Add(c);
            v.Add(d);
            uv.Add(ua);
            uv.Add(ub);
            uv.Add(uc);
            uv.Add(ud);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f)
            {
                t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            else
            {
                t.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
        }

        private static void AddQuad(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Vector3 e1 = (b - a).normalized;
            Vector3 e2 = d - a;
            e2 = (e2 - e1 * Vector3.Dot(e2, e1)).normalized;
            int start = v.Count;
            AddQuadUv(v, uv, t, a, b, c, d, outward,
                Vector2.zero, new Vector2(Vector3.Dot(b - a, e1), 0f),
                new Vector2(Vector3.Dot(c - a, e1), Vector3.Dot(c - a, e2)), new Vector2(0f, Vector3.Dot(d - a, e2)));
            UvPack.Add(start, 4);
        }

        private static void AddTriangleUv(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int start = v.Count;
            v.Add(a);
            v.Add(b);
            v.Add(c);
            uv.Add(ua);
            uv.Add(ub);
            uv.Add(uc);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f)
            {
                t.AddRange(new[] { start, start + 1, start + 2 });
            }
            else
            {
                t.AddRange(new[] { start, start + 2, start + 1 });
            }
        }

        private static void AddTriangle(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 e1 = (b - a).normalized;
            Vector3 e2 = c - a;
            e2 = (e2 - e1 * Vector3.Dot(e2, e1)).normalized;
            int start = v.Count;
            AddTriangleUv(v, uv, t, a, b, c, outward, Vector2.zero, new Vector2(Vector3.Dot(b - a, e1), 0f), new Vector2(Vector3.Dot(c - a, e1), Vector3.Dot(c - a, e2)));
            UvPack.Add(start, 3);
        }

        private static void AddBox(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 x = new Vector3(h.x, 0f, 0f);
            Vector3 y = new Vector3(0f, h.y, 0f);
            Vector3 z = new Vector3(0f, 0f, h.z);

            AddQuad(v, uv, t, center + x - y - z, center + x + y - z, center + x + y + z, center + x - y + z, Vector3.right);
            AddQuad(v, uv, t, center - x - y - z, center - x + y - z, center - x + y + z, center - x - y + z, Vector3.left);
            AddQuad(v, uv, t, center - x + y - z, center + x + y - z, center + x + y + z, center - x + y + z, Vector3.up);
            AddQuad(v, uv, t, center - x - y - z, center + x - y - z, center + x - y + z, center - x - y + z, Vector3.down);
            AddQuad(v, uv, t, center - x - y + z, center + x - y + z, center + x + y + z, center - x + y + z, Vector3.forward);
            AddQuad(v, uv, t, center - x - y - z, center + x - y - z, center + x + y - z, center - x + y - z, Vector3.back);
        }

        private static void AddCylinder(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 center, Vector3 axis, float radius, float length, int segments)
        {
            Vector3 p = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            p = Vector3.Cross(axis, p).normalized;
            Vector3 q = Vector3.Cross(axis, p);
            Vector3 bottom = center - axis * (length * 0.5f);
            Vector3 top = center + axis * (length * 0.5f);
            float circumference = Mathf.PI * 2f * radius;

            int sideStart = v.Count;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 d0 = p * Mathf.Cos(a0) + q * Mathf.Sin(a0);
                Vector3 d1 = p * Mathf.Cos(a1) + q * Mathf.Sin(a1);
                float u0 = (float)i / segments * circumference;
                float u1 = (float)(i + 1) / segments * circumference;
                AddQuadUv(v, uv, t, bottom + d0 * radius, bottom + d1 * radius, top + d1 * radius, top + d0 * radius, d0 + d1,
                    new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, length), new Vector2(u0, length));
            }

            UvPack.Add(sideStart, v.Count - sideStart);

            int topStart = v.Count;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 d0 = p * Mathf.Cos(a0) + q * Mathf.Sin(a0);
                Vector3 d1 = p * Mathf.Cos(a1) + q * Mathf.Sin(a1);
                AddTriangleUv(v, uv, t, top, top + d0 * radius, top + d1 * radius, axis,
                    new Vector2(radius, radius), new Vector2(radius + Mathf.Cos(a0) * radius, radius + Mathf.Sin(a0) * radius), new Vector2(radius + Mathf.Cos(a1) * radius, radius + Mathf.Sin(a1) * radius));
            }

            UvPack.Add(topStart, v.Count - topStart);

            int bottomStart = v.Count;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 d0 = p * Mathf.Cos(a0) + q * Mathf.Sin(a0);
                Vector3 d1 = p * Mathf.Cos(a1) + q * Mathf.Sin(a1);
                AddTriangleUv(v, uv, t, bottom, bottom + d0 * radius, bottom + d1 * radius, -axis,
                    new Vector2(radius, radius), new Vector2(radius + Mathf.Cos(a0) * radius, radius + Mathf.Sin(a0) * radius), new Vector2(radius + Mathf.Cos(a1) * radius, radius + Mathf.Sin(a1) * radius));
            }

            UvPack.Add(bottomStart, v.Count - bottomStart);
        }

        private static void AddPrism(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t, Vector3 baseCenter, float halfWidth, float length, float height)
        {
            Vector3 tip = baseCenter + new Vector3(0f, 0f, length);
            Vector3 left = baseCenter + new Vector3(-halfWidth, 0f, 0f);
            Vector3 right = baseCenter + new Vector3(halfWidth, 0f, 0f);
            Vector3 up = new Vector3(0f, height, 0f);

            AddTriangle(v, uv, t, tip + up, left + up, right + up, Vector3.up);
            AddQuad(v, uv, t, left, right, right + up, left + up, Vector3.back);
            AddQuad(v, uv, t, left, tip, tip + up, left + up, new Vector3(-length, 0f, halfWidth));
            AddQuad(v, uv, t, right, tip, tip + up, right + up, new Vector3(length, 0f, halfWidth));
        }
    }
}

namespace VRSAutomation
{
    [HarmonyLib.HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class ConveyorPlacementPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || __instance.m_buildPieces == null || __instance.m_placementGhost == null)
            {
                return;
            }

            Piece selected = __instance.m_buildPieces.GetSelectedPiece();
            if (selected == null || (selected.gameObject.name != Conveyor.PieceName && !Conveyor.IsJunction(selected.gameObject.name) && !Conveyor.IsAdapter(selected.gameObject.name)))
            {
                return;
            }

            GameObject ghost = __instance.m_placementGhost;
            SocketPreview.AttachGhostArrows(ghost, selected.gameObject.name);
            if (Conveyor.IsAdapter(selected.gameObject.name))
            {
                AdapterPlacement.Update(__instance, ghost, selected.gameObject.name);
                return;
            }

            Transform cam = GameCamera.instance.transform;
            RaycastHit hit;
            if (!Physics.Raycast(cam.position, cam.forward, out hit, 50f, __instance.m_placeRayMask)
                || Vector3.Distance(__instance.m_eye.position, hit.point) > __instance.m_maxPlaceDistance)
            {
                __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                __instance.SetPlacementGhostValid(false);
                return;
            }

            Vector3 normal = hit.normal.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.up, normal);
            }

            Quaternion turn = Quaternion.AngleAxis((Conveyor.IsAdapter(selected.gameObject.name) ? 90f : 22.5f) * __instance.m_placeRotation, normal);
            Quaternion rotation = turn * Quaternion.LookRotation(forward.normalized, normal);
            float lift = MountLift.Compute(selected.gameObject.name, hit, normal);
            MountLift.Current = lift;
            Vector3 position = RailSnap.Apply(selected, ghost, hit.point, normal, rotation);
            position -= normal * Vector3.Dot(position - hit.point, normal);
            position += normal * lift;
            ghost.transform.SetPositionAndRotation(position, rotation);
            ghost.SetActive(true);
            __instance.m_placementStatus = Player.PlacementStatus.Valid;
            __instance.SetPlacementGhostValid(true);
        }
    }
}

namespace VRSAutomation
{
    [HarmonyLib.HarmonyPatch(typeof(Player), "PlacePiece")]
    internal static class MachinePieceSoundPatch
    {
        private static void Postfix(Player __instance, Piece piece)
        {
            if (piece == null || __instance != Player.m_localPlayer)
            {
                return;
            }

            string name = piece.gameObject.name;
            if (name != Conveyor.PieceName && name != "VRS_Splitter" && name != "VRS_Combiner" && !Conveyor.IsAdapter(name))
            {
                return;
            }

            Vector3 position = __instance.m_placementGhost != null ? __instance.m_placementGhost.transform.position : __instance.transform.position;
            Sounds.Place(position);
        }
    }
}

namespace VRSAutomation
{
    internal static class AdapterLink
    {
        internal const string HostKey = "vrs_host";
        private const string PosKey = "vrs_host_pos";
        private const string PrefabKey = "vrs_host_prefab";

        private static readonly int[] Hashes =
        {
            Conveyor.AdapterDualName.GetStableHashCode(),
            Conveyor.AdapterInName.GetStableHashCode(),
            Conveyor.AdapterOutName.GetStableHashCode()
        };

        internal static readonly string[] Names = { Conveyor.AdapterDualName, Conveyor.AdapterInName, Conveyor.AdapterOutName };

        internal static bool IsAdapter(ZDO zdo)
        {
            int prefab = zdo.GetPrefab();
            return prefab == Hashes[0] || prefab == Hashes[1] || prefab == Hashes[2];
        }

        internal static ZDO Resolve(ZDO zdo)
        {
            if (zdo == null)
            {
                return null;
            }

            return IsAdapter(zdo) ? Host(zdo) : zdo;
        }

        internal static ZDO Host(ZDO adapter)
        {
            ZDOID id = adapter.GetZDOID(HostKey);
            if (id.Equals(default(ZDOID)) || ZDOMan.instance == null)
            {
                return null;
            }

            int prefab = adapter.GetInt(PrefabKey, 0);
            Vector3 position = adapter.GetVec3(PosKey, Vector3.zero);
            ZDO host = ZDOMan.instance.GetZDO(id);
            if (host != null && (prefab == 0 || (host.GetPrefab() == prefab && (host.GetPosition() - position).sqrMagnitude < 0.0004f)))
            {
                return host;
            }

            if (prefab == 0)
            {
                return null;
            }

            foreach (ZDO other in ZDOMan.instance.m_objectsByID.Values)
            {
                if (other.GetPrefab() == prefab && (other.GetPosition() - position).sqrMagnitude < 0.0004f)
                {
                    return other;
                }
            }

            return null;
        }

        internal static void Link(ZDO adapter, ZDO host)
        {
            adapter.Set(HostKey, host.m_uid);
            adapter.Set(PosKey, host.GetPosition());
            adapter.Set(PrefabKey, host.GetPrefab());
        }
    }

    internal class AdapterHost : MonoBehaviour
    {
        internal static Piece PendingHost;
        internal static float PendingTime;

        private void Start()
        {
            Invoke(nameof(FindHost), 0.25f);
            Invoke(nameof(Recheck), 5f);
            Invoke(nameof(Recheck), 15f);
        }

        private void Recheck()
        {
            ZNetView view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }

            ZDO zdo = view.GetZDO();
            if (!zdo.GetZDOID(AdapterLink.HostKey).Equals(default(ZDOID)))
            {
                Verify(zdo);
                if (gameObject.name.StartsWith(Conveyor.AdapterInName) && zdo.GetInt(AdapterRole.Key, -1) < 0)
                {
                    ZDO linked = AdapterLink.Host(zdo);
                    ZNetView linkedView = linked != null && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(linked) : null;
                    AssignRole(zdo, linkedView != null ? linkedView.GetComponent<Piece>() : null);
                }
            }
        }

        private void AssignRole(ZDO zdo, Piece host)
        {
            if (host == null || !gameObject.name.StartsWith(Conveyor.AdapterInName) || (ZNet.instance != null && ZNet.instance.IsDedicated()))
            {
                return;
            }

            zdo.Set(AdapterRole.Key, AdapterRole.For(host, transform.position));
        }

        private void FindHost()
        {
            ZNetView view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }

            ZDO zdo = view.GetZDO();
            if (!zdo.GetZDOID(AdapterLink.HostKey).Equals(default(ZDOID)))
            {
                Verify(zdo);
                return;
            }

            ZNetView touching = Touching(null);
            if (touching != null)
            {
                AdapterLink.Link(zdo, touching.GetZDO());
                AssignRole(zdo, touching.GetComponent<Piece>());
                Plugin.Log.LogInfo("Adapter " + zdo.m_uid + " sits on " + touching.gameObject.name + " " + touching.GetZDO().m_uid);
                PendingHost = null;
                return;
            }

            if (PendingHost != null && Time.time - PendingTime < 3f)
            {
                ZNetView pendingView = PendingHost.GetComponent<ZNetView>();
                if (pendingView != null && pendingView.IsValid())
                {
                    AdapterLink.Link(zdo, pendingView.GetZDO());
                    AssignRole(zdo, PendingHost);
                    Plugin.Log.LogInfo("Adapter " + zdo.m_uid + " belongs to " + PendingHost.gameObject.name + " " + pendingView.GetZDO().m_uid);
                    PendingHost = null;
                    return;
                }
            }

            Collider[] near = Physics.OverlapSphere(transform.position, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            Piece best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider hit in near)
            {
                if (hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Piece piece = hit.GetComponentInParent<Piece>();
                if (piece == null || !SocketPreview.IsHost(piece.gameObject.name))
                {
                    continue;
                }

                float distance = Vector3.Distance(hit.ClosestPoint(transform.position), transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = piece;
                }
            }

            ZNetView nearView = best != null ? best.GetComponent<ZNetView>() : null;
            if (nearView != null && nearView.IsValid())
            {
                AdapterLink.Link(zdo, nearView.GetZDO());
                AssignRole(zdo, best);
                Plugin.Log.LogInfo("Adapter " + zdo.m_uid + " sits near " + best.gameObject.name + " " + nearView.GetZDO().m_uid);
                return;
            }

            Plugin.Log.LogInfo("Adapter " + zdo.m_uid + " found no box or machine to sit on");
        }

        private static float Gap(ZNetView box, Vector3 point)
        {
            float best = float.MaxValue;
            foreach (Collider collider in box.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                MeshCollider mesh = collider as MeshCollider;
                Vector3 closest = mesh != null && !mesh.convex ? collider.bounds.ClosestPoint(point) : collider.ClosestPoint(point);
                best = Mathf.Min(best, Vector3.Distance(closest, point));
            }

            return best;
        }

        private ZNetView Touching(ZNetView exclude)
        {
            Vector3 point = transform.position;
            ZNetView best = null;
            float bestGap = 0.03f;
            foreach (Collider hit in Physics.OverlapSphere(point, 0.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Piece piece = hit.GetComponentInParent<Piece>();
                if (piece == null || !SocketPreview.IsHost(piece.gameObject.name))
                {
                    continue;
                }

                ZNetView candidate = piece.GetComponent<ZNetView>();
                if (candidate == null || !candidate.IsValid() || candidate == exclude)
                {
                    continue;
                }

                float gap = Gap(candidate, point);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = candidate;
                }
            }

            return best;
        }

        private void Verify(ZDO zdo)
        {
            ZDO linked = AdapterLink.Host(zdo);
            ZNetView linkedView = linked != null && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(linked) : null;
            if (linkedView == null || Gap(linkedView, transform.position) <= 0.3f)
            {
                return;
            }

            ZNetView touching = Touching(linkedView);
            if (touching == null)
            {
                return;
            }

            AdapterLink.Link(zdo, touching.GetZDO());
            Plugin.Log.LogInfo("Adapter " + zdo.m_uid + " was linked to " + linked.m_uid + " but sits on " + touching.gameObject.name + " " + touching.GetZDO().m_uid + ", re-linked");
        }
    }
}

namespace VRSAutomation
{
    internal static class AdapterPlacement
    {
        private static readonly System.Collections.Generic.HashSet<string> Chests = new System.Collections.Generic.HashSet<string>
        {
            "piece_chest_wood",
            "piece_chest",
            "piece_chest_blackmetal"
        };

        private static string Clean(string name)
        {
            int index = name.IndexOf("(Clone)");
            return index >= 0 ? name.Substring(0, index) : name;
        }

        internal static void Update(Player player, GameObject ghost, string pieceName)
        {
            Transform cam = GameCamera.instance.transform;
            RaycastHit hit;
            if (!Physics.Raycast(cam.position, cam.forward, out hit, 50f, player.m_placeRayMask)
                || Vector3.Distance(player.m_eye.position, hit.point) > player.m_maxPlaceDistance)
            {
                player.m_placementStatus = Player.PlacementStatus.Invalid;
                player.SetPlacementGhostValid(false);
                return;
            }

            Vector3 position = hit.point;
            Vector3 normal = hit.normal.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            }

            forward.Normalize();
            bool valid = false;

            Piece host = hit.collider.GetComponentInParent<Piece>();
            if (host != null && SocketPreview.IsKiln(host.gameObject.name))
            {
                Vector3 spot;
                Vector3 spotNormal;
                if (SocketPreview.TryKilnSpot(host, out spot, out spotNormal))
                {
                    if (Clean(pieceName) == Conveyor.AdapterDualName)
                    {
                        position = spot;
                        normal = spotNormal;
                        forward = Vector3.ProjectOnPlane(Vector3.up, normal);
                        if (forward.sqrMagnitude < 0.01f)
                        {
                            forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
                        }

                        forward.Normalize();
                        valid = !Occupied(position, normal, ghost);
                    }
                }
            }
            else if (host != null && SocketPreview.IsHost(host.gameObject.name) && !Chests.Contains(Clean(host.gameObject.name)) && Clean(pieceName) != Conveyor.AdapterDualName)
            {
                bool wantOutput = Clean(pieceName) == Conveyor.AdapterOutName;
                SocketPreview.Spot best = null;
                float bestDistance = 1.2f;
                foreach (SocketPreview.Spot candidate in SocketPreview.MachineSpots(host))
                {
                    float distance = Vector3.Distance(candidate.Center, hit.point);
                    if (candidate.IsOutput == wantOutput && distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }

                if (best != null)
                {
                    position = best.Center;
                    normal = best.Normal;
                    forward = Vector3.ProjectOnPlane(Vector3.up, normal);
                    if (forward.sqrMagnitude < 0.01f)
                    {
                        forward = Vector3.ProjectOnPlane(host.transform.forward, normal);
                    }

                    forward.Normalize();
                    valid = !Occupied(position, normal, ghost, 0.07f);
                }
            }
            else if (host != null && Chests.Contains(Clean(host.gameObject.name)) && Clean(pieceName) == Conveyor.AdapterDualName)
            {
                Vector3 facePosition;
                Vector3 faceNormal;
                Vector3 faceForward;
                if (Face(host.transform, hit.normal, out facePosition, out faceNormal, out faceForward))
                {
                    position = facePosition;
                    normal = faceNormal;
                    forward = faceForward;
                    valid = !Occupied(position, normal, ghost);
                }
            }


            if (valid && host != null)
            {
                AdapterHost.PendingHost = host;
                AdapterHost.PendingTime = Time.time;
            }

            Quaternion rotation = Quaternion.AngleAxis(90f * player.m_placeRotation, normal) * Quaternion.LookRotation(forward, normal);
            ghost.transform.SetPositionAndRotation(position, rotation);
            ghost.SetActive(true);
            player.m_placementStatus = valid ? Player.PlacementStatus.Valid : Player.PlacementStatus.Invalid;
            player.SetPlacementGhostValid(valid);
        }

        private static bool Face(Transform root, Vector3 worldNormal, out Vector3 position, out Vector3 normal, out Vector3 forward)
        {
            position = Vector3.zero;
            normal = Vector3.up;
            forward = Vector3.forward;
            Bounds bounds;
            if (!SocketPreview.TryGetLocalBounds(root, out bounds))
            {
                return false;
            }

            Vector3 local = root.InverseTransformDirection(worldNormal);
            Vector3 axis;
            float extent;
            if (Mathf.Abs(local.y) >= Mathf.Abs(local.x) && Mathf.Abs(local.y) >= Mathf.Abs(local.z))
            {
                if (local.y < 0f)
                {
                    return false;
                }

                axis = Vector3.up;
                extent = bounds.extents.y;
            }
            else if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
            {
                axis = local.x > 0f ? Vector3.right : Vector3.left;
                extent = bounds.extents.x;
            }
            else
            {
                axis = local.z > 0f ? Vector3.forward : Vector3.back;
                extent = bounds.extents.z;
            }

            position = root.TransformPoint(bounds.center + axis * extent);
            normal = root.TransformDirection(axis).normalized;

            RaycastHit[] surface = Physics.RaycastAll(position + normal * 0.4f, -normal, 0.8f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            foreach (RaycastHit candidate in surface)
            {
                if (candidate.collider.transform.IsChildOf(root) && candidate.distance < nearest)
                {
                    nearest = candidate.distance;
                    position = candidate.point;
                }
            }
            if (axis == Vector3.up)
            {
                Vector3 shortAxis = bounds.extents.x < bounds.extents.z ? Vector3.right : Vector3.forward;
                forward = root.TransformDirection(shortAxis).normalized;
            }
            else
            {
                forward = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            }

            return true;
        }

        private static bool Occupied(Vector3 position, Vector3 normal, GameObject ghost, float radius = 0.12f)
        {
            Collider[] hits = Physics.OverlapSphere(position + normal * 0.05f, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(ghost.transform))
                {
                    continue;
                }

                Piece piece = hit.GetComponentInParent<Piece>();
                if (piece != null && Conveyor.IsAdapter(piece.gameObject.name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

namespace VRSAutomation
{
    [HarmonyLib.HarmonyPatch(typeof(Player), "RemovePiece")]
    internal static class AdapterRemovePatch
    {
        private static void Prefix(Player __instance, out System.Collections.Generic.List<Collider> __state)
        {
            __state = null;
            if (__instance != Player.m_localPlayer || GameCamera.instance == null)
            {
                return;
            }

            Transform cam = GameCamera.instance.transform;
            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, 50f, __instance.m_removeRayMask);
            if (hits.Length < 2)
            {
                return;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float adapterDistance = -1f;
            for (int i = 0; i < hits.Length; i++)
            {
                Piece piece = hits[i].collider.GetComponentInParent<Piece>();
                if (piece != null && Conveyor.IsAdapter(piece.gameObject.name))
                {
                    adapterDistance = hits[i].distance;
                    break;
                }
            }

            if (adapterDistance < 0f || adapterDistance - hits[0].distance > 0.8f || Vector3.Distance(__instance.m_eye.position, cam.position + cam.forward * adapterDistance) > __instance.m_maxPlaceDistance)
            {
                return;
            }

            __state = new System.Collections.Generic.List<Collider>();
            for (int i = 0; i < hits.Length && hits[i].distance < adapterDistance; i++)
            {
                Piece blocker = hits[i].collider.GetComponentInParent<Piece>();
                Collider[] colliders = blocker != null ? blocker.GetComponentsInChildren<Collider>() : new[] { hits[i].collider };
                foreach (Collider collider in colliders)
                {
                    if (collider.enabled)
                    {
                        collider.enabled = false;
                        __state.Add(collider);
                    }
                }
            }

            Physics.SyncTransforms();
        }

        private static void Postfix(System.Collections.Generic.List<Collider> __state)
        {
            if (__state == null)
            {
                return;
            }

            foreach (Collider collider in __state)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
        }
    }
}

namespace VRSAutomation
{
    internal static class PortArrows
    {
        private static readonly Color Green = new Color(0.2f, 0.9f, 0.3f);
        private static readonly Color Orange = new Color(1f, 0.55f, 0.1f);

        internal static bool HoldingBuildTool()
        {
            Player player = Player.m_localPlayer;
            ItemDrop.ItemData item = player != null ? player.GetRightItem() : null;
            return item != null && item.m_shared.m_buildPieces != null;
        }

        private static void Add(Transform parent, Vector3 mouth, Vector3 outward, bool inward)
        {
            SocketPreview.BuildArrow(parent, mouth, outward, Vector3.up, inward, inward ? Green : Orange, false);
        }

        internal static void Build(Transform parent, string name)
        {
            float axis = Conveyor.JunctionAxis;
            float back = Conveyor.JunctionBackZ;
            float front = Conveyor.JunctionFrontZ;
            float[] barrels = { -Conveyor.JunctionBarrelX, 0f, Conveyor.JunctionBarrelX };

            if (name.StartsWith(Conveyor.PieceName))
            {
                Add(parent, new Vector3(0f, 0.10f, -0.182f), Vector3.back, true);
                Add(parent, new Vector3(0f, 0.10f, 0.182f), Vector3.forward, false);
            }
            else if (name.StartsWith(Conveyor.SplitterName))
            {
                Add(parent, new Vector3(0f, axis, back), Vector3.back, true);
                foreach (float x in barrels)
                {
                    Add(parent, new Vector3(x, axis, front), Vector3.forward, false);
                }
            }
            else if (name.StartsWith(Conveyor.CombinerName))
            {
                foreach (float x in barrels)
                {
                    Add(parent, new Vector3(x, axis, front), Vector3.forward, true);
                }

                Add(parent, new Vector3(0f, axis, back), Vector3.back, false);
            }
            else if (name.StartsWith(Conveyor.AdapterDualName))
            {
                Add(parent, new Vector3(0.16f, 0.105f, 0f), Vector3.right, true);
                Add(parent, new Vector3(-0.16f, 0.105f, 0f), Vector3.left, false);
            }
            else if (name.StartsWith(Conveyor.AdapterInName))
            {
                Add(parent, new Vector3(0.135f, 0.08f, 0f), Vector3.right, true);
            }
            else if (name.StartsWith(Conveyor.AdapterOutName))
            {
                Add(parent, new Vector3(-0.135f, 0.08f, 0f), Vector3.left, false);
            }
        }
    }

    internal class JunctionHover : MonoBehaviour, Hoverable
    {
        private GameObject arrows;
        private int hoverFrame = -10;

        private void Update()
        {
            if (arrows == null)
            {
                if (ZNet.instance != null && ZNet.instance.IsDedicated())
                {
                    enabled = false;
                    return;
                }

                arrows = new GameObject("VRS_HoverArrows");
                arrows.transform.SetParent(transform, false);
                PortArrows.Build(arrows.transform, gameObject.name);
            }

            arrows.SetActive(PortArrows.HoldingBuildTool() && Time.frameCount - hoverFrame <= 2);
        }

        public string GetHoverText()
        {
            hoverFrame = Time.frameCount;
            return GetHoverName();
        }

        public string GetHoverName()
        {
            return gameObject.name.StartsWith(Conveyor.SplitterName) ? "Splitter" : "Combiner";
        }

        public float GetHoverOffset()
        {
            return 0f;
        }
    }
}

namespace VRSAutomation
{
    internal static class UvPack
    {
        internal const int Size = 1024;
        internal const int Pad = 4;

        internal class Island
        {
            public int Start;
            public int Count;
            public string Tag;
            public int X;
            public int Y;
            public int W;
            public int H;
            public Vector2 Min;
        }

        private class Pending
        {
            public Mesh Mesh;
            public System.Collections.Generic.List<Vector2> Uv;
            public System.Collections.Generic.List<int> Triangles;
            public System.Collections.Generic.List<Island> Islands;
        }

        internal class Template
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            public Vector2[] Uv;
            public System.Collections.Generic.List<Island> Islands;
            public string Name;
        }

        internal static string Tag = "";
        internal static readonly System.Collections.Generic.Dictionary<string, Template> Templates = new System.Collections.Generic.Dictionary<string, Template>();

        private static System.Collections.Generic.List<Island> current = new System.Collections.Generic.List<Island>();
        private static readonly System.Collections.Generic.List<Pending> pending = new System.Collections.Generic.List<Pending>();
        private static string group = "";

        internal static void Begin(string name)
        {
            group = name;
            current = new System.Collections.Generic.List<Island>();
            pending.Clear();
            Tag = "";
        }

        internal static void Add(int start, int count)
        {
            current.Add(new Island { Start = start, Count = count, Tag = Tag });
        }

        internal static void Track(Mesh mesh, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> triangles)
        {
            pending.Add(new Pending { Mesh = mesh, Uv = uv, Triangles = triangles, Islands = current });
            current = new System.Collections.Generic.List<Island>();
            Tag = "";
        }

        private static bool Place(System.Collections.Generic.List<Island> all, float density)
        {
            System.Collections.Generic.List<Island> order = new System.Collections.Generic.List<Island>(all);
            order.Sort((a, b) => b.H.CompareTo(a.H));
            int x = 0;
            int y = 0;
            int row = 0;
            foreach (Island island in order)
            {
                int w = island.W + Pad * 2;
                int h = island.H + Pad * 2;
                if (x + w > Size)
                {
                    x = 0;
                    y += row;
                    row = 0;
                }

                if (y + h > Size)
                {
                    return false;
                }

                island.X = x + Pad;
                island.Y = y + Pad;
                x += w;
                row = Mathf.Max(row, h);
            }

            return true;
        }

        internal static void Finish()
        {
            System.Collections.Generic.List<Island> all = new System.Collections.Generic.List<Island>();
            System.Collections.Generic.Dictionary<Island, Vector2> size = new System.Collections.Generic.Dictionary<Island, Vector2>();
            foreach (Pending item in pending)
            {
                foreach (Island island in item.Islands)
                {
                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                    Vector2 max = new Vector2(float.MinValue, float.MinValue);
                    for (int i = island.Start; i < island.Start + island.Count; i++)
                    {
                        min = Vector2.Min(min, item.Uv[i]);
                        max = Vector2.Max(max, item.Uv[i]);
                    }

                    island.Min = min;
                    size[island] = new Vector2(Mathf.Max(0.004f, max.x - min.x), Mathf.Max(0.004f, max.y - min.y));
                    all.Add(island);
                }
            }

            float density = 2600f;
            bool placed = false;
            for (int attempt = 0; attempt < 60 && !placed; attempt++)
            {
                foreach (Island island in all)
                {
                    island.W = Mathf.Max(2, Mathf.CeilToInt(size[island].x * density));
                    island.H = Mathf.Max(2, Mathf.CeilToInt(size[island].y * density));
                }

                placed = Place(all, density);
                if (!placed)
                {
                    density *= 0.94f;
                }
            }

            if (!placed)
            {
                Plugin.Log.LogWarning("Texture layout '" + group + "' did not fit");
            }

            foreach (Pending item in pending)
            {
                Vector2[] result = new Vector2[item.Uv.Count];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = new Vector2(0.5f, 0.5f);
                }

                foreach (Island island in item.Islands)
                {
                    for (int i = island.Start; i < island.Start + island.Count; i++)
                    {
                        Vector2 local = (item.Uv[i] - island.Min) * density;
                        result[i] = new Vector2((island.X + local.x) / Size, (island.Y + local.y) / Size);
                    }
                }

                item.Mesh.SetUVs(0, result);
                Templates[item.Mesh.name + "#" + group] = new Template { Name = item.Mesh.name, Vertices = item.Mesh.vertices, Triangles = item.Triangles.ToArray(), Uv = result, Islands = item.Islands };
            }

            Plugin.Log.LogInfo("Texture layout '" + group + "': " + all.Count + " regions at " + Mathf.RoundToInt(density) + " pixels per meter");
        }
    }
}

namespace VRSAutomation
{
    internal static class PieceTextures
    {
        private static readonly System.Collections.Generic.Dictionary<string, Material> cache = new System.Collections.Generic.Dictionary<string, Material>();

        internal static string FileFor(string name)
        {
            if (name.StartsWith(Conveyor.PieceName))
            {
                return "VRS_conveyor.png";
            }

            if (Conveyor.IsJunction(name))
            {
                return "VRS_junction.png";
            }

            return Conveyor.IsAdapter(name) ? "VRS_adapter.png" : null;
        }

        internal static Material For(string pieceName)
        {
            string file = FileFor(pieceName);
            Material source = PipeTool.GetPipeMaterial();
            if (file == null || source == null)
            {
                return source;
            }

            Material cached;
            if (cache.TryGetValue(file, out cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(source);
            byte[] data = Assets.ReadBytes(file);
            Texture2D texture = null;
            if (data != null)
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!PipeTool.TryLoadPng(texture, data))
                {
                    texture = null;
                }
            }

            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.anisoLevel = 0;
                material.mainTexture = texture;
                material.mainTextureScale = Vector2.one;
                material.mainTextureOffset = Vector2.zero;
                Plugin.Log.LogInfo("Loaded texture " + file);
            }
            else
            {
                Texture2D generated = PieceArt.Generate(file.Substring(4, file.Length - 8));
                generated.filterMode = FilterMode.Point;
                generated.wrapMode = TextureWrapMode.Clamp;
                generated.anisoLevel = 0;
                material.mainTexture = generated;
                material.mainTextureScale = Vector2.one;
                material.mainTextureOffset = Vector2.zero;
            }

            cache[file] = material;
            return material;
        }
    }
}

namespace VRSAutomation
{
    internal static class UvTemplate
    {
        private static void Fill(Color32[] pixels, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int size = UvPack.Size;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Mathf.Abs(area) < 0.0001f)
            {
                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float w0 = ((b.x - x) * (c.y - y) - (b.y - y) * (c.x - x)) / area;
                    float w1 = ((c.x - x) * (a.y - y) - (c.y - y) * (a.x - x)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 >= -0.001f && w1 >= -0.001f && w2 >= -0.001f)
                    {
                        pixels[y * size + x] = color;
                    }
                }
            }
        }

        private static void Line(Color32[] pixels, Vector2 a, Vector2 b, Color32 color)
        {
            int size = UvPack.Size;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.RoundToInt(p.y);
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    pixels[y * size + x] = color;
                }
            }
        }

        internal static void Write()
        {
            string folder = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "VRS_templates");
            System.IO.Directory.CreateDirectory(folder);
            System.Reflection.MethodInfo encode = HarmonyLib.AccessTools.TypeByName("UnityEngine.ImageConversion").GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            int size = UvPack.Size;
            foreach (string group in new[] { "conveyor", "junction", "adapter" })
            {
                Color32[] pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(26, 28, 32, 255);
                }

                System.Text.StringBuilder text = new System.Text.StringBuilder();
                text.AppendLine("File: VRS_" + group + ".png (1024 x 1024). Rectangles are x, y, width, height from the TOP-LEFT of the image, in pixels.");
                int colorIndex = 0;
                foreach (System.Collections.Generic.KeyValuePair<string, UvPack.Template> entry in UvPack.Templates)
                {
                    if (!entry.Key.EndsWith("#" + group))
                    {
                        continue;
                    }

                    UvPack.Template template = entry.Value;
                    int[] islandOf = new int[template.Uv.Length];
                    Color32[] colors = new Color32[template.Islands.Count];
                    for (int k = 0; k < template.Islands.Count; k++)
                    {
                        UvPack.Island island = template.Islands[k];
                        Color tint = Color.HSVToRGB((colorIndex++ * 0.137f) % 1f, 0.35f, 0.6f);
                        colors[k] = new Color32((byte)(tint.r * 255f), (byte)(tint.g * 255f), (byte)(tint.b * 255f), 255);
                        for (int i = island.Start; i < island.Start + island.Count; i++)
                        {
                            islandOf[i] = k;
                        }

                        text.AppendLine(template.Name + " | " + island.Tag + " | x " + (island.X) + ", y " + (size - island.Y - island.H) + ", w " + island.W + ", h " + island.H);
                    }

                    for (int i = 0; i + 2 < template.Triangles.Length; i += 3)
                    {
                        int ia = template.Triangles[i];
                        int ib = template.Triangles[i + 1];
                        int ic = template.Triangles[i + 2];
                        Fill(pixels, template.Uv[ia] * size, template.Uv[ib] * size, template.Uv[ic] * size, colors[islandOf[ia]]);
                    }

                    Color32 edge = new Color32(235, 235, 235, 255);
                    for (int i = 0; i + 2 < template.Triangles.Length; i += 3)
                    {
                        Vector2 a = template.Uv[template.Triangles[i]] * size;
                        Vector2 b = template.Uv[template.Triangles[i + 1]] * size;
                        Vector2 c = template.Uv[template.Triangles[i + 2]] * size;
                        Line(pixels, a, b, edge);
                        Line(pixels, b, c, edge);
                        Line(pixels, c, a, edge);
                    }
                }

                Texture2D image = new Texture2D(size, size, TextureFormat.RGBA32, false);
                image.SetPixels32(pixels);
                image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "VRS_" + group + "_template.png"), (byte[])encode.Invoke(null, new object[] { image }));
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "VRS_" + group + "_regions.txt"), text.ToString());
                Object.Destroy(image);

                Texture2D art = PieceArt.Generate(group);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "VRS_" + group + ".png"), (byte[])encode.Invoke(null, new object[] { art }));
                Object.Destroy(art);
            }

            Plugin.Log.LogInfo("Texture templates written to " + folder);
        }
    }
}

namespace VRSAutomation
{
    internal static class PieceArt
    {
        private static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), fx);
            float b = Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float Fractal(float x, float y)
        {
            return 0.5f * Noise(x, y) + 0.25f * Noise(x * 2f, y * 2f) + 0.125f * Noise(x * 4f, y * 4f) + 0.0625f * Noise(x * 8f, y * 8f);
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static readonly Color CopperDark = new Color(0.40f, 0.22f, 0.12f);
        private static readonly Color CopperMid = new Color(0.68f, 0.40f, 0.22f);
        private static readonly Color CopperLight = new Color(0.95f, 0.66f, 0.42f);
        private static readonly Color Patina = new Color(0.22f, 0.58f, 0.50f);
        private static readonly Color PatinaPale = new Color(0.45f, 0.75f, 0.66f);
        private static readonly Color Black = new Color(0.035f, 0.035f, 0.04f);

        private static Color Paint(string tag, float u, float v, float width, float height, int seed)
        {
            bool longU = width >= height;
            float along = longU ? u * width : v * height;
            float across = longU ? v * height : u * width;
            float acrossSize = Mathf.Max(8f, longU ? height : width);
            float alongSize = Mathf.Max(8f, longU ? width : height);
            float s = seed * 7.31f;

            if (tag.Contains("front panel"))
            {
                float grain = Noise(u * width * 0.4f + s, v * height * 0.4f);
                return Color.Lerp(Black, new Color(0.10f, 0.10f, 0.11f), grain * 0.5f);
            }

            float streak = Noise(across * 0.8f + s, along * 0.015f + s);
            float broad = Fractal(u * 3f + s, v * 3f + s * 1.7f);
            float sheen = 0.5f + 0.5f * Mathf.Sin(across / acrossSize * 3.4f + s);
            float tone = Mathf.Clamp01(0.30f * broad + 0.28f * streak + 0.42f * sheen);
            Color color = tone < 0.5f ? Color.Lerp(CopperDark, CopperMid, tone * 2f) : Color.Lerp(CopperMid, CopperLight, (tone - 0.5f) * 2f);
            float scratch = Noise(across * 0.15f + s * 3f, along * 0.9f);
            if (scratch > 0.82f)
            {
                color = Color.Lerp(color, CopperLight, 0.25f);
            }

            if (tag.Contains("arrow"))
            {
                float rust = Fractal(u * 6f + s, v * 6f + s);
                Color red = Color.Lerp(new Color(0.34f, 0.08f, 0.05f), new Color(0.62f, 0.20f, 0.10f), rust);
                float fleck = Smooth(0.62f, 0.8f, Fractal(u * 14f + s, v * 14f));
                return Color.Lerp(red, new Color(0.80f, 0.40f, 0.14f), fleck * 0.5f);
            }

            float edge = Mathf.Min(Mathf.Min(u, 1f - u) * width, Mathf.Min(v, 1f - v) * height);
            float bloom = Smooth(0.52f, 0.70f, Fractal(u * 5f + s * 2f, v * 5f + s));
            float crawl = 0.40f + 0.60f * (1f - Mathf.Clamp01(edge / 22f));
            float amount = bloom * crawl * (tag.Contains("bolt") ? 1.2f : 0.85f);
            if (amount > 0f)
            {
                Color patina = Color.Lerp(Patina, PatinaPale, Fractal(u * 9f + s, v * 9f + 3f));
                color = Color.Lerp(color, patina, Mathf.Clamp01(amount));
            }

            bool fitting = tag.Contains("port") || tag.Contains("collar") || tag.Contains("barrel") || tag.Contains("pipe");
            if (fitting)
            {
                float ends = Mathf.Max(Smooth(0.62f, 1f, along / alongSize), Smooth(0.62f, 1f, 1f - along / alongSize));
                float ring = Smooth(0.55f, 0.95f, Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
                float cover = Mathf.Abs(width - height) < Mathf.Max(width, height) * 0.25f ? ring : ends;
                float blotch = Smooth(0.45f, 0.70f, Fractal(u * 6f + s * 5f, v * 6f + s));
                float smudge = Mathf.Clamp01(blotch * (0.25f + 0.75f * cover) * 1.25f);
                color = Color.Lerp(color, Black, smudge * 0.8f);
            }

            return color;
        }

        internal static Texture2D Generate(string group)
        {
            int size = UvPack.Size;
            Color32[] pixels = new Color32[size * size];
            Color32 baseColor = CopperMid;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = baseColor;
            }

            System.Collections.Generic.List<UvPack.Island> islands = new System.Collections.Generic.List<UvPack.Island>();
            foreach (System.Collections.Generic.KeyValuePair<string, UvPack.Template> entry in UvPack.Templates)
            {
                if (entry.Key.EndsWith("#" + group))
                {
                    foreach (UvPack.Island island in entry.Value.Islands)
                    {
                        if (!islands.Contains(island))
                        {
                            islands.Add(island);
                        }
                    }
                }
            }

            int seed = 0;
            foreach (UvPack.Island island in islands)
            {
                seed++;
                string tag = island.Tag ?? string.Empty;
                for (int y = island.Y - UvPack.Pad; y < island.Y + island.H + UvPack.Pad; y++)
                {
                    if (y < 0 || y >= size)
                    {
                        continue;
                    }

                    for (int x = island.X - UvPack.Pad; x < island.X + island.W + UvPack.Pad; x++)
                    {
                        if (x < 0 || x >= size)
                        {
                            continue;
                        }

                        float u = Mathf.Clamp01((x - island.X + 0.5f) / island.W);
                        float v = Mathf.Clamp01((y - island.Y + 0.5f) / island.H);
                        pixels[y * size + x] = Paint(tag, u, v, island.W, island.H, seed);
                    }
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}

namespace VRSAutomation
{
    internal static class ConveyorSwitch
    {
        internal static readonly int Key = "vrs_conv_on".GetStableHashCode();
        internal static readonly int StatusKey = "vrs_conv_status".GetStableHashCode();
        internal static bool StartOff = true;

        internal const int StatusUnknown = 0;
        internal const int StatusMoving = 1;
        internal const int StatusSourceEmpty = 2;
        internal const int StatusNoMatch = 3;
        internal const int StatusDestinationFull = 4;
        internal const int StatusDestinationRejects = 5;
        internal const int StatusNoInputPipe = 6;
        internal const int StatusNoOutputPipe = 7;
        internal const int StatusNoBoxes = 8;

        internal static int StatusOf(ZDO zdo)
        {
            return zdo != null ? zdo.GetInt(StatusKey, StatusUnknown) : StatusUnknown;
        }

        internal static string StatusText(int status)
        {
            switch (status)
            {
                case StatusMoving:
                    return "Moving items";
                case StatusSourceEmpty:
                    return "Waiting: nothing to take from the source";
                case StatusNoMatch:
                    return "Waiting: no item matches the filter or its limits";
                case StatusDestinationFull:
                    return "Blocked: the destination is full";
                case StatusDestinationRejects:
                    return "Blocked: the destination does not accept these items";
                case StatusNoInputPipe:
                    return "No pipe on the input";
                case StatusNoOutputPipe:
                    return "No pipe on the output";
                case StatusNoBoxes:
                    return "Pipes do not reach a readable box";
                default:
                    return "Starting up";
            }
        }

        internal static bool IsOn(ZDO zdo)
        {
            return zdo == null || zdo.GetInt(Key, 1) != 0;
        }

        internal static bool IsOn(Piece piece)
        {
            ZNetView view = piece != null ? piece.GetComponent<ZNetView>() : null;
            return view == null || !view.IsValid() || IsOn(view.GetZDO());
        }

        internal static void ToggleCircuit(Piece start)
        {
            ZNetView startView = start != null ? start.GetComponent<ZNetView>() : null;
            if (startView == null || !startView.IsValid() || ZNetScene.instance == null || ZDOMan.instance == null)
            {
                return;
            }

            bool on = !IsOn(startView.GetZDO());
            int conveyorHash = Conveyor.PieceName.GetStableHashCode();
            int splitterHash = Conveyor.SplitterName.GetStableHashCode();
            int combinerHash = Conveyor.CombinerName.GetStableHashCode();

            ZDOID first = startView.GetZDO().m_uid;
            System.Collections.Generic.HashSet<ZDOID> visited = new System.Collections.Generic.HashSet<ZDOID> { first };
            System.Collections.Generic.Queue<ZDOID> queue = new System.Collections.Generic.Queue<ZDOID>();
            System.Collections.Generic.List<ZDOID> conveyors = new System.Collections.Generic.List<ZDOID> { first };
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                ZDOID node = queue.Dequeue();
                foreach (Pipe pipe in PipeTool.Pipes)
                {
                    ZDOID other;
                    if (pipe.OutId.Equals(node))
                    {
                        other = pipe.InId;
                    }
                    else if (pipe.InId.Equals(node))
                    {
                        other = pipe.OutId;
                    }
                    else
                    {
                        continue;
                    }

                    if (!visited.Add(other))
                    {
                        continue;
                    }

                    ZDO zdo = ZDOMan.instance.GetZDO(other);
                    int prefab = zdo != null ? zdo.GetPrefab() : 0;
                    if (prefab == conveyorHash)
                    {
                        conveyors.Add(other);
                        queue.Enqueue(other);
                    }
                    else if (prefab == splitterHash || prefab == combinerHash)
                    {
                        queue.Enqueue(other);
                    }
                }
            }

            int changed = 0;
            foreach (ZDOID id in conveyors)
            {
                GameObject go = ZNetScene.instance.FindInstance(id);
                ZNetView view = go != null ? go.GetComponent<ZNetView>() : null;
                if (view == null || !view.IsValid())
                {
                    continue;
                }

                view.ClaimOwnership();
                view.GetZDO().Set(Key, on ? 1 : 0);
                changed++;
            }

            if (Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, changed + (changed == 1 ? " conveyor turned " : " conveyors turned ") + (on ? "on" : "off"));
            }
        }

        internal static void Toggle(Piece piece)
        {
            ZNetView view = piece != null ? piece.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid())
            {
                return;
            }

            bool on = !IsOn(view.GetZDO());
            view.ClaimOwnership();
            view.GetZDO().Set(Key, on ? 1 : 0);
            if (Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, on ? "Conveyor turned on" : "Conveyor turned off");
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    internal static class ConveyorStartStatePatch
    {
        private static void Postfix(Piece __instance)
        {
            if (!ConveyorSwitch.StartOff || !__instance.name.StartsWith(Conveyor.PieceName))
            {
                return;
            }

            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner())
            {
                view.GetZDO().Set(ConveyorSwitch.Key, 0);
            }
        }
    }

    internal static class ConveyorIcons
    {
        private static Sprite gear;
        private static Sprite power;

        internal static Sprite Gear
        {
            get { return gear ?? (gear = Build(false)); }
        }

        internal static Sprite Power
        {
            get { return power ?? (power = Build(true)); }
        }

        private static Sprite Build(bool powerSymbol)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    bool on;
                    if (powerSymbol)
                    {
                        float fromTop = Mathf.Abs(Mathf.DeltaAngle(angle, 90f));
                        bool ring = r >= 36f && r <= 50f && fromTop > 38f;
                        bool bar = Mathf.Abs(dx) <= 6f && dy >= 6f && dy <= 54f;
                        on = ring || bar;
                    }
                    else
                    {
                        float step = Mathf.Abs(((angle % 45f) + 45f) % 45f - 22.5f);
                        bool teeth = r > 38f && r <= 54f && step > 11f;
                        bool body = r <= 42f && r >= 16f;
                        on = teeth || body;
                    }

                    pixels[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    internal static class ConveyorMenu
    {
        private const float HoldSeconds = 0.35f;

        private static Piece pending;
        private static float started;
        private static bool native;
        private static float nativeStarted;

        internal static bool Open;

        internal static bool Pending
        {
            get { return pending != null; }
        }

        internal static bool Start(Piece piece, Humanoid user)
        {
            if (pending != null || piece == null || user == null || user != Player.m_localPlayer || MachineMenu.Busy)
            {
                return false;
            }

            pending = piece;
            started = Time.time;
            native = false;
            Open = false;
            return true;
        }

        private static void Reset()
        {
            pending = null;
            native = false;
            Open = false;
        }

        private static bool Held()
        {
            return ZInput.GetButton("Use") || ZInput.GetButton("JoyUse");
        }

        private static bool TryNative(Piece piece)
        {
            bool on = ConveyorSwitch.IsOn(piece);
            System.Collections.Generic.List<RadialItem> items = new System.Collections.Generic.List<RadialItem>();
            items.Add(new RadialItem
            {
                Name = on ? "Turn off" : "Turn on",
                Description = on ? "Stop this conveyor moving items" : "Start this conveyor moving items",
                Icon = ConveyorIcons.Power,
                Tint = on ? new Color(0.9f, 0.25f, 0.2f) : new Color(0.3f, 0.9f, 0.4f),
                Action = delegate
                {
                    ConveyorSwitch.Toggle(piece);
                    return true;
                }
            });
            items.Add(new RadialItem
            {
                Name = "Change item filters",
                Description = "Specify which items can pass through this conveyor",
                Icon = ConveyorIcons.Gear,
                GoldIcon = true,
                Action = delegate
                {
                    ConveyorFilterWindow.Open(piece);
                    return true;
                }
            });
            return NativeRadial.Open("Conveyor", ConveyorIcons.Gear, items, Held);
        }

        internal static void Tick()
        {
            if (pending == null)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null || Vector3.Distance(player.transform.position, pending.transform.position) > 6f)
            {
                Reset();
                return;
            }

            if (native)
            {
                if (!NativeRadial.Active && Time.time - nativeStarted > 0.25f)
                {
                    Reset();
                }

                return;
            }

            if (Held())
            {
                if (Time.time - started >= HoldSeconds)
                {
                    if (TryNative(pending))
                    {
                        native = true;
                        Open = true;
                        nativeStarted = Time.time;
                        return;
                    }

                    Piece target = pending;
                    Reset();
                    ConveyorFilterWindow.Open(target);
                }

                return;
            }

            Piece piece = pending;
            Reset();
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift))
            {
                ConveyorSwitch.ToggleCircuit(piece);
            }
            else
            {
                ConveyorSwitch.Toggle(piece);
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Player), "Update")]
    internal static class ConveyorMenuTickPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                ConveyorMenu.Tick();
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class ConveyorMenuCursorPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (ConveyorMenu.Open)
            {
                __result = true;
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class ConveyorMenuInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (ConveyorMenu.Open)
            {
                __result = false;
            }
        }
    }

    internal class ConveyorLight : MonoBehaviour
    {
        private static readonly Color OnColor = new Color(0.25f, 1f, 0.35f, 1f);
        private static readonly Color OffColor = new Color(1f, 0.15f, 0.1f, 1f);
        private static readonly Color WaitColor = new Color(1f, 0.7f, 0.1f, 1f);

        private ZNetView view;
        private Material material;
        private int state = -1;
        private float next;

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            RustRing.Prebuild();
            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Cube);
            light.name = "VRS_Light";
            Collider collider = light.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            light.layer = gameObject.layer;
            light.transform.SetParent(transform, false);
            light.transform.localPosition = new Vector3(-0.045f, 0.2445f, -0.105f);
            light.transform.localScale = new Vector3(0.026f, 0.008f, 0.026f);
            Shader shader = Shader.Find("Sprites/Default");
            Renderer renderer = light.GetComponent<Renderer>();
            if (shader != null)
            {
                material = new Material(shader);
                renderer.sharedMaterial = material;
            }
        }

        private void Update()
        {
            if (material == null || Time.time < next)
            {
                return;
            }

            next = Time.time + 0.25f;
            bool valid = view != null && view.IsValid();
            int now;
            if (!valid)
            {
                now = ConveyorSwitch.StartOff ? 0 : 1;
            }
            else if (!ConveyorSwitch.IsOn(view.GetZDO()))
            {
                now = 0;
            }
            else
            {
                now = ConveyorSwitch.StatusOf(view.GetZDO()) == ConveyorSwitch.StatusMoving ? 1 : 2;
            }

            if (now != state)
            {
                state = now;
                material.color = now == 0 ? OffColor : (now == 1 ? OnColor : WaitColor);
            }
        }
    }
}

namespace VRSAutomation
{
    internal static class RustRing
    {
        private const int TextureSize = 768;
        private const float OuterRadius = 380f;
        private const float InnerRadius = 262f;
        private const float DiscRadius = 250f;
        private const float DeadZone = 40f;
        private const float EdgeWidth = 3.5f;

        internal static readonly Color Glass = new Color(0.09f, 0.09f, 0.09f, 0.55f);
        internal static readonly Color GlassHover = new Color(0.82f, 0.57f, 0.22f, 0.55f);
        internal static readonly Color Edge = new Color(0.85f, 0.65f, 0.27f, 0.85f);
        internal static readonly Color EdgeHover = new Color(1f, 0.82f, 0.35f, 1f);
        internal static readonly Color DiscFill = new Color(0.08f, 0.08f, 0.08f, 0.6f);
        internal static readonly Color DiscEdge = new Color(0.85f, 0.65f, 0.27f, 0.6f);
        internal static readonly Color Gold = new Color(0.95f, 0.68f, 0.28f, 1f);
        internal static readonly Color GoldHover = new Color(1f, 0.92f, 0.65f, 1f);
        internal static readonly Color Cream = new Color(0.96f, 0.93f, 0.88f, 1f);

        private sealed class Wedge
        {
            public Sprite Fill;
            public Sprite Edge;
        }

        private static readonly System.Collections.Generic.Dictionary<int, Wedge> wedges = new System.Collections.Generic.Dictionary<int, Wedge>();
        private static Sprite discFill;
        private static Sprite discEdge;
        private static GameObject root;
        private static readonly System.Collections.Generic.List<UnityEngine.UI.Graphic> hidden = new System.Collections.Generic.List<UnityEngine.UI.Graphic>();

        internal static void Prebuild()
        {
            GetWedge(2);
            GetWedge(8);
            EnsureDisc();
        }

        internal static void Restore()
        {
            foreach (UnityEngine.UI.Graphic graphic in hidden)
            {
                if (graphic != null)
                {
                    graphic.enabled = true;
                }
            }

            hidden.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }

            root = null;
        }

        private static float Soft(float value)
        {
            return Mathf.Clamp01(value + 0.5f);
        }

        private static Sprite Make(System.Func<float, float, float> alphaAt)
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            float c = (TextureSize - 1) * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float alpha = alphaAt(x - c, y - c);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 1f);
        }

        private static float WedgeInside(float dx, float dy, float half)
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float theta = Mathf.Abs(Mathf.DeltaAngle(0f, Mathf.Atan2(dx, dy) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float side = r * Mathf.Sin(half - theta);
            return Mathf.Min(Mathf.Min(r - InnerRadius, OuterRadius - r), side - 3f);
        }

        private static Wedge GetWedge(int count)
        {
            Wedge wedge;
            if (wedges.TryGetValue(count, out wedge))
            {
                return wedge;
            }

            float half = Mathf.PI / count;
            wedge = new Wedge
            {
                Fill = Make((dx, dy) => Soft(WedgeInside(dx, dy, half))),
                Edge = Make((dx, dy) => Soft(WedgeInside(dx, dy, half)) - Soft(WedgeInside(dx, dy, half) - EdgeWidth))
            };
            wedges[count] = wedge;
            return wedge;
        }

        private static void EnsureDisc()
        {
            if (discFill != null)
            {
                return;
            }

            discFill = Make((dx, dy) => Soft(DiscRadius - Mathf.Sqrt(dx * dx + dy * dy)));
            discEdge = Make((dx, dy) =>
            {
                float d = DiscRadius - Mathf.Sqrt(dx * dx + dy * dy);
                return Soft(d) - Soft(d - EdgeWidth);
            });
        }

        internal static UnityEngine.UI.Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.enabled = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        private static TMPro.TextMeshProUGUI MakeText(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            TMPro.TextMeshProUGUI text = go.AddComponent<TMPro.TextMeshProUGUI>();
            TMPro.TMP_Text reference = InventoryGui.instance != null ? InventoryGui.instance.m_containerName : null;
            if (reference != null)
            {
                text.font = reference.font;
                text.fontSharedMaterial = reference.fontSharedMaterial;
            }

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            go.SetActive(true);
            return text;
        }

        internal static float BaseAngle(int count)
        {
            return count == 2 ? 45f : 0f;
        }

        internal static void Attach(Valheim.UI.RadialBase radial, System.Collections.Generic.List<RadialItem> items, string title, Sprite titleIcon)
        {
            int count = items != null ? items.Count : 0;
            if (count < 2 || count > 12)
            {
                return;
            }

            try
            {
                Restore();
                Wedge wedge = GetWedge(count);
                EnsureDisc();

                Transform parent = radial.m_elementContainer != null ? (Transform)radial.m_elementContainer : radial.transform;
                root = new GameObject("VRS_Ring", typeof(RectTransform));
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.SetParent(parent, false);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = Vector2.zero;
                rootRect.SetAsLastSibling();

                RustRingDriver driver = root.AddComponent<RustRingDriver>();
                driver.Radial = radial;
                driver.Items = items;
                driver.Title = title;
                driver.TitleIcon = titleIcon;
                float step = 360f / count;
                float start = BaseAngle(count);
                Vector2 full = new Vector2(TextureSize, TextureSize);
                float mid = (OuterRadius + InnerRadius) * 0.5f;
                float iconSize = count == 2 ? 104f : 88f;

                for (int k = 0; k < count; k++)
                {
                    float angle = start + k * step;
                    Quaternion rotation = Quaternion.Euler(0f, 0f, -angle);
                    UnityEngine.UI.Image fill = MakeImage("VRS_Fill" + k, root.transform, wedge.Fill, full, Vector2.zero, Glass);
                    fill.rectTransform.localRotation = rotation;
                    UnityEngine.UI.Image edge = MakeImage("VRS_Edge" + k, root.transform, wedge.Edge, full, Vector2.zero, Edge);
                    edge.rectTransform.localRotation = rotation;
                    driver.Fills.Add(fill);
                    driver.Edges.Add(edge);
                }

                MakeImage("VRS_DiscFill", root.transform, discFill, full, Vector2.zero, DiscFill);
                MakeImage("VRS_DiscEdge", root.transform, discEdge, full, Vector2.zero, DiscEdge);

                for (int k = 0; k < count; k++)
                {
                    float radians = (start + k * step) * Mathf.Deg2Rad;
                    Vector2 position = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * mid;
                    driver.Icons.Add(MakeImage("VRS_Icon" + k, root.transform, items[k].Icon, new Vector2(iconSize, iconSize), position, RustRingDriver.IconColor(items[k], false)));
                }

                driver.CenterIcon = MakeImage("VRS_CenterIcon", root.transform, titleIcon, new Vector2(150f, 150f), new Vector2(0f, 75f), Cream);
                driver.NameText = MakeText("VRS_Name", root.transform, new Vector2(0f, -45f), new Vector2(440f, 60f), 44f, Cream);
                driver.DescriptionText = MakeText("VRS_Description", root.transform, new Vector2(0f, -125f), new Vector2(400f, 100f), 28f, new Color(0.82f, 0.8f, 0.76f, 1f));

                radial.GetMouseDirection = () =>
                {
                    int index = HoverIndex(count);
                    if (index < 0)
                    {
                        return Vector2.zero;
                    }

                    Valheim.UI.RadialMenuElement element = FindElement(radial, items[index].Name);
                    if (element == null)
                    {
                        float radiansAt = (start + index * step) * Mathf.Deg2Rad;
                        return new Vector2(Mathf.Sin(radiansAt), Mathf.Cos(radiansAt));
                    }

                    Vector2 direction = ((Vector3)element.LocalPosition);
                    return direction.sqrMagnitude < 0.0001f ? Vector2.up : direction.normalized;
                };

                driver.Collect();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Ring menu could not be drawn, using the game's look: " + e.Message);
                Restore();
            }
        }

        private static Valheim.UI.RadialMenuElement FindElement(Valheim.UI.RadialBase radial, string name)
        {
            if (radial.m_elements == null)
            {
                return null;
            }

            for (int i = 0; i < radial.m_elements.Count; i++)
            {
                Valheim.UI.EmoteElement emote = radial.m_elements.GetElement(i) as Valheim.UI.EmoteElement;
                if (emote != null && emote.Name == name)
                {
                    return emote;
                }
            }

            return null;
        }

        internal static int HoverIndex(int count)
        {
            Vector2 offset = (Vector2)UnityEngine.Input.mousePosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (offset.magnitude < DeadZone)
            {
                return -1;
            }

            float step = 360f / count;
            float angle = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
            float relative = Mathf.Repeat(angle - BaseAngle(count) + step * 0.5f, 360f);
            return Mathf.FloorToInt(relative / step) % count;
        }

        internal static void Hide(Valheim.UI.RadialBase radial)
        {
            foreach (UnityEngine.UI.Graphic graphic in radial.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (graphic != null && graphic.enabled && root != null && !graphic.transform.IsChildOf(root.transform))
                {
                    graphic.enabled = false;
                    hidden.Add(graphic);
                }
            }
        }
    }

    internal class RustRingDriver : MonoBehaviour
    {
        internal Valheim.UI.RadialBase Radial;
        internal System.Collections.Generic.List<RadialItem> Items;
        internal string Title;
        internal Sprite TitleIcon;
        internal System.Collections.Generic.List<UnityEngine.UI.Image> Fills = new System.Collections.Generic.List<UnityEngine.UI.Image>();
        internal System.Collections.Generic.List<UnityEngine.UI.Image> Edges = new System.Collections.Generic.List<UnityEngine.UI.Image>();
        internal System.Collections.Generic.List<UnityEngine.UI.Image> Icons = new System.Collections.Generic.List<UnityEngine.UI.Image>();
        internal UnityEngine.UI.Image CenterIcon;
        internal TMPro.TextMeshProUGUI NameText;
        internal TMPro.TextMeshProUGUI DescriptionText;

        private float born;
        private float inactiveSince = -1f;
        private int frame;

        internal static Color IconColor(RadialItem item, bool hover)
        {
            if (item.GoldIcon)
            {
                return hover ? RustRing.GoldHover : RustRing.Gold;
            }

            return item.Tint;
        }

        private void Awake()
        {
            born = Time.time;
        }

        private void OnDisable()
        {
            if (Time.time - born > 0.1f)
            {
                RustRing.Restore();
            }
        }

        internal void Collect()
        {
            if (Radial != null)
            {
                RustRing.Hide(Radial);
            }
        }

        private void LateUpdate()
        {
            if (Radial != null && ++frame % 10 == 0)
            {
                RustRing.Hide(Radial);
            }

            if (NativeRadial.Active)
            {
                inactiveSince = -1f;
            }
            else if (inactiveSince < 0f)
            {
                inactiveSince = Time.time;
            }
            else if (Time.time - inactiveSince > 1.5f && Time.time - born > 0.6f)
            {
                RustRing.Restore();
                return;
            }

            int count = Items.Count;
            int hover = RustRing.HoverIndex(count);
            for (int k = 0; k < count; k++)
            {
                bool on = k == hover;
                Fills[k].color = on ? RustRing.GlassHover : RustRing.Glass;
                Edges[k].color = on ? RustRing.EdgeHover : RustRing.Edge;
                Icons[k].color = IconColor(Items[k], on);
            }

            if (hover < 0)
            {
                CenterIcon.sprite = TitleIcon;
                CenterIcon.enabled = TitleIcon != null;
                CenterIcon.color = new Color(1f, 1f, 1f, 0.25f);
                NameText.text = Title;
                DescriptionText.text = "Move the mouse to choose";
                return;
            }

            RadialItem item = Items[hover];
            CenterIcon.sprite = item.Icon;
            CenterIcon.enabled = item.Icon != null;
            CenterIcon.color = IconColor(item, false);
            NameText.text = item.Name;
            DescriptionText.text = item.Description;
        }
    }
}

namespace VRSAutomation
{
    using System.Collections.Generic;

    internal class AdapterScreen : MonoBehaviour
    {
        internal static float CycleSeconds = 2.5f;
        internal static bool Enabled = true;
        private const float RefreshSeconds = 0.5f;
        private const float ShowDistance = 12f;
        private const int MaxKinds = 8;
        private const float ScreenY = 0.195f;
        private const float BarWidth = 0.15f;
        private const float BarY = -0.074f;

        private sealed class Entry
        {
            public string Key;
            public Sprite Icon;
            public int Count;
        }

        private static readonly Color TextColor = new Color(0.65f, 1f, 0.7f, 1f);
        private static readonly Color DimColor = new Color(0.4f, 0.55f, 0.42f, 1f);
        private static readonly Color BarGreen = new Color(0.3f, 0.85f, 0.35f, 1f);
        private static readonly Color BarAmber = new Color(1f, 0.7f, 0.1f, 1f);
        private static readonly Color BarRed = new Color(1f, 0.2f, 0.12f, 1f);

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Material> materials = new List<Material>();
        private ZNetView view;
        private Container container;
        private float nextResolve;
        private float nextRefresh;
        private bool built;
        private Transform pivot;
        private Transform face;
        private Material iconMaterial;
        private Mesh iconMesh;
        private GameObject iconObject;
        private TMPro.TextMeshPro text;
        private Transform barFill;
        private Material barMaterial;
        private Transform[] dots;
        private Material[] dotMaterials;
        private int used;
        private int capacity;
        private int signature = int.MinValue;
        private int shownIndex = -2;
        private int shownSignature = int.MinValue;
        private float yaw = -1f;

        private void Awake()
        {
            view = GetComponent<ZNetView>();
        }

        private void OnDestroy()
        {
            foreach (Material material in materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            if (iconMesh != null)
            {
                Destroy(iconMesh);
            }
        }

        private void Update()
        {
            if (view == null || !view.IsValid())
            {
                return;
            }

            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + RefreshSeconds;
                Refresh();
            }

            if (!built || face == null || !face.gameObject.activeSelf)
            {
                return;
            }

            int index = entries.Count == 0 ? -1 : (int)(Time.time / CycleSeconds) % entries.Count;
            if (index != shownIndex || signature != shownSignature)
            {
                shownIndex = index;
                shownSignature = signature;
                Draw(index);
            }
        }

        private void Refresh()
        {
            Camera cam = Camera.main;
            bool near = Enabled && cam != null && (cam.transform.position - transform.position).sqrMagnitude < ShowDistance * ShowDistance;
            if (!near)
            {
                if (face != null && face.gameObject.activeSelf)
                {
                    face.gameObject.SetActive(false);
                }

                return;
            }

            if (!built && !Build())
            {
                return;
            }

            if (!face.gameObject.activeSelf)
            {
                face.gameObject.SetActive(true);
                shownIndex = -2;
            }

            float wanted = PickYaw();
            if (!Mathf.Approximately(wanted, yaw))
            {
                yaw = wanted;
                pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }

            if (container == null && Time.time >= nextResolve)
            {
                nextResolve = Time.time + 5f;
                container = FindContainer();
            }

            if (container == null)
            {
                return;
            }

            Collect();
        }

        private Container FindContainer()
        {
            ZDO host = AdapterLink.Host(view.GetZDO());
            if (host == null || ZNetScene.instance == null)
            {
                return null;
            }

            ZNetView instance = ZNetScene.instance.FindInstance(host);
            if (instance == null)
            {
                return null;
            }

            Container found = instance.GetComponent<Container>();
            return found != null ? found : instance.GetComponentInChildren<Container>();
        }

        private void Collect()
        {
            Inventory inventory = container.GetInventory();
            if (inventory == null)
            {
                return;
            }

            entries.Clear();
            List<ItemDrop.ItemData> all = inventory.GetAllItems();
            used = all.Count;
            capacity = Mathf.Max(1, inventory.GetWidth() * inventory.GetHeight());
            foreach (ItemDrop.ItemData item in all)
            {
                if (item == null || item.m_shared == null)
                {
                    continue;
                }

                Entry entry = null;
                foreach (Entry candidate in entries)
                {
                    if (candidate.Key == item.m_shared.m_name)
                    {
                        entry = candidate;
                        break;
                    }
                }

                if (entry == null)
                {
                    entry = new Entry();
                    entry.Key = item.m_shared.m_name;
                    entry.Icon = item.GetIcon();
                    entries.Add(entry);
                }

                entry.Count += item.m_stack;
            }

            entries.Sort((a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : string.CompareOrdinal(a.Key, b.Key));
            if (entries.Count > MaxKinds)
            {
                entries.RemoveRange(MaxKinds, entries.Count - MaxKinds);
            }

            int hash = used * 31 + capacity;
            foreach (Entry entry in entries)
            {
                hash = hash * 31 + entry.Key.GetHashCode();
                hash = hash * 31 + entry.Count;
            }

            signature = hash;
        }

        private float PickYaw()
        {
            if (Mathf.Abs(transform.up.y) >= 0.8f)
            {
                return 0f;
            }

            float best = 0f;
            float bestDot = -2f;
            for (int i = 0; i < 4; i++)
            {
                float candidate = i * 90f;
                Vector3 up = transform.TransformDirection(Quaternion.Euler(0f, candidate, 0f) * Vector3.forward);
                float dot = Vector3.Dot(up, Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = candidate;
                }
            }

            return best;
        }

        private bool Build()
        {
            TMPro.TMP_Text reference = InventoryGui.instance != null ? InventoryGui.instance.m_containerName : null;
            Shader shader = Shader.Find("Sprites/Default");
            if (reference == null || shader == null)
            {
                return false;
            }

            GameObject pivotObject = new GameObject("VRS_ScreenPivot");
            pivotObject.layer = gameObject.layer;
            pivot = pivotObject.transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0f, ScreenY, 0f);

            GameObject faceObject = new GameObject("VRS_ScreenFace");
            faceObject.layer = gameObject.layer;
            face = faceObject.transform;
            face.SetParent(pivot, false);
            face.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Quad("border", shader, new Vector2(0f, 0f), new Vector2(0.18f, 0.18f), new Color(0.55f, 0.4f, 0.15f, 1f), -0.0002f, 3000);
            Quad("glass", shader, new Vector2(0f, 0f), new Vector2(0.17f, 0.17f), new Color(0.03f, 0.05f, 0.04f, 1f), -0.0004f, 3001);
            Quad("bar back", shader, new Vector2(0f, BarY), new Vector2(BarWidth, 0.012f), new Color(0.12f, 0.14f, 0.12f, 1f), -0.0006f, 3002);

            GameObject fill = Quad("bar fill", shader, new Vector2(0f, BarY), new Vector2(BarWidth, 0.012f), BarGreen, -0.0008f, 3003);
            barFill = fill.transform;
            barMaterial = fill.GetComponent<Renderer>().sharedMaterial;

            dots = new Transform[MaxKinds];
            dotMaterials = new Material[MaxKinds];
            for (int i = 0; i < MaxKinds; i++)
            {
                GameObject dot = Quad("dot " + i, shader, new Vector2(0f, 0.07f), new Vector2(0.01f, 0.01f), DimColor, -0.001f, 3004);
                dots[i] = dot.transform;
                dotMaterials[i] = dot.GetComponent<Renderer>().sharedMaterial;
                dot.SetActive(false);
            }

            iconObject = Quad("icon", shader, new Vector2(0f, 0.032f), new Vector2(0.08f, 0.08f), new Color(0.85f, 1f, 0.88f, 1f), -0.0012f, 3005);
            iconMaterial = iconObject.GetComponent<Renderer>().sharedMaterial;
            MeshFilter iconFilter = iconObject.GetComponent<MeshFilter>();
            iconMesh = Instantiate(iconFilter.sharedMesh);
            iconFilter.sharedMesh = iconMesh;
            iconObject.SetActive(false);

            GameObject textObject = new GameObject("VRS_ScreenText");
            textObject.SetActive(false);
            textObject.layer = gameObject.layer;
            textObject.transform.SetParent(face, false);
            textObject.transform.localPosition = new Vector3(0f, -0.034f, -0.0014f);
            text = textObject.AddComponent<TMPro.TextMeshPro>();
            text.font = reference.font;
            text.fontSize = 36f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta = new Vector2(10f, 2f);
            text.color = TextColor;
            text.text = "";
            textObject.SetActive(true);
            Material textMaterial = text.fontMaterial;
            if (textMaterial != null)
            {
                textMaterial.renderQueue = 3100;
                materials.Add(textMaterial);
            }

            built = true;
            return true;
        }

        private GameObject Quad(string name, Shader shader, Vector2 position, Vector2 size, Color color, float z, int queue)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            quad.layer = gameObject.layer;
            quad.transform.SetParent(face, false);
            quad.transform.localPosition = new Vector3(position.x, position.y, z);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            Renderer renderer = quad.GetComponent<Renderer>();
            Material material = new Material(shader);
            material.color = color;
            material.renderQueue = queue;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            materials.Add(material);
            return quad;
        }

        private void Draw(int index)
        {
            float ratio = Mathf.Clamp01((float)used / Mathf.Max(1, capacity));
            float width = Mathf.Max(0.002f, BarWidth * ratio);
            barFill.localScale = new Vector3(width, 0.012f, 1f);
            barFill.localPosition = new Vector3(-BarWidth * 0.5f + width * 0.5f, BarY, -0.0008f);
            barMaterial.color = used >= capacity ? BarRed : (ratio >= 0.85f ? BarAmber : BarGreen);

            int kinds = entries.Count;
            for (int i = 0; i < MaxKinds; i++)
            {
                bool on = kinds > 1 && i < kinds;
                dots[i].gameObject.SetActive(on);
                if (on)
                {
                    float x = (i - (kinds - 1) * 0.5f) * 0.018f;
                    dots[i].localPosition = new Vector3(x, 0.07f, -0.001f);
                    dotMaterials[i].color = i == index ? TextColor : new Color(0.2f, 0.28f, 0.2f, 1f);
                }
            }

            if (index < 0)
            {
                iconObject.SetActive(false);
                SetText("EMPTY", DimColor, 0.05f);
                return;
            }

            Entry entry = entries[index];
            Sprite sprite = entry.Icon;
            if (sprite != null && sprite.texture != null)
            {
                Texture2D texture = sprite.texture;
                Rect rect = sprite.textureRect;
                iconMaterial.mainTexture = texture;
                float u0 = rect.x / texture.width;
                float v0 = rect.y / texture.height;
                float u1 = (rect.x + rect.width) / texture.width;
                float v1 = (rect.y + rect.height) / texture.height;
                iconMesh.uv = new Vector2[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u0, v1), new Vector2(u1, v1) };
                iconObject.SetActive(true);
            }
            else
            {
                iconObject.SetActive(false);
            }

            SetText(entry.Count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), TextColor, 0.08f);
        }

        private void SetText(string value, Color color, float height)
        {
            text.text = value;
            text.color = color;
            Vector2 size = text.GetPreferredValues(value);
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(0.165f / size.x, height / size.y);
            text.transform.localScale = new Vector3(scale, scale, scale);
        }
    }
}

namespace VRSAutomation
{
    using System.Collections.Generic;

    internal static class RailSnap
    {
        internal static bool Enabled = true;
        internal static float MinGap = 0.03f;

        private const float SideMargin = 0.06f;
        private const float PortZone = 0.2245f;
        private const float Capture = 0.15f;
        private const float SoftCapture = 0.04f;
        private const float SoftRelease = 0.06f;
        private const float Reach = 8f;
        private const float OnRail = 0.02f;

        private struct Rail
        {
            public Vector3 Origin;
            public Vector3 Dir;
            public Vector3 Perp;
            public Piece Owner;
        }

        private static bool lockActive;
        private static Vector3 lockOrigin;
        private static Vector3 lockDir;
        private static float lockT;

        private static bool ShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool IsRailPiece(string name)
        {
            return name.StartsWith(Conveyor.PieceName) || Conveyor.IsJunction(name);
        }

        internal static Vector3 Apply(Piece prefab, GameObject ghost, Vector3 point, Vector3 normal, Quaternion rotation)
        {
            if (!Enabled || !ShiftHeld())
            {
                lockActive = false;
                return point;
            }

            BoxCollider ghostBox = prefab.GetComponent<BoxCollider>();
            if (ghostBox == null)
            {
                return point;
            }

            List<Piece> neighbors = Neighbors(ghost, point, normal);
            if (neighbors.Count == 0)
            {
                lockActive = false;
                return point;
            }

            List<Rail> rails = new List<Rail>();
            foreach (Piece neighbor in neighbors)
            {
                AddRails(rails, neighbor, normal);
            }

            int best = -1;
            float bestDistance = Capture;
            for (int i = 0; i < rails.Count; i++)
            {
                float distance = Mathf.Abs(Vector3.Dot(point - rails[i].Origin, rails[i].Perp));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            if (best < 0)
            {
                lockActive = false;
                return point;
            }

            int second = -1;
            float secondDistance = Capture;
            for (int i = 0; i < rails.Count; i++)
            {
                if (i == best || rails[i].Owner == rails[best].Owner || Mathf.Abs(Vector3.Dot(rails[i].Dir, rails[best].Dir)) > 0.9f)
                {
                    continue;
                }

                float distance = Mathf.Abs(Vector3.Dot(point - rails[i].Origin, rails[i].Perp));
                if (distance < secondDistance)
                {
                    secondDistance = distance;
                    second = i;
                }
            }

            if (second >= 0)
            {
                Vector3 crossing;
                if (Intersect(rails[best], rails[second], out crossing)
                    && Clear(crossing, rails[best], neighbors, ghostBox, rotation)
                    && Clear(crossing, rails[second], neighbors, ghostBox, rotation))
                {
                    lockActive = false;
                    return crossing;
                }
            }

            return Slide(rails[best], point, neighbors, ghostBox, rotation);
        }

        private static List<Piece> Neighbors(GameObject ghost, Vector3 point, Vector3 normal)
        {
            List<Piece> result = new List<Piece>();
            foreach (Piece piece in Piece.s_allPieces)
            {
                if (piece == null || piece.gameObject == ghost || !IsRailPiece(piece.gameObject.name))
                {
                    continue;
                }

                ZNetView view = piece.GetComponent<ZNetView>();
                if (view == null || !view.IsValid())
                {
                    continue;
                }

                Vector3 offset = piece.transform.position - point;
                if (offset.sqrMagnitude > Reach * Reach
                    || Vector3.Dot(piece.transform.up, normal) < 0.98f
                    || Mathf.Abs(Vector3.Dot(offset, normal)) > 0.12f)
                {
                    continue;
                }

                result.Add(piece);
            }

            return result;
        }

        private static void AddRails(List<Rail> rails, Piece piece, Vector3 normal)
        {
            Vector3 first = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (first.magnitude < 0.5f)
            {
                first = Vector3.ProjectOnPlane(piece.transform.forward, normal);
            }

            if (first.sqrMagnitude < 0.0001f)
            {
                return;
            }

            first.Normalize();
            Vector3 second = Vector3.Cross(normal, first).normalized;
            Vector3 origin = piece.transform.position;
            rails.Add(new Rail { Origin = origin, Dir = first, Perp = second, Owner = piece });
            rails.Add(new Rail { Origin = origin, Dir = second, Perp = first, Owner = piece });
        }

        private static bool Intersect(Rail a, Rail b, out Vector3 point)
        {
            point = Vector3.zero;
            float denominator = Vector3.Dot(a.Dir, b.Perp);
            if (Mathf.Abs(denominator) < 0.0001f)
            {
                return false;
            }

            float t = Vector3.Dot(b.Origin - a.Origin, b.Perp) / denominator;
            point = a.Origin + a.Dir * t;
            return true;
        }

        private static void Extent(Quaternion rotation, BoxCollider box, Vector3 dir, out float min, out float max)
        {
            float offset = Vector3.Dot(rotation * box.center, dir);
            float half = Mathf.Abs(Vector3.Dot(rotation * Vector3.right, dir)) * box.size.x * 0.5f
                + Mathf.Abs(Vector3.Dot(rotation * Vector3.up, dir)) * box.size.y * 0.5f
                + Mathf.Abs(Vector3.Dot(rotation * Vector3.forward, dir)) * box.size.z * 0.5f
                + SideMargin;
            min = offset - half;
            max = offset + half;

            float reach = box.size.z * 0.5f + PortZone;
            float front = Vector3.Dot(rotation * (box.center + new Vector3(0f, 0f, reach)), dir);
            float back = Vector3.Dot(rotation * (box.center - new Vector3(0f, 0f, reach)), dir);
            min = Mathf.Min(min, Mathf.Min(front, back));
            max = Mathf.Max(max, Mathf.Max(front, back));
        }

        private static bool OnThisRail(Rail rail, Piece piece)
        {
            return Mathf.Abs(Vector3.Dot(piece.transform.position - rail.Origin, rail.Perp)) < OnRail;
        }

        private static void Spans(Rail rail, List<Piece> neighbors, BoxCollider ghostBox, Quaternion rotation, List<float> lows, List<float> highs, List<float> centers)
        {
            float ghostMin;
            float ghostMax;
            Extent(rotation, ghostBox, rail.Dir, out ghostMin, out ghostMax);
            foreach (Piece neighbor in neighbors)
            {
                if (!OnThisRail(rail, neighbor))
                {
                    continue;
                }

                BoxCollider box = neighbor.GetComponent<BoxCollider>();
                float s = Vector3.Dot(neighbor.transform.position - rail.Origin, rail.Dir);
                centers.Add(s);
                if (box == null)
                {
                    lows.Add(s - MinGap);
                    highs.Add(s + MinGap);
                    continue;
                }

                float nMin;
                float nMax;
                Extent(neighbor.transform.rotation, box, rail.Dir, out nMin, out nMax);
                lows.Add(s + nMin - ghostMax - MinGap);
                highs.Add(s + nMax - ghostMin + MinGap);
            }
        }

        private static bool Inside(List<float> lows, List<float> highs, float t)
        {
            for (int i = 0; i < lows.Count; i++)
            {
                if (t > lows[i] + 0.0005f && t < highs[i] - 0.0005f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Clear(Vector3 position, Rail rail, List<Piece> neighbors, BoxCollider ghostBox, Quaternion rotation)
        {
            List<float> lows = new List<float>();
            List<float> highs = new List<float>();
            List<float> centers = new List<float>();
            Spans(rail, neighbors, ghostBox, rotation, lows, highs, centers);
            return !Inside(lows, highs, Vector3.Dot(position - rail.Origin, rail.Dir));
        }

        private static Vector3 Slide(Rail rail, Vector3 point, List<Piece> neighbors, BoxCollider ghostBox, Quaternion rotation)
        {
            List<float> lows = new List<float>();
            List<float> highs = new List<float>();
            List<float> centers = new List<float>();
            Spans(rail, neighbors, ghostBox, rotation, lows, highs, centers);

            float t = Vector3.Dot(point - rail.Origin, rail.Dir);
            float chosen = t;
            bool sameRail = lockActive
                && Mathf.Abs(Vector3.Dot(lockOrigin - rail.Origin, rail.Perp)) < OnRail
                && Mathf.Abs(Vector3.Dot(lockDir, rail.Dir)) > 0.99f;
            if (sameRail && Mathf.Abs(t - lockT) < SoftRelease && !Inside(lows, highs, lockT))
            {
                chosen = lockT;
            }
            else
            {
                lockActive = false;
                List<float> spacings = new List<float>();
                List<float> sorted = new List<float>(centers);
                sorted.Sort();
                for (int i = 1; i < sorted.Count; i++)
                {
                    float gap = sorted[i] - sorted[i - 1];
                    if (gap > 0.01f && !spacings.Exists(s => Mathf.Abs(s - gap) < 0.005f))
                    {
                        spacings.Add(gap);
                    }
                }

                float bestDistance = SoftCapture;
                foreach (float center in centers)
                {
                    foreach (float spacing in spacings)
                    {
                        for (int sign = -1; sign <= 1; sign += 2)
                        {
                            float candidate = center + sign * spacing;
                            float distance = Mathf.Abs(candidate - t);
                            if (distance < bestDistance && !Inside(lows, highs, candidate))
                            {
                                bestDistance = distance;
                                chosen = candidate;
                                lockActive = true;
                                lockOrigin = rail.Origin;
                                lockDir = rail.Dir;
                                lockT = candidate;
                            }
                        }
                    }
                }
            }

            for (int pass = 0; pass < 6; pass++)
            {
                bool moved = false;
                for (int i = 0; i < lows.Count; i++)
                {
                    if (chosen > lows[i] + 0.0005f && chosen < highs[i] - 0.0005f)
                    {
                        chosen = chosen - lows[i] < highs[i] - chosen ? lows[i] : highs[i];
                        moved = true;
                    }
                }

                if (!moved)
                {
                    break;
                }
            }

            return rail.Origin + rail.Dir * chosen;
        }
    }
}

namespace VRSAutomation
{
    using System.Collections.Generic;

    internal static class MountLift
    {
        internal const string Key = "vrs_lift";
        internal const float MinBulge = 0.01f;

        internal static float Current;

        internal static float Cap(string pieceName)
        {
            return pieceName.StartsWith(Conveyor.PieceName) ? 0.02f : 0.03f;
        }

        internal static float Compute(string pieceName, RaycastHit hit, Vector3 normal)
        {
            Piece surface = hit.collider != null ? hit.collider.GetComponentInParent<Piece>() : null;
            if (surface == null || surface.gameObject.name.StartsWith("VRS_"))
            {
                return 0f;
            }

            float plane = Vector3.Dot(hit.point, normal);
            float farthest = plane;
            foreach (MeshFilter filter in surface.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = mesh.bounds;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + new Vector3(
                        (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                        (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                        (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                    farthest = Mathf.Max(farthest, Vector3.Dot(matrix.MultiplyPoint3x4(corner), normal));
                }
            }

            float bulge = farthest - plane;
            return bulge < MinBulge ? 0f : Mathf.Min(bulge, Cap(pieceName));
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    internal static class MountLiftStorePatch
    {
        private static void Postfix(Piece __instance)
        {
            string name = __instance.name;
            if (MountLift.Current <= 0f || (!name.StartsWith(Conveyor.PieceName) && !Conveyor.IsJunction(name)))
            {
                return;
            }

            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner())
            {
                view.GetZDO().Set(MountLift.Key, MountLift.Current);
            }
        }
    }

    internal class MountFeet : MonoBehaviour
    {
        private struct Spec
        {
            public float X;
            public float Z;
            public float Width;
            public float Depth;
            public float Top;
            public float Ear;
        }

        private ZNetView view;
        private GameObject feet;
        private Mesh mesh;
        private float next;
        private int signature = int.MinValue;

        private void Awake()
        {
            view = GetComponent<ZNetView>();
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void Update()
        {
            if (Time.time < next)
            {
                return;
            }

            float lift;
            if (view != null && view.IsValid())
            {
                lift = view.GetZDO().GetFloat(MountLift.Key, 0f);
                next = Time.time + 2f;
            }
            else if (Player.m_localPlayer != null && Player.m_localPlayer.m_placementGhost == gameObject)
            {
                lift = MountLift.Current;
                next = Time.time + 0.1f;
            }
            else
            {
                next = Time.time + 1f;
                return;
            }

            if (lift < MountLift.MinBulge * 0.5f)
            {
                if (feet != null && feet.activeSelf)
                {
                    feet.SetActive(false);
                }

                signature = int.MinValue;
                return;
            }

            List<Vector3> centers = new List<Vector3>();
            List<Vector3> sizes = new List<Vector3>();
            foreach (Spec spec in Layout())
            {
                Foot(centers, sizes, spec, lift);
            }

            int hash = centers.Count;
            for (int i = 0; i < centers.Count; i++)
            {
                hash = hash * 31 + Mathf.RoundToInt(centers[i].y * 500f);
                hash = hash * 31 + Mathf.RoundToInt(sizes[i].y * 500f);
            }

            if (hash == signature && feet != null && feet.activeSelf)
            {
                return;
            }

            if (centers.Count == 0)
            {
                if (feet != null)
                {
                    feet.SetActive(false);
                }

                signature = hash;
                return;
            }

            Material material = PipeTool.GetPipeMaterial();
            if (material == null)
            {
                return;
            }

            signature = hash;
            if (feet == null)
            {
                feet = new GameObject("VRS_Feet");
                feet.layer = gameObject.layer;
                feet.transform.SetParent(transform, false);
                mesh = new Mesh();
                mesh.name = "VRS_Feet";
                feet.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = feet.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            FillMesh(mesh, centers, sizes);
            feet.SetActive(true);
        }

        private List<Spec> Layout()
        {
            List<Spec> specs = new List<Spec>();
            if (gameObject.name.StartsWith(Conveyor.PieceName))
            {
                foreach (float z in new[] { -0.10f, 0.10f })
                {
                    specs.Add(new Spec { X = 0f, Z = z, Width = 0.12f, Depth = 0.05f, Top = 0.004f, Ear = 0f });
                }
            }
            else
            {
                foreach (float x in new[] { -Conveyor.JunctionBarrelX, 0f, Conveyor.JunctionBarrelX })
                {
                    specs.Add(new Spec { X = x, Z = 0.11f, Width = 0.07f, Depth = 0.04f, Top = 0.012f, Ear = 0.026f });
                }

                specs.Add(new Spec { X = 0f, Z = -0.025f, Width = 0.09f, Depth = 0.04f, Top = 0.012f, Ear = 0.026f });
            }

            return specs;
        }

        private float SurfaceY(Spec spec, float lift)
        {
            int mask = Player.m_localPlayer != null ? Player.m_localPlayer.m_placeRayMask : LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            Vector3 origin = transform.TransformPoint(new Vector3(spec.X, 0.05f, spec.Z));
            Vector3 direction = -transform.up;
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, 0.35f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Piece other = hit.collider.GetComponentInParent<Piece>();
                if (other != null && other.gameObject.name.StartsWith("VRS_"))
                {
                    continue;
                }

                best = Mathf.Min(best, hit.distance);
            }

            if (best == float.MaxValue)
            {
                return -lift;
            }

            return 0.05f - best;
        }

        private void Foot(List<Vector3> centers, List<Vector3> sizes, Spec spec, float lift)
        {
            float bottom = Mathf.Clamp(SurfaceY(spec, lift), -0.14f, spec.Top);
            if (bottom > spec.Top - 0.004f)
            {
                return;
            }

            float height = spec.Top - bottom;
            centers.Add(new Vector3(spec.X, (spec.Top + bottom) * 0.5f, spec.Z));
            sizes.Add(new Vector3(spec.Width, height, spec.Depth));
            if (spec.Ear > 0f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    centers.Add(new Vector3(spec.X + side * (spec.Width * 0.5f + 0.004f), spec.Top + spec.Ear * 0.5f, spec.Z));
                    sizes.Add(new Vector3(0.008f, spec.Ear, spec.Depth));
                }
            }
        }

        private static void FillMesh(Mesh target, List<Vector3> centers, List<Vector3> sizes)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            Vector3[,] faces =
            {
                { Vector3.right, Vector3.up, Vector3.forward },
                { Vector3.left, Vector3.forward, Vector3.up },
                { Vector3.up, Vector3.forward, Vector3.right },
                { Vector3.down, Vector3.right, Vector3.forward },
                { Vector3.forward, Vector3.right, Vector3.up },
                { Vector3.back, Vector3.up, Vector3.right }
            };

            for (int box = 0; box < centers.Count; box++)
            {
                Vector3 half = sizes[box] * 0.5f;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 n = faces[f, 0];
                    Vector3 u = faces[f, 1];
                    Vector3 v = faces[f, 2];
                    float hn = Mathf.Abs(Vector3.Dot(n, half));
                    float hu = Mathf.Abs(Vector3.Dot(u, half));
                    float hv = Mathf.Abs(Vector3.Dot(v, half));
                    Vector3 a = centers[box] + n * hn - u * hu - v * hv;
                    Vector3 b = a + u * (2f * hu);
                    Vector3 c = b + v * (2f * hv);
                    Vector3 d = a + v * (2f * hv);
                    int start = vertices.Count;
                    foreach (Vector3 corner in new[] { a, b, c, d })
                    {
                        vertices.Add(corner);
                        normals.Add(n);
                        uvs.Add(new Vector2(Vector3.Dot(corner, u) * 3f, Vector3.Dot(corner, v) * 3f));
                    }

                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }

            target.Clear();
            target.SetVertices(vertices);
            target.SetNormals(normals);
            target.SetUVs(0, uvs);
            target.SetTriangles(triangles, 0);
            target.RecalculateBounds();
        }
    }
}

namespace VRSAutomation
{
    using System.Collections.Generic;

    internal static class AdapterRole
    {
        internal const string Key = "vrs_role";
        internal const int Any = 0;
        internal const int Ore = 1;
        internal const int Fuel = 2;

        internal static bool Typed(Piece host, out Smelter smelter, out List<SocketPreview.Spot> inputs)
        {
            smelter = host != null ? host.GetComponent<Smelter>() : null;
            inputs = new List<SocketPreview.Spot>();
            if (smelter == null || smelter.m_fuelItem == null)
            {
                return false;
            }

            foreach (SocketPreview.Spot spot in SocketPreview.MachineSpots(host))
            {
                if (!spot.IsOutput && spot.Items != null && spot.Items.Count > 0)
                {
                    inputs.Add(spot);
                }
            }

            return inputs.Count >= 2;
        }

        internal static int OfSpot(Smelter smelter, SocketPreview.Spot spot)
        {
            return spot.Items.Contains(smelter.m_fuelItem.gameObject.name) ? Fuel : Ore;
        }

        internal static int For(Piece host, Vector3 position)
        {
            Smelter smelter;
            List<SocketPreview.Spot> inputs;
            if (!Typed(host, out smelter, out inputs))
            {
                return Any;
            }

            SocketPreview.Spot best = null;
            float bestDistance = 0.45f;
            foreach (SocketPreview.Spot spot in inputs)
            {
                float distance = Vector3.Distance(spot.Center, position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = spot;
                }
            }

            return best != null ? OfSpot(smelter, best) : Any;
        }
    }

    internal static class SpotLabels
    {
        private sealed class Entry
        {
            public Vector3 Center;
            public Vector3 Normal;
            public Piece Owner;
            public string Text;
        }

        internal static bool Enabled = true;
        private static readonly Dictionary<int, List<Entry>> built = new Dictionary<int, List<Entry>>();
        private static readonly List<Piece> nearby = new List<Piece>();
        private static float nextScan;
        private static GUIStyle style;

        private static string ItemName(ItemDrop item)
        {
            return item != null ? ConveyorFilterWindow.Localize(item.m_itemData.m_shared.m_name) : null;
        }

        private static List<Entry> EntriesOf(Piece piece)
        {
            List<Entry> list;
            if (built.TryGetValue(piece.GetInstanceID(), out list))
            {
                return list;
            }

            list = new List<Entry>();
            Smelter smelter;
            List<SocketPreview.Spot> inputs;
            if (AdapterRole.Typed(piece, out smelter, out inputs))
            {
                foreach (SocketPreview.Spot spot in inputs)
                {
                    string text;
                    if (AdapterRole.OfSpot(smelter, spot) == AdapterRole.Fuel)
                    {
                        text = ItemName(smelter.m_fuelItem);
                    }
                    else if (smelter.m_conversion.Count == 1 && smelter.m_conversion[0].m_from != null)
                    {
                        text = ItemName(smelter.m_conversion[0].m_from);
                    }
                    else
                    {
                        text = "Ore";
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        list.Add(new Entry { Center = spot.Center, Normal = spot.Normal, Owner = piece, Text = text });
                    }
                }
            }

            built[piece.GetInstanceID()] = list;
            return list;
        }

        private static readonly List<Vector3> adapters = new List<Vector3>();

        internal static bool MenuOpen()
        {
            return Hud.IsPieceSelectionVisible() || InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || Console.IsVisible() || TextInput.IsVisible() || StoreGui.IsVisible();
        }

        private static void Scan(Vector3 origin)
        {
            nearby.Clear();
            adapters.Clear();
            GameObject ghost = Player.m_localPlayer != null ? Player.m_localPlayer.m_placementGhost : null;
            foreach (Piece piece in Piece.s_allPieces)
            {
                if (piece == null || piece.gameObject == ghost || (piece.transform.position - origin).sqrMagnitude >= 144f)
                {
                    continue;
                }

                string name = piece.gameObject.name;
                if (Conveyor.IsAdapter(name))
                {
                    adapters.Add(piece.transform.position);
                }
                else if (SocketPreview.IsHost(name) && piece.GetComponent<Smelter>() != null)
                {
                    nearby.Add(piece);
                }
            }
        }

        private static bool Blocked(Entry entry, Vector3 from, Vector3 toSpot, float distance, int mask)
        {
            if (distance <= 0.3f)
            {
                return false;
            }

            foreach (RaycastHit hit in Physics.RaycastAll(from, toSpot / distance, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (distance - hit.distance < 0.3f)
                {
                    continue;
                }

                Piece other = hit.collider.GetComponentInParent<Piece>();
                if (other == entry.Owner)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool Occupied(Vector3 center)
        {
            foreach (Vector3 position in adapters)
            {
                if ((position - center).sqrMagnitude < 0.09f)
                {
                    return true;
                }
            }

            return false;
        }

        internal static void Draw()
        {
            Player player = Player.m_localPlayer;
            Camera cam = Camera.main;
            if (!Enabled || player == null || cam == null || !PortArrows.HoldingBuildTool() || MenuOpen())
            {
                return;
            }

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.5f;
                Scan(player.transform.position);
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 32;
                style.fontStyle = FontStyle.Bold;
            }

            Transform view = cam.transform;
            int mask = player.m_placeRayMask;
            foreach (Piece piece in nearby)
            {
                if (piece == null)
                {
                    continue;
                }

                foreach (Entry entry in EntriesOf(piece))
                {
                    Vector3 toSpot = entry.Center - view.position;
                    float distance = toSpot.magnitude;
                    if (distance > 10f || Vector3.Dot(toSpot, view.forward) <= 0f || Vector3.Cross(view.forward, toSpot).magnitude > 0.6f || Occupied(entry.Center))
                    {
                        continue;
                    }

                    if (distance > 0.01f && Vector3.Dot(entry.Normal, -toSpot / distance) < 0.05f)
                    {
                        continue;
                    }

                    if (Blocked(entry, view.position, toSpot, distance, mask))
                    {
                        continue;
                    }

                    Vector3 screen = cam.WorldToScreenPoint(entry.Center + Vector3.up * 0.22f);
                    if (screen.z <= 0f)
                    {
                        continue;
                    }

                    Rect rect = new Rect(screen.x - 120f, Screen.height - screen.y - 20f, 240f, 40f);
                    GUI.color = Color.black;
                    GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), entry.Text, style);
                    GUI.color = new Color(1f, 0.85f, 0.45f, 1f);
                    GUI.Label(rect, entry.Text, style);
                    GUI.color = Color.white;
                }
            }
        }
    }
}
