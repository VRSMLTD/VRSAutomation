using System;
using Jotunn.Managers;
using UnityEngine;

namespace VRSAutomation
{
    internal static class IconRenderer
    {
        private static readonly string[] Names =
        {
            Conveyor.PieceName, Conveyor.SplitterName, Conveyor.CombinerName,
            Conveyor.AdapterDualName, Conveyor.AdapterInName, Conveyor.AdapterOutName
        };

        private static bool done;

        internal static void Run(ZNetScene scene)
        {
            if (done || RenderManager.Instance == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            done = true;
            foreach (string name in Names)
            {
                try
                {
                    Render(scene, name);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Icon for " + name + " failed: " + e.Message);
                }
            }
        }

        private static void Render(ZNetScene scene, string name)
        {
            GameObject prefab = scene.GetPrefab(name);
            Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (piece == null)
            {
                return;
            }

            Material material = PieceTextures.For(name);
            if (material != null)
            {
                foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterial = material;
                }
            }

            RenderManager.RenderRequest request = new RenderManager.RenderRequest(prefab);
            request.Width = 128;
            request.Height = 128;
            if (name == Conveyor.PieceName || name == Conveyor.SplitterName || name == Conveyor.CombinerName)
            {
                Quaternion topDown = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 90f, 0f);
                if (name == Conveyor.PieceName)
                {
                    request.Rotation = Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(180f, 0f, 0f) * topDown;
                }
                else
                {
                    request.Rotation = Quaternion.Euler(0f, 0f, name == Conveyor.SplitterName ? -90f : 90f) * topDown;
                }
            }

            RenderManager.Instance.EnqueueRender(request, sprite =>
            {
                if (sprite == null)
                {
                    Plugin.Log.LogWarning("Icon for " + name + " could not be rendered");
                    return;
                }

                piece.m_icon = sprite;
                Plugin.Log.LogInfo("Icon rendered for " + name);
            });
        }
    }
}
