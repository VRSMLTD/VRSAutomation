using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRSAutomation
{
    internal static class ToolModel
    {
        private static Texture2D wrenchTexture;
        private const float Length = 0.47f;
        private const float ButtZ = -0.10f;

        internal static void Apply(GameObject item)
        {
            MeshFilter target = null;
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null)
                {
                    target = filter;
                    break;
                }
            }

            if (target == null)
            {
                Plugin.Log.LogWarning("Tool model: no mesh found on the tool prefab");
                return;
            }

            Texture2D palette;
            Mesh mesh = BuildExtruded(out palette);
            if (mesh == null)
            {
                return;
            }

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            Material material = new Material(renderer.sharedMaterial);
            material.mainTexture = palette;
            material.color = Color.white;
            Neutral(material, "startup");
            foreach (Collider existing in item.GetComponentsInChildren<Collider>(true))
            {
                Plugin.Log.LogInfo("Tool collider " + existing.GetType().Name + " on " + existing.name + " trigger " + existing.isTrigger);
                BoxCollider box = existing as BoxCollider;
                if (box != null && !box.isTrigger)
                {
                    box.transform.localScale = Vector3.one;
                    box.size = mesh.bounds.size;
                    box.center = mesh.bounds.center;
                }
            }

            target.sharedMesh = mesh;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;
            renderer.sharedMaterials = new[] { material };
            wrenchTexture = palette;
        }

        private static void Neutral(Material material, string label)
        {
            string textures = string.Empty;
            foreach (string property in material.GetTexturePropertyNames())
            {
                if (property != "_MainTex" && material.GetTexture(property) != null)
                {
                    textures += property + " ";
                    material.SetTexture(property, null);
                }
            }

            string[] zero = { "_Metallic", "_Glossiness", "_Smoothness", "_GlossMapScale", "_Shininess", "_SpecularHighlights", "_GlossyReflections", "_BumpScale" };
            foreach (string property in zero)
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, 0f);
                }
            }

            foreach (string property in new[] { "_SpecColor", "_EmissionColor" })
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, Color.black);
                }
            }

            Plugin.Log.LogInfo("Tool material (" + label + ") shader " + material.shader.name + ", cleared maps: " + textures);
        }

        internal static void Refresh()
        {
            if (wrenchTexture == null || ObjectDB.instance == null)
            {
                return;
            }

            GameObject item = ObjectDB.instance.GetItemPrefab(Plugin.LinkToolName);
            Material source = PipeTool.GetPipeMaterial();
            if (item == null || source == null)
            {
                return;
            }

            Material material = new Material(source);
            material.mainTexture = wrenchTexture;
            material.color = Color.white;
            Neutral(material, "refreshed");
            Plugin.Log.LogInfo("Tool material refreshed on " + item.name);
            foreach (MeshRenderer renderer in item.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponent<MeshFilter>() != null && renderer.GetComponent<MeshFilter>().sharedMesh != null && renderer.GetComponent<MeshFilter>().sharedMesh.name == "VRS_Wrench")
                {
                    renderer.sharedMaterials = new[] { material };
                }
            }
        }

        private const float Thickness = 0.03f;

        private static readonly List<Vector3> slabVertices = new List<Vector3>();
        private static readonly List<Vector2> slabUvs = new List<Vector2>();
        private static readonly List<int> slabTriangles = new List<int>();

        private static void AddSlabQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3, Vector3 outward)
        {
            int start = slabVertices.Count;
            slabVertices.Add(p0);
            slabVertices.Add(p1);
            slabVertices.Add(p2);
            slabVertices.Add(p3);
            slabUvs.Add(u0);
            slabUvs.Add(u1);
            slabUvs.Add(u2);
            slabUvs.Add(u3);
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) >= 0f)
            {
                slabTriangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            else
            {
                slabTriangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
        }

        private static float AlongOf(int x, int y, Vector2 along)
        {
            return (x + 0.5f) * along.x + (y + 0.5f) * along.y;
        }

        private static void Extent(bool[] mask, int w, int h, Vector2 along, out float low, out float high)
        {
            low = float.MaxValue;
            high = float.MinValue;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y * w + x])
                    {
                        float u = AlongOf(x, y, along);
                        low = Mathf.Min(low, u);
                        high = Mathf.Max(high, u);
                    }
                }
            }
        }

        private static Mesh BuildExtruded(out Texture2D texture)
        {
            texture = null;
            byte[] data = Assets.ReadBytes("VRS_tool_icon.png");
            if (data == null)
            {
                return null;
            }

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!PipeTool.TryLoadPng(source, data))
            {
                return null;
            }

            int w = source.width;
            int h = source.height;
            Color32[] pixels = source.GetPixels32();
            bool[] mask = new bool[w * h];
            double sumX = 0;
            double sumY = 0;
            int count = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a >= 217)
                    {
                        mask[y * w + x] = true;
                        sumX += x + 0.5;
                        sumY += y + 0.5;
                        count++;
                    }
                }
            }

            if (count < 50)
            {
                return null;
            }

            double meanX = sumX / count;
            double meanY = sumY / count;
            double cxx = 0;
            double cxy = 0;
            double cyy = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y * w + x])
                    {
                        double dx = x + 0.5 - meanX;
                        double dy = y + 0.5 - meanY;
                        cxx += dx * dx;
                        cxy += dx * dy;
                        cyy += dy * dy;
                    }
                }
            }

            double angle = 0.5 * System.Math.Atan2(2.0 * cxy, cxx - cyy);
            Vector2 along = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
            float low;
            float high;
            Extent(mask, w, h, along, out low, out high);

            int nearLow = 0;
            int nearHigh = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!mask[y * w + x])
                    {
                        continue;
                    }

                    float u = AlongOf(x, y, along);
                    if (u < low + (high - low) * 0.12f)
                    {
                        nearLow++;
                    }
                    else if (u > high - (high - low) * 0.12f)
                    {
                        nearHigh++;
                    }
                }
            }

            if (nearLow > nearHigh)
            {
                along = -along;
                Extent(mask, w, h, along, out low, out high);
            }

            Vector2 right = new Vector2(along.y, -along.x);
            float scale = Length / (high - low);
            double handleSum = 0;
            int handleCount = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y * w + x] && AlongOf(x, y, along) < low + (high - low) * 0.35f)
                    {
                        handleSum += (x + 0.5f) * right.x + (y + 0.5f) * right.y;
                        handleCount++;
                    }
                }
            }

            float handleCenter = handleCount > 0 ? (float)(handleSum / handleCount) : 0f;

            slabVertices.Clear();
            slabUvs.Clear();
            slabTriangles.Clear();
            float top = Thickness * 0.5f;

            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (!mask[y * w + x])
                    {
                        x++;
                        continue;
                    }

                    int start = x;
                    while (x < w && mask[y * w + x])
                    {
                        x++;
                    }

                    Vector2 uv00 = new Vector2((float)start / w, (float)y / h);
                    Vector2 uv10 = new Vector2((float)x / w, (float)y / h);
                    Vector2 uv11 = new Vector2((float)x / w, (float)(y + 1) / h);
                    Vector2 uv01 = new Vector2((float)start / w, (float)(y + 1) / h);
                    AddSlabQuad(Place(start, y, top, along, right, low, handleCenter, scale), Place(x, y, top, along, right, low, handleCenter, scale),
                        Place(x, y + 1, top, along, right, low, handleCenter, scale), Place(start, y + 1, top, along, right, low, handleCenter, scale),
                        uv00, uv10, uv11, uv01, Vector3.up);
                    AddSlabQuad(Place(start, y, -top, along, right, low, handleCenter, scale), Place(x, y, -top, along, right, low, handleCenter, scale),
                        Place(x, y + 1, -top, along, right, low, handleCenter, scale), Place(start, y + 1, -top, along, right, low, handleCenter, scale),
                        uv00, uv10, uv11, uv01, Vector3.down);
                }
            }

            int[] stepX = { -1, 1, 0, 0 };
            int[] stepY = { 0, 0, -1, 1 };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!mask[y * w + x])
                    {
                        continue;
                    }

                    Vector2 center = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                    for (int side = 0; side < 4; side++)
                    {
                        int nx = x + stepX[side];
                        int ny = y + stepY[side];
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && mask[ny * w + nx])
                        {
                            continue;
                        }

                        float ex0 = side == 1 ? x + 1 : x;
                        float ey0 = side == 3 ? y + 1 : y;
                        float ex1 = side < 2 ? ex0 : x + 1;
                        float ey1 = side < 2 ? y + 1 : ey0;
                        Vector3 outward = new Vector3(stepX[side] * right.x + stepY[side] * right.y, 0f, stepX[side] * along.x + stepY[side] * along.y);
                        AddSlabQuad(Place(ex0, ey0, top, along, right, low, handleCenter, scale), Place(ex1, ey1, top, along, right, low, handleCenter, scale),
                            Place(ex1, ey1, -top, along, right, low, handleCenter, scale), Place(ex0, ey0, -top, along, right, low, handleCenter, scale),
                            center, center, center, center, outward);
                    }
                }
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = 255;
            }

            texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();

            Mesh mesh = new Mesh();
            mesh.name = "VRS_Wrench";
            mesh.SetVertices(slabVertices);
            mesh.SetUVs(0, slabUvs);
            mesh.SetTriangles(slabTriangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Plugin.Log.LogInfo("Tool model: wrench slab from icon, " + count + " pixels, " + slabVertices.Count + " vertices, bounds " + mesh.bounds);
            return mesh;
        }

        private static Vector3 Place(float px, float py, float y, Vector2 along, Vector2 right, float low, float handleCenter, float scale)
        {
            float u = px * along.x + py * along.y;
            float v = px * right.x + py * right.y;
            return new Vector3((v - handleCenter) * scale, y, (u - low) * scale + ButtZ);
        }
    }
}
