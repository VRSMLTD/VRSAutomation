using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace VRSAutomation
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "VRSMLTD.VRSAutomation";
        public const string PluginName = "VRSAutomation";
        public const string PluginVersion = "0.7.2";
        public const string LinkTableName = "_VRSLinkToolPieceTable";
        public const string PipeToolName = "VRS_Pipe";
        public const string LinkToolName = "VRS_LinkTool";
        public const string CategoryName = "Automation";

        internal static readonly string HarmonyId = PluginGuid + "." + System.Guid.NewGuid().ToString("N");

        internal static ManualLogSource Log;

        private Harmony harmony;
        static partial void BindDev();
        static partial void ApplyDev();
        static partial void StartDev();

        private static ConfigEntry<int> itemsPerMove;
        private static ConfigEntry<float> secondsBetween;
        private static ConfigEntry<bool> startOff;
        private static ConfigEntry<bool> railSnapping;
        private static ConfigEntry<float> railClearance;
        private static ConfigEntry<bool> autoResume;
        private static ConfigEntry<bool> serverCatchUp;
        private static ConfigEntry<float> windmillPower;
        private static ConfigEntry<float> maxPipeLength;
        private static ConfigEntry<bool> flowAnimation;
        private static ConfigEntry<bool> pipeHop;
        private static ConfigEntry<string> pipeHopKey;
        private static ConfigEntry<bool> adapterScreen;
        private static ConfigEntry<float> screenCycle;
        private static ConfigEntry<bool> spotLabels;
        private static ConfigEntry<float> pipeCleanupDelay;
        private static ConfigEntry<float> pipeRemovalDelay;
        private static ConfigEntry<float> conveyorStartDelay;
        private static ConfigEntry<float> catchUpInterval;

        private void BindSettings()
        {
            Cfg.File = Config;

            itemsPerMove = Cfg.Bind("Conveyor Behaviour", "Items per slot per move", 1, new ConfigDescription("How many items a conveyor takes from each slot of the source box every time it runs. A box with one stack of wood moves one wood each time; a box with wood, stone and coal in three slots moves one of each.", new AcceptableValueRange<int>(1, 50)), true);
            secondsBetween = Cfg.Bind("Conveyor Behaviour", "Seconds between moves", 2f, new ConfigDescription("How many seconds pass between conveyor moves. Needs a restart to change.", new AcceptableValueRange<float>(0.2f, 30f)), true);
            startOff = Cfg.Bind("Conveyor Behaviour", "New conveyors start off", true, "A newly placed conveyor stays switched off until you turn it on (tap E on it). Conveyors built before this option existed are not affected.", true);
            railSnapping = Cfg.Bind("Placement Snapping", "Rail snapping", true, "Hold Shift while placing a conveyor, splitter or combiner to snap it to the invisible vertical and horizontal rails of nearby pieces on the same surface.");
            railClearance = Cfg.Bind("Placement Snapping", "Rail extra clearance", 0.03f, new ConfigDescription("Extra space in meters kept between two pieces on the same rail, on top of the room already reserved for the pipes that leave and enter their ports.", new AcceptableValueRange<float>(0f, 2f)));

            autoResume = Cfg.Bind("Machine Automation", "Auto resume", true, "A furnace, blast furnace, eitr refinery or frigid kiln that switched itself off for lack of fuel switches itself on again when fuel is back in its fuel slot. A machine you switched off yourself stays off.", true);
            serverCatchUp = Cfg.Bind("Machine Automation", "Server catch-up", true, "On a server, work out what unloaded furnaces, kilns, the eitr refinery, spinning wheels, windmills, beehives, bird nests and sap extractors made while no player was nearby.", true);
            windmillPower = Cfg.Bind("Machine Automation", "Windmill power while away", 0.5f, new ConfigDescription("Average wind power (0 to 1) assumed for a windmill while no player is near. The real wind cannot be known, so 0.5 means half speed.", new AcceptableValueRange<float>(0f, 1f)), true);

            maxPipeLength = Cfg.Bind("Pipe Drawing", "Max pipe length", 50f, new ConfigDescription("Longest distance in meters between the two ends of a pipe.", new AcceptableValueRange<float>(5f, 200f)), true);
            flowAnimation = Cfg.Bind("Pipe Drawing", "Flow animation", true, "Show pulses travelling along pipes while the pipe tool is out.");
            pipeHop = Cfg.Bind("Pipe Drawing", "Pipe hop", true, "When drawing a pipe over another pipe, lift it over automatically.");
            pipeHopKey = Cfg.Bind("Pipe Drawing", "Pipe hop key", "Z", "Key that turns pipe hop on and off while the pipe tool is out.");

            adapterScreen = Cfg.Bind("Display", "Adapter screen", true, "Show the item icon and count on top of storage adapters.");
            screenCycle = Cfg.Bind("Display", "Screen cycle seconds", 2.5f, new ConfigDescription("How many seconds the adapter screen shows each item type when a box holds several.", new AcceptableValueRange<float>(1f, 10f)));
            spotLabels = Cfg.Bind("Display", "Spot labels", true, "Show the item name (Ore, Coal...) above a machine's input spots while the hammer or pipe tool is out and you look at them.");

            pipeCleanupDelay = Cfg.Bind("Advanced", "Pipe cleanup start delay", 4f, "Seconds after the world is ready before orphaned pipes are checked and removed. Raise it only if pipes are ever removed wrongly after a restart.", false, true);
            pipeRemovalDelay = Cfg.Bind("Advanced", "Pipe removal delay", 0.9f, "Seconds a pipe end must stay missing before the pipe is removed.", false, true);
            conveyorStartDelay = Cfg.Bind("Advanced", "Conveyor start delay", 2f, "Extra seconds after the pipes are repaired before conveyors start moving items.", false, true);
            catchUpInterval = Cfg.Bind("Advanced", "Catch-up interval", 30f, "Seconds between server catch-up passes.", false, true);

            BindDev();

            ApplyConfig(true);
            Config.SettingChanged += (sender, args) => ApplyConfig(false);
        }

        private static void ApplyConfig(bool first)
        {
            ConveyorServer.ItemsPerSlot = Mathf.Max(1, itemsPerMove.Value);
            ConveyorSwitch.StartOff = startOff.Value;
            RailSnap.Enabled = railSnapping.Value;
            RailSnap.MinGap = Mathf.Clamp(railClearance.Value, 0f, 2f);
            MachineStorage.AutoResume = autoResume.Value;
            MachineServer.Enabled = serverCatchUp.Value;
            MachineServer.WindmillPower = Mathf.Clamp01(windmillPower.Value);
            PipeTool.MaxPipeLength = Mathf.Clamp(maxPipeLength.Value, 5f, 200f);
            PipeFlow.Enabled = flowAnimation.Value;
            AdapterScreen.Enabled = adapterScreen.Value;
            AdapterScreen.CycleSeconds = Mathf.Clamp(screenCycle.Value, 1f, 10f);
            SpotLabels.Enabled = spotLabels.Value;
            PipeServer.StartupDelay = Mathf.Max(0f, pipeCleanupDelay.Value);
            PipeServer.MissingGrace = Mathf.Max(0f, pipeRemovalDelay.Value);
            ConveyorServer.StartupDelay = Mathf.Max(0f, conveyorStartDelay.Value);
            MachineServer.Interval = catchUpInterval.Value;
            ApplyDev();

            KeyCode parsedKey;
            PipeTool.HopKey = System.Enum.TryParse(pipeHopKey.Value, true, out parsedKey) ? parsedKey : KeyCode.Z;
            if (first)
            {
                PipeTool.HopEnabled = pipeHop.Value;
            }

            Costs.Refresh();
        }

        internal static bool IsPipePiece(Piece piece)
        {
            return piece != null && piece.gameObject.name.StartsWith(PipeToolName);
        }

        internal static string AssetDirectory()
        {
            string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(location))
            {
                return System.IO.Path.GetDirectoryName(location);
            }

            return System.IO.Path.Combine(BepInEx.Paths.PluginPath, "VRSAutomation");
        }

        private void Awake()
        {
            Log = Logger;
            BindSettings();
            PrefabManager.OnVanillaPrefabsAvailable += AddLinkTool;

            harmony = new Harmony(HarmonyId);
            foreach (System.Type type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (System.Exception e)
                {
                    Log.LogError("Patch failed: " + type.Name + ": " + e.Message);
                }
            }

            StartDev();
            InvokeRepeating(nameof(ConveyorTick), 1f, Mathf.Max(0.2f, secondsBetween.Value));
            InvokeRepeating(nameof(ServerTick), 1f, 1f);
            Log.LogInfo(PluginName + " " + PluginVersion + " loaded");
        }

        private void ConveyorTick()
        {
            ConveyorServer.Tick();
        }

        private void ServerTick()
        {
            PipeServer.Tick();
            MachineServer.Tick();
        }

        private void AddLinkTool()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddLinkTool;

            ItemConfig config = new ItemConfig();
            config.Name = "Link tool";
            config.Description = "Links storage boxes and machines together.";
            config.CraftingStation = CraftingStations.Workbench;
            ConfigEntry<string> linkCost = Costs.Bind(LinkToolName, "Link tool", "Bronze:10,Wood:2", "craft the link tool");
            foreach (System.Collections.Generic.KeyValuePair<string, int> part in Costs.Parse(linkCost.Value, "Bronze:10,Wood:2"))
            {
                config.AddRequirement(new RequirementConfig(part.Key, part.Value, 0, true));
            }

            CustomItem tool = new CustomItem("VRS_LinkTool", "Hammer", config);

            CustomPieceTable table = new CustomPieceTable(LinkTableName, new PieceTableConfig());
            PieceManager.Instance.AddPieceTable(table);
            tool.ItemDrop.m_itemData.m_shared.m_buildPieces = table.PieceTable;

            ItemManager.Instance.AddItem(tool);

            foreach (Transform child in tool.ItemPrefab.GetComponentsInChildren<Transform>(true))
            {
                MeshFilter filter = child.GetComponent<MeshFilter>();
                Renderer renderer = child.GetComponent<Renderer>();
                Log.LogInfo("Tool prefab part " + child.name + " pos " + child.localPosition + " rot " + child.localEulerAngles + " scale " + child.localScale
                    + (filter != null && filter.sharedMesh != null ? " mesh " + filter.sharedMesh.name + " bounds " + filter.sharedMesh.bounds : string.Empty)
                    + (renderer != null ? " renderer " + renderer.GetType().Name : string.Empty));
            }

            Color gray = new Color(0.3f, 0.3f, 0.32f);
            foreach (Renderer renderer in tool.ItemPrefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    if (material.HasProperty("_Color"))
                    {
                        material.color = gray;
                    }
                }
                renderer.materials = materials;
            }

            ToolModel.Apply(tool.ItemPrefab);

            Sprite graySprite = null;
            Sprite[] icons = tool.ItemDrop.m_itemData.m_shared.m_icons;
            if (icons != null && icons.Length > 0 && icons[0] != null)
            {
                graySprite = MakeGraySprite(icons[0]);
                tool.ItemDrop.m_itemData.m_shared.m_icons = new Sprite[] { graySprite };
            }

            Sprite toolIcon = PipeTool.LoadToolIcon();
            if (toolIcon != null)
            {
                tool.ItemDrop.m_itemData.m_shared.m_icons = new Sprite[] { toolIcon };
            }

            PieceConfig pipeConfig = new PieceConfig();
            pipeConfig.Name = "Pipe";
            pipeConfig.Description = "Links an output to an input.";
            pipeConfig.PieceTable = LinkTableName;
            pipeConfig.Icon = toolIcon != null ? toolIcon : graySprite;
            CustomPiece pipePiece = new CustomPiece(PipeToolName, true, pipeConfig);
            Piece pipePieceComponent = pipePiece.PiecePrefab.GetComponent<Piece>();
            if (pipePieceComponent != null)
            {
                pipePieceComponent.m_canRotate = false;
            }

            PieceManager.Instance.AddPiece(pipePiece);
            Conveyor.Register(Config, graySprite);

            Log.LogInfo("Link tool added");
        }

        private static Sprite MakeGraySprite(Sprite source)
        {
            Texture2D texture = source.texture;
            Rect rect = source.textureRect;

            RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            Graphics.Blit(texture, target);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D copy = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            Color[] pixels = copy.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float level = (pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f) * 0.7f;
                pixels[i] = new Color(level, level, level * 1.05f, pixels[i].a);
            }
            copy.SetPixels(pixels);
            copy.Apply();

            return Sprite.Create(copy, new Rect(0f, 0f, copy.width, copy.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    internal static class TryPlacePiecePatch
    {
        private static bool Prefix(Piece piece, ref bool __result)
        {
            if (!Plugin.IsPipePiece(piece))
            {
                return true;
            }

            Plugin.Log.LogInfo("Pipe tool clicked");
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class UpdatePlacementGhostPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || __instance.m_buildPieces == null)
            {
                return;
            }

            if (!Plugin.IsPipePiece(__instance.m_buildPieces.GetSelectedPiece()))
            {
                return;
            }

            __instance.m_placementStatus = Player.PlacementStatus.Valid;

            if (__instance.m_placementMarkerInstance != null)
            {
                __instance.m_placementMarkerInstance.SetActive(false);
            }

            GameObject ghost = __instance.m_placementGhost;
            if (ghost == null)
            {
                return;
            }

            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }
    }
}
namespace VRSAutomation
{
    internal static class Assets
    {
        internal static byte[] ReadBytes(string file)
        {
            try
            {
                string loose = System.IO.Path.Combine(Plugin.AssetDirectory(), file);
                if (System.IO.File.Exists(loose))
                {
                    return System.IO.File.ReadAllBytes(loose);
                }

                using (System.IO.Stream stream = typeof(Assets).Assembly.GetManifestResourceStream("VRSAssets." + file))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    byte[] data = new byte[stream.Length];
                    int read = 0;
                    while (read < data.Length)
                    {
                        int count = stream.Read(data, read, data.Length - read);
                        if (count <= 0)
                        {
                            break;
                        }

                        read += count;
                    }

                    return data;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not read asset " + file + ": " + e.Message);
                return null;
            }
        }

        internal static string FilePath(string file)
        {
            string loose = System.IO.Path.Combine(Plugin.AssetDirectory(), file);
            if (System.IO.File.Exists(loose))
            {
                return loose;
            }

            byte[] data = ReadBytes(file);
            if (data == null)
            {
                return null;
            }

            try
            {
                string folder = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "VRSAutomation");
                System.IO.Directory.CreateDirectory(folder);
                string path = System.IO.Path.Combine(folder, file);
                if (!System.IO.File.Exists(path) || new System.IO.FileInfo(path).Length != data.Length)
                {
                    System.IO.File.WriteAllBytes(path, data);
                }

                return path;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not extract asset " + file + ": " + e.Message);
                return null;
            }
        }
    }
}

namespace VRSAutomation
{
    using System.Collections.Generic;
    using BepInEx.Configuration;
    using Jotunn.Configs;
    using Jotunn.Managers;

    internal static class Cfg
    {
        internal static ConfigFile File;

        internal static ConfigEntry<T> Bind<T>(string section, string key, T value, string description, bool synced = false, bool advanced = false)
        {
            return Bind(section, key, value, new ConfigDescription(description), synced, advanced);
        }

        internal static ConfigEntry<T> Bind<T>(string section, string key, T value, ConfigDescription description, bool synced = false, bool advanced = false)
        {
            ConfigurationManagerAttributes attributes = new ConfigurationManagerAttributes { IsAdminOnly = synced, IsAdvanced = advanced };
            string note = synced ? " Set by the server: on a server, every player uses the server's value." : string.Empty;
            return File.Bind(section, key, value, new ConfigDescription(description.Description + note, description.AcceptableValues, attributes));
        }
    }

    internal static class Costs
    {
        private sealed class Entry
        {
            public string Piece;
            public string Fallback;
            public ConfigEntry<string> Setting;
        }

        private static readonly List<Entry> entries = new List<Entry>();

        internal static ConfigEntry<string> Bind(string piece, string title, string defaultCost, string purpose)
        {
            ConfigEntry<string> setting = Cfg.Bind("Automation Build Pieces", title, defaultCost, "Materials needed to " + purpose + ", as item:amount pairs separated by commas, for example Bronze:2,Wood:2. Use the game's item names (Bronze, Wood, Stone, Iron, Copper, Tin...).", true);
            entries.Add(new Entry { Piece = piece, Fallback = defaultCost, Setting = setting });
            return setting;
        }

        internal static List<KeyValuePair<string, int>> Parse(string text, string fallback)
        {
            List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>();
            foreach (string part in (text ?? string.Empty).Split(','))
            {
                string[] pair = part.Split(':');
                int amount;
                if (pair.Length == 2 && pair[0].Trim().Length > 0 && int.TryParse(pair[1].Trim(), out amount) && amount > 0)
                {
                    result.Add(new KeyValuePair<string, int>(pair[0].Trim(), amount));
                }
            }

            if (result.Count == 0 && text != fallback)
            {
                Plugin.Log.LogWarning("Cost \"" + text + "\" could not be read, using " + fallback);
                return Parse(fallback, fallback);
            }

            return result;
        }

        private static Piece.Requirement[] Requirements(string text, string fallback)
        {
            List<Piece.Requirement> list = new List<Piece.Requirement>();
            foreach (KeyValuePair<string, int> part in Parse(text, fallback))
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(part.Key);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    Plugin.Log.LogWarning("Cost item " + part.Key + " does not exist, skipped");
                    continue;
                }

                list.Add(new Piece.Requirement { m_resItem = drop, m_amount = part.Value, m_recover = true });
            }

            return list.ToArray();
        }

        internal static void Refresh()
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0)
            {
                return;
            }

            foreach (Entry entry in entries)
            {
                Piece.Requirement[] wanted = Requirements(entry.Setting.Value, entry.Fallback);
                if (wanted.Length == 0)
                {
                    continue;
                }

                if (entry.Piece == Plugin.LinkToolName)
                {
                    Recipe recipe = ObjectDB.instance.m_recipes.Find(r => r != null && r.name == "Recipe_" + entry.Piece);
                    if (recipe != null)
                    {
                        recipe.m_resources = wanted;
                    }

                    continue;
                }

                CustomPiece custom = PieceManager.Instance.GetPiece(entry.Piece);
                Piece piece = custom != null ? custom.PiecePrefab.GetComponent<Piece>() : null;
                if (piece != null)
                {
                    piece.m_resources = wanted;
                }
            }
        }
    }
}
