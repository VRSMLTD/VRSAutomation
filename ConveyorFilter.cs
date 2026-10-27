using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VRSAutomation
{
    internal class FilterEntry
    {
        public string Id;
        public int Min;
        public int Max;
    }

    internal class ItemInfo
    {
        public string Prefab;
        public string Label;
        public Sprite Icon;
        public ItemDrop.ItemData Data;
    }

    internal static class ItemGroups
    {
        internal const string Prefix = "group:";

        private static readonly HashSet<string> Bars = new HashSet<string> { "Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal", "Flametal", "FlametalNew" };
        private static readonly HashSet<string> Fuel = new HashSet<string> { "Coal", "Wood", "FineWood", "ElderBark", "YggdrasilWood", "Sap", "Ice" };
        private static readonly HashSet<string> Woods = new HashSet<string> { "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood" };
        private static readonly HashSet<string> Crops = new HashSet<string> { "Carrot", "Turnip", "Onion", "Barley", "Flax" };

        internal static readonly string[] Names =
        {
            "Ores", "Metal bars", "Fuel", "Wood", "Food", "Mead", "Weapons", "Armor", "Ammo", "Tools", "Trophies", "Fish", "Seeds and crops", "Materials"
        };

        internal static bool Matches(string group, ItemInfo item)
        {
            string name = item.Prefab;
            ItemDrop.ItemData.ItemType type = item.Data.m_shared.m_itemType;
            switch (group)
            {
                case "Ores":
                    return name.EndsWith("Ore") || name.EndsWith("Scrap") || name == "FlametalOreNew";
                case "Metal bars":
                    return Bars.Contains(name);
                case "Fuel":
                    return Fuel.Contains(name);
                case "Wood":
                    return Woods.Contains(name);
                case "Food":
                    return type == ItemDrop.ItemData.ItemType.Consumable && item.Data.m_shared.m_food > 0f && name.IndexOf("Mead", System.StringComparison.OrdinalIgnoreCase) < 0;
                case "Mead":
                    return type == ItemDrop.ItemData.ItemType.Consumable && name.IndexOf("Mead", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case "Weapons":
                    return type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Bow || type == ItemDrop.ItemData.ItemType.Shield;
                case "Armor":
                    return type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder;
                case "Ammo":
                    return type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
                case "Tools":
                    return type == ItemDrop.ItemData.ItemType.Tool || type == ItemDrop.ItemData.ItemType.Torch;
                case "Trophies":
                    return type == ItemDrop.ItemData.ItemType.Trophy;
                case "Fish":
                    return type == ItemDrop.ItemData.ItemType.Fish;
                case "Seeds and crops":
                    return name.IndexOf("Seeds", System.StringComparison.OrdinalIgnoreCase) >= 0 || Crops.Contains(name);
                case "Materials":
                    return type == ItemDrop.ItemData.ItemType.Material;
                default:
                    return false;
            }
        }
    }

    internal static class ConveyorFilterWindow
    {
        private const int MaxEntries = 12;
        private static List<FilterEntry> clipboard = new List<FilterEntry>();
        private static Button pasteButton;
        internal const string FilterKey = "vrs_filter";

        private static List<ItemInfo> catalog;
        private static GameObject canvas;
        private static RectTransform cardArea;
        private static RectTransform resultArea;
        private static RectTransform resultContent;
        private const int MaxResults = 80;
        private static TMP_InputField searchField;
        private static TMP_Text emptyText;
        private static TMP_FontAsset font;
        private static Color fontColor = Color.white;
        private static Material fontMaterial;
        private static Sprite flat;
        private static Piece current;
        private static List<FilterEntry> entries = new List<FilterEntry>();
        private static string selectedId;
        private static string selectedLabel;

        internal static bool IsOpen
        {
            get { return canvas != null && canvas.activeSelf; }
        }

        internal static string Localize(string key)
        {
            if (localizeMethod == null)
            {
                System.Type type = AccessTools.TypeByName("Localization");
                if (type == null)
                {
                    return key;
                }

                localizeType = type;
                localizeMethod = type.GetMethod("Localize", new[] { typeof(string) });
                localizeInstance = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            }

            object instance = localizeInstance != null ? localizeInstance.GetValue(null, null) : null;
            return instance != null && localizeMethod != null ? (string)localizeMethod.Invoke(instance, new object[] { key }) : key;
        }

        private static System.Type localizeType;
        private static MethodInfo localizeMethod;
        private static PropertyInfo localizeInstance;

        private static List<ItemInfo> Catalog()
        {
            if (catalog != null)
            {
                return catalog;
            }

            catalog = new List<ItemInfo>();
            if (ObjectDB.instance == null)
            {
                return catalog;
            }

            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                {
                    continue;
                }

                Sprite[] icons = drop.m_itemData.m_shared.m_icons;
                if (icons == null || icons.Length == 0 || icons[0] == null)
                {
                    continue;
                }

                catalog.Add(new ItemInfo { Prefab = prefab.name, Label = Localize(drop.m_itemData.m_shared.m_name), Icon = icons[0], Data = drop.m_itemData });
            }

            catalog.Sort((a, b) => string.Compare(a.Label, b.Label, System.StringComparison.OrdinalIgnoreCase));
            return catalog;
        }

        internal static ItemInfo Find(string id)
        {
            foreach (ItemInfo item in Catalog())
            {
                if (item.Prefab == id)
                {
                    return item;
                }
            }

            return null;
        }

        private static readonly Dictionary<string, string> GroupIconPrefabs = new Dictionary<string, string>
        {
            { "Ores", "CopperOre" }, { "Metal bars", "Iron" }, { "Fuel", "Coal" }, { "Wood", "RoundLog" }, { "Food", "CookedMeat" }, { "Mead", "MeadHealthMinor" },
            { "Weapons", "SwordIron" }, { "Armor", "ArmorIronChest" }, { "Ammo", "ArrowWood" }, { "Tools", "PickaxeIron" }, { "Trophies", "TrophyBoar" },
            { "Fish", "FishRaw" }, { "Seeds and crops", "CarrotSeeds" }, { "Materials", "LeatherScraps" }
        };

        private static Sprite GroupIcon(string group)
        {
            string preferred;
            if (GroupIconPrefabs.TryGetValue(group, out preferred))
            {
                ItemInfo chosen = Find(preferred);
                if (chosen != null)
                {
                    return chosen.Icon;
                }
            }

            foreach (ItemInfo item in Catalog())
            {
                if (ItemGroups.Matches(group, item))
                {
                    return item.Icon;
                }
            }

            return null;
        }

        internal static string Serialize(List<FilterEntry> list)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            foreach (FilterEntry entry in list)
            {
                if (text.Length > 0)
                {
                    text.Append(';');
                }

                text.Append(entry.Id).Append(',').Append(entry.Min).Append(',').Append(entry.Max);
            }

            return text.ToString();
        }

        internal static List<FilterEntry> Read(ZDO zdo)
        {
            List<FilterEntry> list = new List<FilterEntry>();
            string text = zdo != null ? zdo.GetString(FilterKey, string.Empty) : string.Empty;
            foreach (string part in text.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = part.Split(',');
                int min;
                int max;
                if (fields.Length == 3 && fields[0].Length > 0 && int.TryParse(fields[1], out min) && int.TryParse(fields[2], out max))
                {
                    list.Add(new FilterEntry { Id = fields[0], Min = min, Max = max });
                }
            }

            return list;
        }

        internal static void Open(Piece conveyor)
        {
            if (conveyor == null || InventoryGui.instance == null)
            {
                return;
            }

            EnsureBuilt();
            Catalog();
            current = conveyor;
            ZNetView view = conveyor.GetComponent<ZNetView>();
            entries = view != null && view.IsValid() ? Read(view.GetZDO()) : new List<FilterEntry>();

            selectedId = null;
            selectedLabel = null;
            searchField.text = string.Empty;
            resultArea.gameObject.SetActive(false);
            canvas.SetActive(true);
            RebuildCards();
        }

        internal static void Close(bool apply)
        {
            if (apply && current != null)
            {
                ZNetView view = current.GetComponent<ZNetView>();
                if (view != null && view.IsValid())
                {
                    view.ClaimOwnership();
                    view.GetZDO().Set(FilterKey, Serialize(entries));
                }
            }

            current = null;
            if (canvas != null)
            {
                canvas.SetActive(false);
            }
        }

        internal static void Tick()
        {
            if (!IsOpen)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close(false);
                return;
            }

            emptyText.gameObject.SetActive(entries.Count == 0 && !resultArea.gameObject.activeSelf);

            if (current == null || Player.m_localPlayer == null || Vector3.Distance(Player.m_localPlayer.transform.position, current.transform.position) > 6f)
            {
                Close(false);
            }
        }

        private static void EnsureBuilt()
        {
            if (canvas != null)
            {
                return;
            }

            InventoryGui gui = InventoryGui.instance;
            font = gui.m_containerName.font;
            fontMaterial = gui.m_containerName.fontSharedMaterial;
            fontColor = gui.m_containerName.color;
            flat = MakeFlat();
            woodSprite = GameSprite(gui, "woodpanel_container");
            sunkenSprite = GameSprite(gui, "sunken");
            trackSprite = GameSprite(gui, "Background");
            handleSprite = GameSprite(gui, "UISprite");
            itemSprite = GameSprite(gui, "item_background");

            canvas = new GameObject("VRS_FilterCanvas");
            Canvas root = canvas.AddComponent<Canvas>();
            root.renderMode = RenderMode.ScreenSpaceOverlay;
            root.sortingOrder = 500;
            CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.AddComponent<GraphicRaycaster>();
            canvas.AddComponent<FilterWindowDriver>();

            GameObject dim = new GameObject("Dim");
            dim.transform.SetParent(canvas.transform, false);
            Image dimImage = dim.AddComponent<Image>();
            dimImage.sprite = flat;
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            Stretch(dim.GetComponent<RectTransform>());

            GameObject border = new GameObject("Border");
            border.transform.SetParent(canvas.transform, false);
            Image borderImage = border.AddComponent<Image>();
            borderImage.sprite = flat;
            borderImage.color = new Color(0.62f, 0.4f, 0.15f, 1f);
            border.GetComponent<RectTransform>().sizeDelta = new Vector2(832f, 652f);

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvas.transform, false);
            Image background = panel.AddComponent<Image>();
            background.sprite = flat;
            background.color = new Color(0.13f, 0.085f, 0.05f, 0.98f);
            Skin(background, woodSprite);
            border.SetActive(woodSprite == null);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(820f, 640f);

            MakeText(panel.transform, "Conveyor filter", 34f, new Vector2(0f, 290f), new Vector2(600f, 50f), TextAlignmentOptions.Center);
            MakeText(panel.transform, "Item search", 28f, new Vector2(-315f, 225f), new Vector2(140f, 44f), TextAlignmentOptions.Left);
            searchField = MakeInput(panel.transform, "Enter item or group name...", new Vector2(68f, 225f), new Vector2(634f, 42f), TMP_InputField.ContentType.Standard);
            searchField.onValueChanged.AddListener(OnSearchChanged);


            MakeText(panel.transform, "Current items", 28f, new Vector2(0f, 160f), new Vector2(400f, 44f), TextAlignmentOptions.Center);

            GameObject divider = new GameObject("Divider");
            divider.transform.SetParent(panel.transform, false);
            Image dividerImage = divider.AddComponent<Image>();
            dividerImage.sprite = flat;
            dividerImage.color = new Color(0f, 0f, 0f, 0.35f);
            dividerImage.raycastTarget = false;
            RectTransform dividerRect = divider.GetComponent<RectTransform>();
            dividerRect.anchoredPosition = new Vector2(0f, 190f);
            dividerRect.sizeDelta = new Vector2(796f, 3f);

            GameObject frame = new GameObject("CardFrame");
            frame.transform.SetParent(panel.transform, false);
            Image frameImage = frame.AddComponent<Image>();
            frameImage.sprite = flat;
            frameImage.color = new Color(0.08f, 0.05f, 0.03f, 1f);
            Skin(frameImage, sunkenSprite);
            frameImage.raycastTarget = false;
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchoredPosition = new Vector2(0f, -52.5f);
            frameRect.sizeDelta = new Vector2(786f, 375f);

            GameObject view = new GameObject("CardView");
            view.transform.SetParent(panel.transform, false);
            Image viewImage = view.AddComponent<Image>();
            viewImage.color = new Color(0f, 0f, 0f, 0f);
            RectTransform viewRect = view.GetComponent<RectTransform>();
            viewRect.anchoredPosition = new Vector2(0f, -52.5f);
            viewRect.sizeDelta = new Vector2(772f, 365f);
            view.AddComponent<RectMask2D>();

            GameObject area = new GameObject("Cards");
            area.transform.SetParent(view.transform, false);
            cardArea = area.AddComponent<RectTransform>();
            cardArea.anchorMin = new Vector2(0.5f, 1f);
            cardArea.anchorMax = new Vector2(0.5f, 1f);
            cardArea.pivot = new Vector2(0.5f, 1f);
            cardArea.anchoredPosition = new Vector2(-8f, -6f);
            cardArea.sizeDelta = new Vector2(740f, 0f);
            area.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            GridLayoutGroup grid = area.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(176f, 160f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperLeft;

            ScrollRect cardScroll = view.AddComponent<ScrollRect>();
            cardScroll.content = cardArea;
            cardScroll.viewport = viewRect;
            cardScroll.horizontal = false;
            cardScroll.vertical = true;
            cardScroll.movementType = ScrollRect.MovementType.Clamped;
            cardScroll.scrollSensitivity = 30f;
            cardScroll.verticalScrollbar = MakeScrollbar(view.transform);
            cardScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            emptyText = Body(MakeText(panel.transform, "No items assigned, conveyor will move all items", 26f, new Vector2(0f, -52.5f), new Vector2(560f, 100f), TextAlignmentOptions.Center));

            Button copy = MakeButton(gui, panel.transform, "Copy", new Vector2(175f, 160f), new Vector2(120f, 38f));
            copy.onClick.AddListener(delegate
            {
                clipboard = new List<FilterEntry>();
                foreach (FilterEntry entry in entries)
                {
                    clipboard.Add(new FilterEntry { Id = entry.Id, Min = entry.Min, Max = entry.Max });
                }

                pasteButton.interactable = clipboard.Count > 0;
            });
            pasteButton = MakeButton(gui, panel.transform, "Paste", new Vector2(305f, 160f), new Vector2(120f, 38f));
            pasteButton.onClick.AddListener(delegate
            {
                entries = new List<FilterEntry>();
                foreach (FilterEntry entry in clipboard)
                {
                    if (entries.Count < MaxEntries)
                    {
                        entries.Add(new FilterEntry { Id = entry.Id, Min = entry.Min, Max = entry.Max });
                    }
                }

                RebuildCards();
            });
            pasteButton.interactable = clipboard.Count > 0;

            Button discard = MakeButton(gui, panel.transform, "Discard changes", new Vector2(-215f, -280f), new Vector2(300f, 48f));
            discard.onClick.AddListener(delegate { Close(false); });
            Button apply = MakeButton(gui, panel.transform, "Apply", new Vector2(215f, -280f), new Vector2(300f, 48f));
            apply.onClick.AddListener(delegate { Close(true); });

            GameObject results = new GameObject("Results");
            results.transform.SetParent(panel.transform, false);
            resultArea = results.AddComponent<RectTransform>();
            resultArea.anchoredPosition = new Vector2(-30f, 20f);
            resultArea.sizeDelta = new Vector2(390f, 340f);
            Image resultBackground = results.AddComponent<Image>();
            resultBackground.sprite = flat;
            resultBackground.color = new Color(0.08f, 0.05f, 0.03f, 1f);
            Skin(resultBackground, sunkenSprite);
            results.AddComponent<RectMask2D>();

            GameObject content = new GameObject("Content");
            content.transform.SetParent(results.transform, false);
            resultContent = content.AddComponent<RectTransform>();
            resultContent.anchorMin = new Vector2(0f, 1f);
            resultContent.anchorMax = new Vector2(1f, 1f);
            resultContent.pivot = new Vector2(0.5f, 1f);
            resultContent.offsetMin = new Vector2(2f, 0f);
            resultContent.offsetMax = new Vector2(-16f, 0f);
            VerticalLayoutGroup list = content.AddComponent<VerticalLayoutGroup>();
            list.childControlHeight = true;
            list.childControlWidth = true;
            list.childForceExpandHeight = false;
            list.spacing = 2f;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scrollbar bar = MakeScrollbar(results.transform);

            ScrollRect scroll = results.AddComponent<ScrollRect>();
            scroll.content = resultContent;
            scroll.viewport = resultArea;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            results.SetActive(false);

            canvas.SetActive(false);
        }

        private static Sprite woodSprite;
        private static Sprite sunkenSprite;
        private static Sprite trackSprite;
        private static Sprite handleSprite;

        private static Sprite itemSprite;
        private static readonly Dictionary<string, Color> spriteColors = new Dictionary<string, Color>();
        private static readonly Dictionary<string, Material> spriteMaterials = new Dictionary<string, Material>();
        private static readonly Color BodyColor = new Color(0.92f, 0.89f, 0.82f, 1f);

        private static TMP_Text Body(TMP_Text text)
        {
            text.color = BodyColor;
            return text;
        }

        private static Sprite GameSprite(InventoryGui gui, string name)
        {
            List<Image> images = new List<Image>();
            images.AddRange(gui.m_container.GetComponentsInChildren<Image>(true));
            images.AddRange(gui.m_player.GetComponentsInChildren<Image>(true));
            if (gui.m_containerGrid != null && gui.m_containerGrid.m_elementPrefab != null)
            {
                images.AddRange(gui.m_containerGrid.m_elementPrefab.GetComponentsInChildren<Image>(true));
            }

            foreach (Image image in images)
            {
                if (image.sprite != null && image.sprite.name == name)
                {
                    spriteColors[name] = image.color;
                    spriteMaterials[name] = image.material;
                    return image.sprite;
                }
            }

            return null;
        }

        private static void Skin(Image image, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            Color tint;
            image.color = spriteColors.TryGetValue(sprite.name, out tint) ? tint : Color.white;
            Material material;
            if (spriteMaterials.TryGetValue(sprite.name, out material) && material != null)
            {
                image.material = material;
            }
        }

        private static Scrollbar MakeScrollbar(Transform parent)
        {
            GameObject barObject = new GameObject("Scrollbar");
            barObject.transform.SetParent(parent, false);
            Image barImage = barObject.AddComponent<Image>();
            barImage.sprite = flat;
            barImage.color = new Color(0.04f, 0.025f, 0.015f, 1f);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(12f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(barObject.transform, false);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.sprite = flat;
            handleImage.color = new Color(0.62f, 0.4f, 0.15f, 1f);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            Scrollbar bar = barObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handleRect;
            bar.targetGraphic = handleImage;
            return bar;
        }

        private static Sprite MakeFlat()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        }

        private static void CopyPanelLook(InventoryGui gui, Image target)
        {
            Image source = gui.m_container.GetComponent<Image>();
            if (source == null)
            {
                source = gui.m_container.GetComponentInChildren<Image>(true);
            }

            if (source != null && source.sprite != null)
            {
                target.sprite = source.sprite;
                target.type = source.type;
                target.color = source.color;
                target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            }
            else
            {
                target.sprite = flat;
                target.color = new Color(0.2f, 0.13f, 0.08f, 0.97f);
            }
        }

        private static TMP_Text MakeText(Transform parent, string text, float size, Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject("Text");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            if (fontMaterial != null)
            {
                label.fontSharedMaterial = fontMaterial;
            }
            label.color = fontColor;
            label.fontSize = size;
            label.text = text;
            label.alignment = alignment;
            label.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            go.SetActive(true);
            return label;
        }

        private static Button MakeButton(InventoryGui gui, Transform parent, string text, Vector2 position, Vector2 dimensions)
        {
            GameObject go = Object.Instantiate(gui.m_takeAllButton.gameObject, parent);
            go.name = "Button " + text;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            TMP_Text label = go.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = text;
            }

            return button;
        }

        private static TMP_InputField MakeInput(Transform parent, string placeholder, Vector2 position, Vector2 dimensions, TMP_InputField.ContentType contentType)
        {
            GameObject go = new GameObject("Input");
            go.transform.SetParent(parent, false);
            Image background = go.AddComponent<Image>();
            background.sprite = flat;
            Skin(background, sunkenSprite);
            background.color = new Color(0.07f, 0.045f, 0.03f, 0.9f);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(go.transform, false);
            RectTransform viewRect = viewport.AddComponent<RectTransform>();
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = new Vector2(8f, 2f);
            viewRect.offsetMax = new Vector2(-8f, -2f);
            viewport.AddComponent<RectMask2D>();

            TMP_Text text = Body(MakeText(viewport.transform, string.Empty, 22f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left));
            Stretch(text.rectTransform);
            TMP_Text hint = MakeText(viewport.transform, placeholder, 22f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
            hint.color = new Color(fontColor.r, fontColor.g, fontColor.b, 0.5f);
            Stretch(hint.rectTransform);

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.textViewport = viewRect;
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = contentType;
            input.targetGraphic = background;
            input.caretColor = fontColor;
            return input;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void OnSearchChanged(string text)
        {
            for (int i = resultContent.childCount - 1; i >= 0; i--)
            {
                Transform child = resultContent.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
            resultContent.anchoredPosition = Vector2.zero;

            selectedId = null;
            selectedLabel = null;
            string query = (text ?? string.Empty).Trim();
            if (query.Length == 0)
            {
                resultArea.gameObject.SetActive(false);
                return;
            }

            int rows = 0;
            foreach (string group in ItemGroups.Names)
            {
                if (rows < MaxResults && WordStartsWith(group, query))
                {
                    AddResultRow(ItemGroups.Prefix + group, group, GroupIcon(group));
                    rows++;
                }
            }

            List<ItemInfo> hits = new List<ItemInfo>();
            foreach (ItemInfo item in Catalog())
            {
                if (WordStartsWith(item.Label, query))
                {
                    hits.Add(item);
                }
            }

            hits.Sort((a, b) =>
            {
                bool aFirst = a.Label.StartsWith(query, System.StringComparison.OrdinalIgnoreCase);
                bool bFirst = b.Label.StartsWith(query, System.StringComparison.OrdinalIgnoreCase);
                if (aFirst != bFirst)
                {
                    return aFirst ? -1 : 1;
                }

                return string.Compare(a.Label, b.Label, System.StringComparison.OrdinalIgnoreCase);
            });

            foreach (ItemInfo item in hits)
            {
                if (rows >= MaxResults)
                {
                    break;
                }

                AddResultRow(item.Prefab, item.Label, item.Icon);
                rows++;
            }

            int visible = Mathf.Min(rows, 8);
            resultArea.sizeDelta = new Vector2(634f, visible * 42f + 4f);
            resultArea.anchoredPosition = new Vector2(68f, 204f - (visible * 42f + 4f) * 0.5f);
            resultArea.gameObject.SetActive(rows > 0);
            resultArea.SetAsLastSibling();
        }

        private static bool WordStartsWith(string label, string query)
        {
            int index = 0;
            while (index < label.Length)
            {
                if (string.Compare(label, index, query, 0, query.Length, System.StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                int space = label.IndexOf(' ', index);
                if (space < 0)
                {
                    return false;
                }

                index = space + 1;
            }

            return false;
        }

        private static void AddResultRow(string id, string label, Sprite icon)
        {
            GameObject row = new GameObject("Row");
            row.transform.SetParent(resultContent, false);
            Image background = row.AddComponent<Image>();
            background.sprite = flat;
            background.color = new Color(0.16f, 0.1f, 0.06f, 0.95f);
            row.AddComponent<LayoutElement>().preferredHeight = 40f;
            Button button = row.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;
            button.onClick.AddListener(delegate
            {
                selectedId = id;
                selectedLabel = label;
                resultArea.gameObject.SetActive(false);
                OnAdd();
                searchField.SetTextWithoutNotify(string.Empty);
            });

            if (icon != null)
            {
                GameObject iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(row.transform, false);
                Image image = iconObject.AddComponent<Image>();
                image.sprite = icon;
                image.raycastTarget = false;
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(24f, 0f);
                iconRect.sizeDelta = new Vector2(34f, 34f);
            }

            TMP_Text text = Body(MakeText(row.transform, label, 22f, new Vector2(60f, 0f), new Vector2(300f, 36f), TextAlignmentOptions.Left));
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(0f, 0.5f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(46f, 0f);

            if (id.StartsWith(ItemGroups.Prefix))
            {
                text.color = GroupColor;
                AddGroupBadge(row.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f));
            }
        }

        private static readonly Color GroupColor = new Color(0.95f, 0.62f, 0.2f, 1f);

        private static void AddGroupBadge(Transform parent, Vector2 anchor, Vector2 offset)
        {
            GameObject chip = new GameObject("GroupBadge");
            chip.transform.SetParent(parent, false);
            Image chipImage = chip.AddComponent<Image>();
            chipImage.sprite = flat;
            chipImage.color = new Color(0.04f, 0.025f, 0.015f, 1f);
            chipImage.raycastTarget = false;
            Outline chipOutline = chip.AddComponent<Outline>();
            chipOutline.effectColor = new Color(0.62f, 0.4f, 0.15f, 1f);
            chipOutline.effectDistance = new Vector2(1f, 1f);
            RectTransform chipRect = chip.GetComponent<RectTransform>();
            chipRect.anchorMin = anchor;
            chipRect.anchorMax = anchor;
            chipRect.pivot = anchor;
            chipRect.anchoredPosition = offset;
            chipRect.sizeDelta = new Vector2(64f, 22f);
            chip.AddComponent<LayoutElement>().ignoreLayout = true;

            TMP_Text label = MakeText(chip.transform, "GROUP", 14f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            label.color = GroupColor;
            Stretch(label.rectTransform);
        }

        private static void OnAdd()
        {
            if (selectedId == null)
            {
                return;
            }

            foreach (FilterEntry entry in entries)
            {
                if (entry.Id == selectedId)
                {
                    return;
                }
            }

            if (entries.Count >= MaxEntries)
            {
                return;
            }

            entries.Add(new FilterEntry { Id = selectedId });
            selectedId = null;
            selectedLabel = null;
            searchField.SetTextWithoutNotify(string.Empty);
            resultArea.gameObject.SetActive(false);
            RebuildCards();
        }

        private static void RebuildCards()
        {
            for (int i = cardArea.childCount - 1; i >= 0; i--)
            {
                Transform child = cardArea.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }

            emptyText.gameObject.SetActive(entries.Count == 0);
            foreach (FilterEntry entry in entries)
            {
                BuildCard(entry);
            }
        }

        private static void BuildCard(FilterEntry entry)
        {
            bool isGroup = entry.Id.StartsWith(ItemGroups.Prefix);
            string label = isGroup ? entry.Id.Substring(ItemGroups.Prefix.Length) : entry.Id;
            Sprite icon = null;
            if (isGroup)
            {
                icon = GroupIcon(label);
            }
            else
            {
                ItemInfo item = Find(entry.Id);
                if (item != null)
                {
                    label = item.Label;
                    icon = item.Icon;
                }
            }

            GameObject card = new GameObject("Card");
            card.transform.SetParent(cardArea, false);
            Image background = card.AddComponent<Image>();
            background.sprite = flat;
            background.color = new Color(0.1f, 0.065f, 0.04f, 0.92f);

            if (icon != null)
            {
                GameObject iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(card.transform, false);
                Image image = iconObject.AddComponent<Image>();
                image.sprite = icon;
                image.raycastTarget = false;
                RectTransform rect = iconObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(0f, 32f);
                rect.sizeDelta = new Vector2(42f, 42f);
            }

            TMP_Text name = Body(MakeText(card.transform, label, 18f, new Vector2(0f, -6f), new Vector2(166f, 26f), TextAlignmentOptions.Center));
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            if (isGroup)
            {
                name.color = GroupColor;
                AddGroupBadge(card.transform, new Vector2(0f, 1f), new Vector2(4f, -4f));
            }

            if (isGroup)
            {
                Body(MakeText(card.transform, "Whole group", 17f, new Vector2(0f, -52f), new Vector2(166f, 24f), TextAlignmentOptions.Center));
            }
            else
            {
                Body(MakeText(card.transform, "Min", 17f, new Vector2(-54f, -38f), new Vector2(50f, 24f), TextAlignmentOptions.Left));
                TMP_InputField min = MakeInput(card.transform, "-", new Vector2(18f, -38f), new Vector2(90f, 24f), TMP_InputField.ContentType.IntegerNumber);
                min.SetTextWithoutNotify(entry.Min > 0 ? entry.Min.ToString() : string.Empty);
                min.onEndEdit.AddListener(delegate (string value) { entry.Min = ParseCount(value); });

                Body(MakeText(card.transform, "Max", 17f, new Vector2(-54f, -66f), new Vector2(50f, 24f), TextAlignmentOptions.Left));
                TMP_InputField max = MakeInput(card.transform, "-", new Vector2(18f, -66f), new Vector2(90f, 24f), TMP_InputField.ContentType.IntegerNumber);
                max.SetTextWithoutNotify(entry.Max > 0 ? entry.Max.ToString() : string.Empty);
                max.onEndEdit.AddListener(delegate (string value) { entry.Max = ParseCount(value); });
            }

            GameObject remove = new GameObject("Remove");
            remove.transform.SetParent(card.transform, false);
            Image removeBackground = remove.AddComponent<Image>();
            removeBackground.sprite = flat;
            removeBackground.color = new Color(0.65f, 0.12f, 0.1f, 1f);
            RectTransform removeRect = remove.GetComponent<RectTransform>();
            removeRect.anchorMin = new Vector2(1f, 1f);
            removeRect.anchorMax = new Vector2(1f, 1f);
            removeRect.pivot = new Vector2(1f, 1f);
            removeRect.anchoredPosition = new Vector2(-4f, -4f);
            removeRect.sizeDelta = new Vector2(24f, 24f);
            Button removeButton = remove.AddComponent<Button>();
            removeButton.targetGraphic = removeBackground;
            removeButton.onClick.AddListener(delegate
            {
                entries.Remove(entry);
                RebuildCards();
            });
            MakeText(remove.transform, "X", 18f, Vector2.zero, new Vector2(24f, 24f), TextAlignmentOptions.Center);
        }

        private static int ParseCount(string value)
        {
            int result;
            return int.TryParse(value, out result) && result > 0 ? result : 0;
        }
    }

    internal class FilterWindowDriver : MonoBehaviour
    {
        private void Update()
        {
            ConveyorFilterWindow.Tick();
        }
    }

    internal class ConveyorInteract : MonoBehaviour, Interactable, Hoverable
    {
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold)
            {
                return false;
            }

            return ConveyorMenu.Start(GetComponent<Piece>(), user);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

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
                PortArrows.Build(arrows.transform, Conveyor.PieceName);
            }

            arrows.SetActive(PortArrows.HoldingBuildTool() && Time.frameCount - hoverFrame <= 2);
        }

        public string GetHoverText()
        {
            hoverFrame = Time.frameCount;
            Piece piece = GetComponent<Piece>();
            bool on = ConveyorSwitch.IsOn(piece);
            ZNetView view = piece != null ? piece.GetComponent<ZNetView>() : null;
            string status = on && view != null && view.IsValid() ? "\n<color=#c8c8c8>" + ConveyorSwitch.StatusText(ConveyorSwitch.StatusOf(view.GetZDO())) + "</color>" : string.Empty;
            return ConveyorFilterWindow.Localize("Conveyor (" + (on ? "on" : "off") + ")" + status + "\n[<color=yellow><b>$KEY_Use</b></color>] Turn " + (on ? "off" : "on") + " (hold Shift: whole circuit)\nMore options available: hold [<color=yellow><b>$KEY_Use</b></color>]");
        }

        public string GetHoverName()
        {
            return "Conveyor";
        }

        public float GetHoverOffset()
        {
            return 0f;
        }
    }

    [HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class FilterTakeInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (ConveyorFilterWindow.IsOpen)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class FilterTextInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (ConveyorFilterWindow.IsOpen)
            {
                __result = true;
            }
        }
    }
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
    internal static class FilterScrollPatch
    {
        private static void Postfix(ref float __result)
        {
            if (ConveyorFilterWindow.IsOpen)
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(Menu), "Update")]
    internal static class FilterMenuPatch
    {
        private static bool Prefix()
        {
            return !ConveyorFilterWindow.IsOpen;
        }
    }
}
