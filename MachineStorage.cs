using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VRSAutomation
{
    internal static class MachineStorage
    {
        internal const string OnKey = "vrs_on";
        internal const string ResumeKey = "vrs_resume";
        internal static bool AutoResume = true;

        internal static readonly Vector2i[] OutputSlots = { new Vector2i(0, 1), new Vector2i(1, 1), new Vector2i(2, 1) };

        private static readonly Vector2i[] OneInput = { new Vector2i(1, 0) };
        private static readonly Vector2i[] TwoInputs = { new Vector2i(1, 0), new Vector2i(2, 0) };
        private const float None = float.NaN;

        internal sealed class MachineKind
        {
            public string Title;
            public bool HasFuel;
            public bool Toggle;
            public Vector2i FuelSlot = new Vector2i(0, 0);
            public float ExtraDistance;
            public Vector2i[] Inputs;
            public float[] Columns;
            public float Middle;
            public float FuelLabel;
            public float InputLabel;
            public float OutputLabel;
            public float Arrow1;
            public float Arrow2;
        }

        private static readonly MachineKind FurnaceKind = new MachineKind
        {
            Title = "Furnace", HasFuel = true, Toggle = true, Inputs = TwoInputs,
            Columns = new[] { 0f, 1.6f, 2.6f, 4.2f, 5.2f, 6.2f }, Middle = 3.1f,
            FuelLabel = 0f, InputLabel = 2.1f, OutputLabel = 5.2f, Arrow1 = 0.8f, Arrow2 = 3.4f
        };

        private static readonly MachineKind BlastFurnaceKind = new MachineKind
        {
            Title = "Blast furnace", HasFuel = true, Toggle = true, Inputs = TwoInputs,
            Columns = FurnaceKind.Columns, Middle = 3.1f,
            FuelLabel = 0f, InputLabel = 2.1f, OutputLabel = 5.2f, Arrow1 = 0.8f, Arrow2 = 3.4f
        };

        private static readonly MachineKind KilnKind = new MachineKind
        {
            Title = "Charcoal kiln", HasFuel = false, Toggle = true, Inputs = OneInput, ExtraDistance = 1f,
            Columns = new[] { None, 0f, None, 1.6f, 2.6f, 3.6f }, Middle = 1.8f,
            FuelLabel = None, InputLabel = 0f, OutputLabel = 2.6f, Arrow1 = 0.8f, Arrow2 = None
        };

        private static readonly MachineKind RefineryKind = new MachineKind
        {
            Title = "Eitr refinery", HasFuel = true, Toggle = true, Inputs = OneInput,
            Columns = new[] { 0f, 1.6f, None, 3.2f, 4.2f, 5.2f }, Middle = 2.6f,
            FuelLabel = 0f, InputLabel = 1.6f, OutputLabel = 4.2f, Arrow1 = 0.8f, Arrow2 = 2.4f
        };

        private static readonly MachineKind WheelKind = new MachineKind
        {
            Title = "Spinning wheel", HasFuel = false, Toggle = false, Inputs = OneInput,
            Columns = KilnKind.Columns, Middle = 1.8f,
            FuelLabel = None, InputLabel = 0f, OutputLabel = 2.6f, Arrow1 = 0.8f, Arrow2 = None
        };

        private static readonly MachineKind FrostKilnKind = new MachineKind
        {
            Title = "Frigid kiln", HasFuel = true, Toggle = true, Inputs = new Vector2i[0], FuelSlot = new Vector2i(1, 0), ExtraDistance = 3f,
            Columns = new[] { None, 0f, None, 1.6f, 2.6f, 3.6f }, Middle = 1.8f,
            FuelLabel = None, InputLabel = 0f, OutputLabel = 2.6f, Arrow1 = 0.8f, Arrow2 = None
        };

        private static readonly MachineKind WindmillKind = new MachineKind
        {
            Title = "Windmill", HasFuel = false, Toggle = false, Inputs = TwoInputs,
            Columns = new[] { None, 0f, 1f, 2.6f, 3.6f, 4.6f }, Middle = 2.3f,
            FuelLabel = None, InputLabel = 0.5f, OutputLabel = 3.6f, Arrow1 = 1.8f, Arrow2 = None
        };

        private static MachineKind ProducerKind(string title)
        {
            return new MachineKind
            {
                Title = title, HasFuel = false, Toggle = false, Inputs = new Vector2i[0],
                Columns = new[] { None, None, None, 0f, 1f, 2f }, Middle = 1f,
                FuelLabel = None, InputLabel = None, OutputLabel = 1f, Arrow1 = None, Arrow2 = None
            };
        }

        private static readonly MachineKind BeehiveKind = ProducerKind("Beehive");
        private static readonly MachineKind NestKind = ProducerKind("Bird nest");
        private static readonly MachineKind SapKind = ProducerKind("Sap extractor");

        private static readonly ConditionalWeakTable<Inventory, Container> producers = new ConditionalWeakTable<Inventory, Container>();

        internal static bool IsProducerInventory(Inventory inventory)
        {
            Container container;
            return inventory != null && producers.TryGetValue(inventory, out container);
        }

        private static MachineKind ProducerKindOf(Component producer)
        {
            if (producer is SapCollector)
            {
                return SapKind;
            }

            return producer.gameObject.name.StartsWith("piece_birdnest") ? NestKind : BeehiveKind;
        }

        internal static MachineKind KindOfContainer(Container container)
        {
            Smelter smelter = container.GetComponent<Smelter>();
            if (smelter != null)
            {
                return KindOf(smelter);
            }

            Beehive hive = container.GetComponentInChildren<Beehive>();
            if (hive != null)
            {
                return ProducerKindOf(hive);
            }

            SapCollector sap = container.GetComponentInChildren<SapCollector>();
            return sap != null ? SapKind : null;
        }

        internal static void SetupProducer(Component producer)
        {
            ZNetView view = producer.GetComponentInParent<ZNetView>();
            if (view == null || view.GetZDO() == null)
            {
                return;
            }

            MachineKind kind = ProducerKindOf(producer);
            Container container = view.GetComponent<Container>();
            if (container == null)
            {
                container = view.gameObject.AddComponent<Container>();
                container.m_name = kind.Title;
                container.GetInventory().m_name = kind.Title;
                WearNTear wear = view.GetComponent<WearNTear>();
                if (wear != null)
                {
                    wear.m_onDestroyed += container.OnDestroyed;
                }
            }

            producers.Remove(container.GetInventory());
            producers.Add(container.GetInventory(), container);
        }

        internal static MachineKind KindOf(Smelter smelter)
        {
            string name = smelter.gameObject.name;
            if (name.StartsWith("smelter"))
            {
                return FurnaceKind;
            }

            if (name.StartsWith("blastfurnace"))
            {
                return BlastFurnaceKind;
            }

            if (name.StartsWith("charcoal_kiln"))
            {
                return KilnKind;
            }

            if (name.StartsWith("eitrrefinery"))
            {
                return RefineryKind;
            }

            if (name.StartsWith("piece_spinningwheel"))
            {
                return WheelKind;
            }

            if (name.StartsWith("piece_FrostKiln"))
            {
                return FrostKilnKind;
            }

            return name.StartsWith("windmill") ? WindmillKind : null;
        }

        internal static bool IsFurnace(Smelter smelter)
        {
            return KindOf(smelter) != null;
        }

        internal static Vector2i FuelSlotOf(Smelter smelter)
        {
            return KindOf(smelter).FuelSlot;
        }

        internal static bool HasFuelSlot(Smelter smelter)
        {
            return KindOf(smelter).HasFuel;
        }

        internal static Vector2i[] InputsOf(Smelter smelter)
        {
            return KindOf(smelter).Inputs;
        }

        internal static string Title(Smelter smelter)
        {
            return KindOf(smelter).Title;
        }

        internal static bool IsRunning(Smelter smelter, ZNetView view)
        {
            return !KindOf(smelter).Toggle || IsOn(view);
        }

        internal static void Setup(Smelter smelter)
        {
            if (!IsFurnace(smelter))
            {
                return;
            }

            Container container = smelter.GetComponent<Container>();
            if (container == null)
            {
                ZNetView view = smelter.GetComponent<ZNetView>();
                if (view == null || view.GetZDO() == null)
                {
                    return;
                }

                string title = Title(smelter);
                container = smelter.gameObject.AddComponent<Container>();
                container.m_name = title;
                container.GetInventory().m_name = title;
            }

            Register(container.GetInventory(), smelter);
            RedirectSwitches(smelter);
        }

        internal static bool IsOn(ZNetView view)
        {
            return view.GetZDO().GetInt(OnKey, 0) != 0;
        }

        private static readonly ConditionalWeakTable<Inventory, Smelter> registry = new ConditionalWeakTable<Inventory, Smelter>();

        internal static void Register(Inventory inventory, Smelter smelter)
        {
            registry.Remove(inventory);
            registry.Add(inventory, smelter);
        }

        internal static void RedirectSwitches(Smelter smelter)
        {
            Container container = smelter.GetComponent<Container>();
            if (container == null)
            {
                return;
            }

            Switch[] switches = { smelter.m_addWoodSwitch, smelter.m_addOreSwitch, smelter.m_emptyOreSwitch };
            foreach (Switch item in switches)
            {
                if (item == null)
                {
                    continue;
                }

                foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = true;
                }

                item.m_onUse = (caller, user, held) => held == null && container.Interact(user, false, false);
                item.m_onHover = () => container.GetHoverText();
            }
        }

        internal static bool TryGetSmelter(Inventory inventory, out Smelter smelter)
        {
            smelter = null;
            return inventory != null && registry.TryGetValue(inventory, out smelter) && smelter != null;
        }

        internal static bool IsFuel(Smelter smelter, ItemDrop.ItemData item)
        {
            return HasFuelSlot(smelter) && item.m_shared.m_name == smelter.m_fuelItem.m_itemData.m_shared.m_name;
        }

        private static bool IsInput(Smelter smelter, ItemDrop.ItemData item)
        {
            return item.m_dropPrefab != null && smelter.IsItemAllowed(item.m_dropPrefab.name);
        }

        internal static bool Accepts(Smelter smelter, ItemDrop.ItemData item)
        {
            return IsFuel(smelter, item) || IsInput(smelter, item);
        }

        internal static bool SlotAccepts(Smelter smelter, Vector2i pos, ItemDrop.ItemData item)
        {
            if (pos == FuelSlotOf(smelter))
            {
                return IsFuel(smelter, item);
            }

            foreach (Vector2i slot in InputsOf(smelter))
            {
                if (pos == slot)
                {
                    return IsInput(smelter, item);
                }
            }

            return false;
        }

        internal static void Deny()
        {
            if (Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "This container can't accept this type of item.");
            }
        }

        internal static bool Deposit(Inventory inventory, Smelter smelter, ItemDrop.ItemData item)
        {
            List<Vector2i> slots = new List<Vector2i>();
            if (IsFuel(smelter, item))
            {
                slots.Add(FuelSlotOf(smelter));
            }

            if (IsInput(smelter, item))
            {
                slots.AddRange(InputsOf(smelter));
            }

            int original = item.m_stack;
            int remaining = original;
            int max = item.m_shared.m_maxStackSize;
            bool changed = false;

            foreach (Vector2i slot in slots)
            {
                ItemDrop.ItemData existing = inventory.GetItemAt(slot.x, slot.y);
                if (remaining <= 0 || existing == null || existing.m_shared.m_name != item.m_shared.m_name)
                {
                    continue;
                }

                int add = Mathf.Min(max - existing.m_stack, remaining);
                if (add <= 0)
                {
                    continue;
                }

                existing.m_stack += add;
                remaining -= add;
                changed = true;
            }

            foreach (Vector2i slot in slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (inventory.GetItemAt(slot.x, slot.y) != null)
                {
                    continue;
                }

                ItemDrop.ItemData copy = item.Clone();
                copy.m_stack = Mathf.Min(max, remaining);
                copy.m_gridPos = slot;
                int placed = copy.m_stack;
                if (inventory.AddItem(copy, slot))
                {
                    remaining -= placed;
                }
            }

            if (changed)
            {
                inventory.Changed(true, false);
            }

            if (remaining <= 0)
            {
                return true;
            }

            if (remaining < original)
            {
                item.m_stack = remaining;
            }

            return false;
        }

        internal static bool Toggle(Smelter smelter)
        {
            ZNetView view = smelter.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !KindOf(smelter).Toggle)
            {
                return false;
            }

            bool on = !IsOn(view);
            if (on && !HasFuel(smelter))
            {
                Say("Needs fuel to start");
                return false;
            }

            view.ClaimOwnership();
            view.GetZDO().Set(OnKey, on ? 1 : 0);
            view.GetZDO().Set(ResumeKey, 0);
            Say(Title(smelter) + (on ? " ON" : " OFF"));
            return on;
        }

        internal static bool TryResume(Smelter smelter, ZNetView view)
        {
            if (!AutoResume || !HasFuelSlot(smelter) || view.GetZDO().GetInt(ResumeKey, 0) == 0)
            {
                return false;
            }

            if (!HasFuel(smelter))
            {
                return false;
            }

            view.GetZDO().Set(OnKey, 1);
            view.GetZDO().Set(ResumeKey, 0);
            Say(Title(smelter) + " restarted");
            return true;
        }

        private static bool HasFuel(Smelter smelter)
        {
            if (!HasFuelSlot(smelter))
            {
                return true;
            }

            Container container = smelter.GetComponent<Container>();
            return container != null && FuelInSlot(container.GetInventory(), smelter) >= smelter.m_fuelPerProduct;
        }

        private static void Say(string text)
        {
            if (Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, text);
            }
        }

        private static ItemDrop.ItemData FindInput(Inventory inventory, Smelter smelter, string oreName)
        {
            foreach (Vector2i slot in InputsOf(smelter))
            {
                ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                if (item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == oreName)
                {
                    return item;
                }
            }

            return null;
        }

        internal static bool ConsumeInput(Container container, Smelter smelter, string oreName, int amount)
        {
            Inventory inventory = container.GetInventory();
            int available = 0;
            foreach (Vector2i slot in InputsOf(smelter))
            {
                ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                if (item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == oreName)
                {
                    available += item.m_stack;
                }
            }

            if (available < amount)
            {
                return false;
            }

            foreach (Vector2i slot in InputsOf(smelter))
            {
                if (amount <= 0)
                {
                    break;
                }

                ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                if (item == null || item.m_dropPrefab == null || item.m_dropPrefab.name != oreName)
                {
                    continue;
                }

                int take = Mathf.Min(item.m_stack, amount);
                inventory.RemoveItem(item, take);
                amount -= take;
            }

            return true;
        }

        internal static void Feed(Smelter smelter, Container container)
        {
            Inventory inventory = container.GetInventory();

            if (InputsOf(smelter).Length == 0)
            {
                if (FuelInSlot(inventory, smelter) >= smelter.m_fuelPerProduct)
                {
                    if (smelter.GetFuel() < smelter.m_fuelPerProduct)
                    {
                        smelter.SetFuel(smelter.m_fuelPerProduct);
                    }
                }
                else
                {
                    smelter.SetFuel(0f);
                    smelter.GetComponent<ZNetView>().GetZDO().Set(OnKey, 0);
                    smelter.GetComponent<ZNetView>().GetZDO().Set(ResumeKey, 1);
                    Say(Title(smelter) + " out of fuel");
                }

                return;
            }

            if (smelter.GetProcessedQueueSize() > 0)
            {
                smelter.SpawnProcessed();
            }

            if (smelter.GetQueueSize() > 0)
            {
                if (FindInput(inventory, smelter, smelter.GetQueuedOre()) == null)
                {
                    smelter.RemoveOneOre();
                    smelter.SetBakeTimer(0f);
                }
            }
            else
            {
                foreach (Vector2i slot in InputsOf(smelter))
                {
                    ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                    if (item != null && item.m_dropPrefab != null && smelter.IsItemAllowed(item.m_dropPrefab.name))
                    {
                        smelter.RPC_AddOre(0L, item.m_dropPrefab.name, false);
                        break;
                    }
                }
            }

            if (!HasFuelSlot(smelter))
            {
                return;
            }

            if (smelter.GetQueueSize() == 0)
            {
                if (smelter.GetFuel() > 0f)
                {
                    smelter.SetFuel(0f);
                }

                return;
            }

            if (FuelInSlot(inventory, smelter) >= smelter.m_fuelPerProduct)
            {
                if (smelter.GetFuel() < smelter.m_fuelPerProduct)
                {
                    smelter.SetFuel(smelter.m_fuelPerProduct);
                }

                return;
            }

            smelter.SetFuel(0f);
            smelter.GetComponent<ZNetView>().GetZDO().Set(OnKey, 0);
            smelter.GetComponent<ZNetView>().GetZDO().Set(ResumeKey, 1);
            Say(Title(smelter) + " out of fuel");
        }

        internal static int FuelInSlot(Inventory inventory, Smelter smelter)
        {
            ItemDrop.ItemData fuel = inventory.GetItemAt(FuelSlotOf(smelter).x, FuelSlotOf(smelter).y);
            return fuel != null && IsFuel(smelter, fuel) ? fuel.m_stack : 0;
        }

        internal static void ConsumeFuel(Container container, Smelter smelter, int amount)
        {
            Inventory inventory = container.GetInventory();
            ItemDrop.ItemData fuel = inventory.GetItemAt(FuelSlotOf(smelter).x, FuelSlotOf(smelter).y);
            if (fuel != null && IsFuel(smelter, fuel))
            {
                inventory.RemoveItem(fuel, Mathf.Min(fuel.m_stack, amount));
            }
        }

        internal static int AddOutput(Container container, ItemDrop prefab, int amount)
        {
            return AddOutput(container.GetInventory(), prefab, amount);
        }

        internal static int AddOutput(Inventory inventory, ItemDrop prefab, int amount)
        {
            ItemDrop.ItemData proto = prefab.m_itemData;
            int max = proto.m_shared.m_maxStackSize;
            bool changed = false;

            foreach (Vector2i slot in OutputSlots)
            {
                if (amount <= 0)
                {
                    break;
                }

                ItemDrop.ItemData existing = inventory.GetItemAt(slot.x, slot.y);
                if (existing == null || existing.m_shared.m_name != proto.m_shared.m_name)
                {
                    continue;
                }

                int add = Mathf.Min(max - existing.m_stack, amount);
                if (add <= 0)
                {
                    continue;
                }

                existing.m_stack += add;
                amount -= add;
                changed = true;
            }

            foreach (Vector2i slot in OutputSlots)
            {
                if (amount <= 0)
                {
                    break;
                }

                if (inventory.GetItemAt(slot.x, slot.y) != null)
                {
                    continue;
                }

                ItemDrop.ItemData item = proto.Clone();
                item.m_dropPrefab = prefab.gameObject;
                item.m_stack = Mathf.Min(max, amount);
                item.m_gridPos = slot;
                int placed = item.m_stack;
                if (inventory.AddItem(item, slot))
                {
                    amount -= placed;
                }
            }

            if (changed)
            {
                inventory.Changed(true, false);
            }

            return amount;
        }
    }

    [HarmonyPatch(typeof(Smelter), "Awake")]
    internal static class SmelterAwakePatch
    {
        private static void Postfix(Smelter __instance)
        {
            MachineStorage.Setup(__instance);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    internal static class InventoryAddItemPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (MachineStorage.IsProducerInventory(__instance))
            {
                MachineStorage.Deny();
                __result = false;
                return false;
            }

            Smelter smelter;
            if (!MachineStorage.TryGetSmelter(__instance, out smelter))
            {
                return true;
            }

            if (!MachineStorage.Accepts(smelter, item))
            {
                MachineStorage.Deny();
                __result = false;
                return false;
            }

            __result = MachineStorage.Deposit(__instance, smelter, item);
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "DropItem")]
    internal static class InventoryGridDropPatch
    {
        private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
        {
            Inventory target = __instance.GetInventory();
            if (MachineStorage.IsProducerInventory(target))
            {
                MachineStorage.Deny();
                __result = false;
                return false;
            }

            Smelter smelter;
            if (MachineStorage.IsProducerInventory(fromInventory))
            {
                ItemDrop.ItemData held = target.GetItemAt(pos.x, pos.y);
                if (held != null && held != item && held.m_shared.m_name != item.m_shared.m_name)
                {
                    __result = false;
                    return false;
                }

                return true;
            }

            if (MachineStorage.TryGetSmelter(target, out smelter))
            {
                if (!MachineStorage.SlotAccepts(smelter, pos, item))
                {
                    MachineStorage.Deny();
                    __result = false;
                    return false;
                }

                return true;
            }

            if (MachineStorage.TryGetSmelter(fromInventory, out smelter))
            {
                ItemDrop.ItemData occupant = target.GetItemAt(pos.x, pos.y);
                if (occupant != null && occupant != item && occupant.m_shared.m_name != item.m_shared.m_name)
                {
                    __result = false;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Smelter), "UpdateSmelter")]
    internal static class SmelterPausePatch
    {
        private static bool Prefix(Smelter __instance)
        {
            if (!MachineStorage.IsFurnace(__instance))
            {
                return true;
            }

            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || MachineStorage.IsRunning(__instance, view))
            {
                return true;
            }

            if (view.IsOwner() && MachineStorage.TryResume(__instance, view))
            {
                return true;
            }

            if (view.IsOwner())
            {
                __instance.GetDeltaTime();
                if (__instance.GetFuel() > 0f)
                {
                    __instance.SetFuel(0f);
                }
            }

            __instance.UpdateState();
            __instance.UpdateSmoke();
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "IsActive")]
    internal static class SmelterIsActivePatch
    {
        private static void Postfix(Smelter __instance, ref bool __result)
        {
            if (!__result || !MachineStorage.IsFurnace(__instance))
            {
                return;
            }

            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !MachineStorage.IsRunning(__instance, view))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Smelter), "UpdateState")]
    internal static class SmelterStatePatch
    {
        private static void Postfix(Smelter __instance)
        {
            if (!MachineStorage.IsFurnace(__instance) || __instance.m_haveFuelObject == null)
            {
                return;
            }

            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !MachineStorage.IsRunning(__instance, view))
            {
                __instance.m_haveFuelObject.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(Smelter), "UpdateSmelter")]
    internal static class SmelterFeedPatch
    {
        private static void Postfix(Smelter __instance)
        {
            if (!MachineStorage.IsFurnace(__instance))
            {
                return;
            }

            ZNetView view = __instance.GetComponent<ZNetView>();
            Container container = __instance.GetComponent<Container>();
            if (view == null || !view.IsValid() || !view.IsOwner() || container == null || !MachineStorage.IsRunning(__instance, view))
            {
                return;
            }

            MachineStorage.Feed(__instance, container);
        }
    }

    [HarmonyPatch(typeof(Smelter), "Spawn")]
    internal static class SmelterSpawnPatch
    {
        private static bool Prefix(Smelter __instance, string ore, ref int stack)
        {
            if (!MachineStorage.IsFurnace(__instance))
            {
                return true;
            }

            Container container = __instance.GetComponent<Container>();
            if (container == null)
            {
                return true;
            }

            bool noInputs = MachineStorage.InputsOf(__instance).Length == 0;
            Smelter.ItemConversion conversion = noInputs && __instance.m_conversion.Count > 0 ? __instance.m_conversion[0] : __instance.GetItemConversion(ore);
            if (conversion == null)
            {
                return true;
            }

            bool kiln = !MachineStorage.HasFuelSlot(__instance);
            int fuelNeeded = kiln ? 0 : __instance.m_fuelPerProduct * stack;
            if (MachineStorage.FuelInSlot(container.GetInventory(), __instance) < fuelNeeded || (!noInputs && !MachineStorage.ConsumeInput(container, __instance, ore, stack)))
            {
                return false;
            }

            if (fuelNeeded > 0)
            {
                MachineStorage.ConsumeFuel(container, __instance, fuelNeeded);
            }

            stack = MachineStorage.AddOutput(container, conversion.m_to, stack);
            return stack > 0;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
    internal static class MachineDistancePatch
    {
        private static float baseDistance = -1f;

        private static void Prefix(InventoryGui __instance)
        {
            if (baseDistance < 0f)
            {
                baseDistance = __instance.m_autoCloseDistance > 8f ? 4f : __instance.m_autoCloseDistance;
            }

            Container container = __instance.m_currentContainer;
            MachineStorage.MachineKind kind = container != null ? MachineStorage.KindOfContainer(container) : null;
            __instance.m_autoCloseDistance = kind != null ? baseDistance + kind.ExtraDistance : baseDistance;

            Player player = Player.m_localPlayer;
            if (container != null && player != null && Vector3.Distance(container.transform.position, player.transform.position) > __instance.m_autoCloseDistance)
            {
                __instance.Hide();
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class FurnaceHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            if (MachineStorage.KindOfContainer(__instance) == null || __result == null)
            {
                return;
            }

            __result = System.Text.RegularExpressions.Regex.Replace(__result, @"\s*\([^)]*\)\s*$", "");
            if (MachineMenu.Open)
            {
                __result = string.Empty;
                return;
            }

            if (MachineStorage.KindOfContainer(__instance).Toggle)
            {
                __result += ConveyorFilterWindow.Localize("\nMore options available: hold [<color=yellow><b>$KEY_Use</b></color>]");
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class FurnaceInteractPatch
    {
        private static bool Prefix(Container __instance, object[] __args, bool hold, ref bool __result)
        {
            if (!hold)
            {
                if (MachineMenu.Start(__instance, __args[0] as Humanoid))
                {
                    __result = true;
                    return false;
                }

                return true;
            }

            if (MachineStorage.KindOfContainer(__instance) == null)
            {
                return true;
            }

            __result = NativeRadial.ForcePlaceMode ? false : __result;
            return false;
        }
    }

    [HarmonyPatch(typeof(Beehive), "Awake")]
    internal static class BeehiveAwakePatch
    {
        private static void Postfix(Beehive __instance)
        {
            MachineStorage.SetupProducer(__instance);
        }
    }

    [HarmonyPatch(typeof(SapCollector), "Awake")]
    internal static class SapCollectorAwakePatch
    {
        private static void Postfix(SapCollector __instance)
        {
            MachineStorage.SetupProducer(__instance);
        }
    }

    [HarmonyPatch(typeof(Beehive), "UpdateBees")]
    internal static class BeehiveCollectPatch
    {
        internal static void Postfix(Beehive __instance)
        {
            ZNetView view = __instance.GetComponentInParent<ZNetView>();
            Container container = __instance.GetComponentInParent<Container>();
            if (container == null || view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }

            int level = __instance.GetHoneyLevel();
            if (level <= 0)
            {
                return;
            }

            int left = MachineStorage.AddOutput(container, __instance.m_honeyItem, level);
            if (left < level)
            {
                __instance.IncreseLevel(left - level);
                if (ConveyorServer.LogMoves)
                {
                    Plugin.Log.LogInfo(System.DateTime.Now.ToString("HH:mm:ss") + " Producer " + __instance.gameObject.name + " stored " + (level - left) + " " + __instance.m_honeyItem.gameObject.name);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SapCollector), "UpdateTick")]
    internal static class SapCollectPatch
    {
        internal static void Postfix(SapCollector __instance)
        {
            ZNetView view = __instance.GetComponentInParent<ZNetView>();
            Container container = __instance.GetComponentInParent<Container>();
            if (container == null || view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }

            ResourceRoot root = __instance.m_root;
            if (root != null && root.m_nview != null && root.m_nview.IsValid())
            {
                ZDO own = view.GetZDO();
                ZDO rootZdo = root.m_nview.GetZDO();
                if (own != null && rootZdo != null && (own.GetString(MachineServer.RootPrefabKey, string.Empty).Length == 0 || Vector3.Distance(own.GetVec3(MachineServer.RootPosKey, Vector3.zero), rootZdo.GetPosition()) > 0.5f))
                {
                    own.Set(MachineServer.RootPosKey, rootZdo.GetPosition());
                    own.Set(MachineServer.RootPrefabKey, Utils.GetPrefabName(root.m_nview.gameObject));
                    Plugin.Log.LogInfo("Sap extractor linked to " + Utils.GetPrefabName(root.m_nview.gameObject) + " at " + rootZdo.GetPosition());
                }
            }

            int level = __instance.GetLevel();
            if (level <= 0)
            {
                return;
            }

            int left = MachineStorage.AddOutput(container, __instance.m_spawnItem, level);
            if (left < level)
            {
                __instance.IncreseLevel(left - level);
                if (ConveyorServer.LogMoves)
                {
                    Plugin.Log.LogInfo(System.DateTime.Now.ToString("HH:mm:ss") + " Producer " + __instance.gameObject.name + " stored " + (level - left) + " " + __instance.m_spawnItem.gameObject.name);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Beehive), "Interact")]
    internal static class BeehiveInteractPatch
    {
        private static bool Prefix(Beehive __instance, Humanoid character, bool repeat, bool alt, ref bool __result)
        {
            Container container = __instance.GetComponentInParent<Container>();
            if (container == null)
            {
                return true;
            }

            __result = !repeat && container.Interact(character, repeat, alt);
            return false;
        }
    }

    [HarmonyPatch(typeof(SapCollector), "Interact")]
    internal static class SapInteractPatch
    {
        private static bool Prefix(SapCollector __instance, Humanoid character, bool repeat, bool alt, ref bool __result)
        {
            Container container = __instance.GetComponentInParent<Container>();
            if (container == null)
            {
                return true;
            }

            __result = !repeat && container.Interact(character, repeat, alt);
            return false;
        }
    }

    [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoverText))]
    internal static class BeehiveHoverPatch
    {
        private static void Postfix(Beehive __instance, ref string __result)
        {
            Container container = __instance.GetComponentInParent<Container>();
            if (container != null)
            {
                __result = HoverMerge.Merge(__result, container.GetHoverText(), container);
            }
        }
    }

    [HarmonyPatch(typeof(SapCollector), nameof(SapCollector.GetHoverText))]
    internal static class SapHoverPatch
    {
        private static void Postfix(SapCollector __instance, ref string __result)
        {
            Container container = __instance.GetComponentInParent<Container>();
            if (container != null)
            {
                __result = HoverMerge.Merge(__result, container.GetHoverText(), container);
            }
        }
    }

    internal static class HoverMerge
    {
        internal static string Merge(string original, string open, Container container)
        {
            if (string.IsNullOrEmpty(original))
            {
                return open;
            }

            int first = original.IndexOf('\n');
            int last = open.LastIndexOf('\n');
            if (first > 0 && last >= 0)
            {
                string line = original.Substring(0, first);
                int stored = 0;
                foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
                {
                    stored += item.m_stack;
                }

                if (stored > 0)
                {
                    int bracket = line.IndexOf(" (");
                    string name = bracket > 0 ? line.Substring(0, bracket) : line;
                    line = name + " ( " + stored + " stored )";
                }

                return line + open.Substring(last);
            }

            return open;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnDestroyed")]
    internal static class SmelterDestroyedPatch
    {
        private static void Postfix(Smelter __instance)
        {
            Container container = __instance.GetComponent<Container>();
            if (container != null)
            {
                container.OnDestroyed();
            }
        }
    }

    internal static class FurnacePanel
    {

        private static readonly Dictionary<InventoryElement, Vector3> original = new Dictionary<InventoryElement, Vector3>();
        private static TMP_Text fuelLabel;
        private static TMP_Text inputLabel;
        private static TMP_Text outputLabel;
        private static TMP_Text firstArrow;
        private static TMP_Text secondArrow;
        private static Button toggleButton;
        private static TMP_Text toggleText;
        private static Smelter current;
        private static bool applied;

        private static int ColumnIndex(Vector2i pos)
        {
            return pos.y * 3 + pos.x;
        }

        private static TMP_Text MakeText(InventoryGui gui, string name, string text, float size)
        {
            GameObject go = Object.Instantiate(gui.m_containerName.gameObject, gui.m_container);
            go.name = name;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(160f, 40f);
            TMP_Text label = go.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static void Build(InventoryGui gui)
        {
            fuelLabel = MakeText(gui, "VRS_FuelLabel", "FUEL", 20f);
            inputLabel = MakeText(gui, "VRS_InputLabel", "INPUT", 20f);
            outputLabel = MakeText(gui, "VRS_OutputLabel", "OUTPUT", 20f);
            firstArrow = MakeText(gui, "VRS_Arrow1", ">", 40f);
            secondArrow = MakeText(gui, "VRS_Arrow2", ">", 40f);

            GameObject buttonObject = Object.Instantiate(gui.m_takeAllButton.gameObject, gui.m_container);
            buttonObject.name = "VRS_ToggleButton";
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(200f, 44f);
            rect.anchoredPosition = new Vector2(0f, 14f);
            toggleButton = buttonObject.GetComponent<Button>();
            toggleButton.onClick = new Button.ButtonClickedEvent();
            toggleButton.onClick.AddListener(OnToggleClicked);
            toggleText = buttonObject.GetComponentInChildren<TMP_Text>();
        }

        private static void OnToggleClicked()
        {
            if (current != null)
            {
                MachineStorage.Toggle(current);
            }
        }

        private static void SetVisible(bool visible)
        {
            TMP_Text[] texts = { fuelLabel, inputLabel, outputLabel, firstArrow, secondArrow };
            foreach (TMP_Text text in texts)
            {
                if (text != null)
                {
                    text.gameObject.SetActive(visible);
                }
            }

            if (toggleButton != null)
            {
                toggleButton.gameObject.SetActive(visible);
            }
        }

        private static void PlaceOrHide(TMP_Text label, Transform parent, Vector3 center, float pitch, float middle, float column, float rows)
        {
            bool present = !float.IsNaN(column);
            label.gameObject.SetActive(present);
            if (present)
            {
                Place(label, parent, center, pitch, middle, column, rows);
            }
        }

        private static void Place(TMP_Text label, Transform parent, Vector3 center, float pitch, float middle, float column, float rows)
        {
            Vector3 local = new Vector3(center.x + (column - middle + 0.5f) * pitch, center.y + (rows - 0.5f) * pitch, center.z);
            label.rectTransform.position = parent.TransformPoint(local);
        }

        internal static void Apply(InventoryGui gui, MachineStorage.MachineKind kind, Smelter smelter)
        {
            List<InventoryElement> elements = gui.m_containerGrid.m_elements;
            if (elements == null || elements.Count < 6)
            {
                return;
            }

            if (fuelLabel == null || toggleButton == null)
            {
                Build(gui);
            }

            current = smelter;
            foreach (InventoryElement element in elements)
            {
                if (!original.ContainsKey(element))
                {
                    original[element] = element.transform.localPosition;
                }
            }

            Vector3 first = Vector3.zero;
            Vector3 second = Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (InventoryElement element in elements)
            {
                Vector3 pos = original[element];
                sum += pos;
                if (element.Position == new Vector2i(0, 0))
                {
                    first = pos;
                }
                else if (element.Position == new Vector2i(1, 0))
                {
                    second = pos;
                }
            }

            float pitch = Mathf.Abs(second.x - first.x);
            if (pitch < 1f)
            {
                return;
            }

            float[] columns = kind.Columns;
            float middle = kind.Middle;
            Vector3 center = sum / elements.Count;
            Transform parent = elements[0].transform.parent;
            foreach (InventoryElement element in elements)
            {
                int index = ColumnIndex(element.Position);
                if (index < 0 || index >= columns.Length)
                {
                    continue;
                }

                bool used = !float.IsNaN(columns[index]);
                element.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                Vector3 pos = original[element];
                pos.x = center.x + (columns[index] - middle) * pitch;
                pos.y = center.y;
                element.transform.localPosition = pos;
            }

            SetVisible(true);
            PlaceOrHide(fuelLabel, parent, center, pitch, middle, kind.FuelLabel, 0.9f);
            PlaceOrHide(inputLabel, parent, center, pitch, middle, kind.InputLabel, 0.9f);
            PlaceOrHide(outputLabel, parent, center, pitch, middle, kind.OutputLabel, 0.9f);
            PlaceOrHide(firstArrow, parent, center, pitch, middle, kind.Arrow1, 0f);
            PlaceOrHide(secondArrow, parent, center, pitch, middle, kind.Arrow2, 0f);
            toggleButton.gameObject.SetActive(kind.Toggle);

            ZNetView view = smelter != null ? smelter.GetComponent<ZNetView>() : null;
            if (toggleText != null && view != null && view.IsValid())
            {
                toggleText.text = MachineStorage.IsOn(view) ? "Turn Off" : "Turn On";
            }

            applied = true;
        }

        internal static void Reset()
        {
            if (!applied)
            {
                return;
            }

            foreach (KeyValuePair<InventoryElement, Vector3> pair in original)
            {
                if (pair.Key != null)
                {
                    pair.Key.transform.localPosition = pair.Value;
                    pair.Key.gameObject.SetActive(true);
                }
            }

            original.Clear();
            SetVisible(false);
            current = null;
            applied = false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
    internal static class FurnacePanelPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            Container container = __instance.m_currentContainer;
            MachineStorage.MachineKind kind = container != null ? MachineStorage.KindOfContainer(container) : null;
            if (kind != null)
            {
                FurnacePanel.Apply(__instance, kind, container.GetComponent<Smelter>());
            }
            else if (container != null || !InventoryGui.IsVisible())
            {
                FurnacePanel.Reset();
            }
        }
    }
}

namespace VRSAutomation
{
    internal static class MachineMenu
    {
        private const float HoldSeconds = 0.35f;

        private static Container pending;
        private static float started;
        private static bool bypass;

        internal static bool Open;

        internal static bool Busy
        {
            get { return pending != null || Open || native; }
        }

        internal static bool Start(Container container, Humanoid character)
        {
            if (bypass || character == null || character != Player.m_localPlayer)
            {
                return false;
            }

            Smelter smelter = container.GetComponent<Smelter>();
            ZNetView view = smelter != null ? smelter.GetComponent<ZNetView>() : null;
            MachineStorage.MachineKind kind = smelter != null ? MachineStorage.KindOf(smelter) : null;
            if (kind == null || !kind.Toggle || view == null || !view.IsValid())
            {
                return false;
            }

            pending = container;
            native = false;
            started = Time.time;
            Open = false;
            return true;
        }

        private static void Reset()
        {
            pending = null;
            native = false;
            Open = false;
        }

        private static void OpenWindow(Container container, Player player)
        {
            bypass = true;
            container.Interact(player, false, false);
            bypass = false;
        }

        private static bool native;
        private static float nativeStarted;

        private static bool TryNative(Container container)
        {
            try
            {
                EnsureIcons();
                Player player = Player.m_localPlayer;
                Smelter smelter = container.GetComponent<Smelter>();
                ZNetView view = smelter != null ? smelter.GetComponent<ZNetView>() : null;
                bool on = view != null && view.IsValid() && MachineStorage.IsRunning(smelter, view);
                string machine = container.m_name.ToLower();

                System.Collections.Generic.List<RadialItem> items = new System.Collections.Generic.List<RadialItem>();
                items.Add(new RadialItem
                {
                    Name = on ? "Turn off" : "Turn on",
                    Description = (on ? "Stop the " : "Start the ") + machine,
                    Icon = flameIcon,
                    Action = delegate
                    {
                        if (smelter != null)
                        {
                            MachineStorage.Toggle(smelter);
                        }

                        return true;
                    }
                });
                items.Add(new RadialItem
                {
                    Name = "Open",
                    Description = "Open the storage window",
                    Icon = boxIcon,
                    Action = delegate
                    {
                        if (container != null && player != null)
                        {
                            OpenWindow(container, player);
                        }

                        return true;
                    }
                });

                if (!NativeRadial.Open(container.m_name, boxIcon, items, () => ZInput.GetButton("Use") || ZInput.GetButton("JoyUse")))
                {
                    return false;
                }

                nativeStarted = Time.time;
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Native radial failed, opening the window instead: " + e.Message);
                return false;
            }
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

            if (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
            {
                if (!Open && Time.time - started >= HoldSeconds)
                {
                    if (TryNative(pending))
                    {
                        native = true;
                        Open = true;
                        return;
                    }

                    Container fallback = pending;
                    Reset();
                    OpenWindow(fallback, player);
                }

                return;
            }

            Container target = pending;
            bool wasOpen = Open;
            Reset();
            if (!wasOpen)
            {
                OpenWindow(target, player);
            }
        }

        private static Sprite flameIcon;
        private static Sprite boxIcon;

        private static void EnsureIcons()
        {
            if (flameIcon == null && ObjectDB.instance != null)
            {
                GameObject torch = ObjectDB.instance.GetItemPrefab("Torch");
                ItemDrop drop = torch != null ? torch.GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData.m_shared.m_icons != null && drop.m_itemData.m_shared.m_icons.Length > 0)
                {
                    flameIcon = drop.m_itemData.m_shared.m_icons[0];
                }
            }

            if (boxIcon == null && ZNetScene.instance != null)
            {
                GameObject chest = ZNetScene.instance.GetPrefab("piece_chest_wood");
                Piece piece = chest != null ? chest.GetComponent<Piece>() : null;
                if (piece != null)
                {
                    boxIcon = piece.m_icon;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Player), "Update")]
    internal static class MachineMenuTickPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                MachineMenu.Tick();
            }
        }
    }

    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class MachineMenuCursorPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (MachineMenu.Open)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class MachineMenuInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (MachineMenu.Open)
            {
                __result = false;
            }
        }
    }
}

namespace VRSAutomation
{
    internal sealed class RadialItem
    {
        internal string Name;
        internal string Description;
        internal Sprite Icon;
        internal Color Tint = Color.white;
        internal bool GoldIcon;
        internal System.Func<bool> Action;
    }

    internal sealed class NativeRadial : Valheim.UI.IRadialConfig
    {
        private readonly string title;
        private readonly Sprite sprite;
        private readonly System.Collections.Generic.List<RadialItem> items;

        private NativeRadial(string title, Sprite sprite, System.Collections.Generic.List<RadialItem> items)
        {
            this.title = title;
            this.sprite = sprite;
            this.items = items;
        }

        public string LocalizedName
        {
            get { return title; }
        }

        public Sprite Sprite
        {
            get { return sprite; }
        }

        public void InitRadialConfig(Valheim.UI.RadialBase radial)
        {
            System.Collections.Generic.List<Valheim.UI.RadialMenuElement> list = new System.Collections.Generic.List<Valheim.UI.RadialMenuElement>();
            foreach (RadialItem item in items)
            {
                Valheim.UI.EmoteElement element = UnityEngine.Object.Instantiate(Valheim.UI.RadialData.SO.EmoteElement);
                element.Name = item.Name;
                element.Description = item.Description;
                element.Interact = item.Action;
                element.CloseOnInteract = () => true;
                if (element.Icon != null)
                {
                    element.Icon.gameObject.SetActive(item.Icon != null);
                    element.Icon.sprite = item.Icon;
                    element.Icon.color = item.Tint;
                }

                list.Add(element);
            }

            radial.ConstructRadial(list);
        }

        internal static void ClearStale(Valheim.UI.RadialBase radial)
        {
            if (radial.m_animationManager != null)
            {
                radial.m_animationManager.EndAll();
            }

            radial.m_isClosing = false;
            radial.m_closeQueued = false;
            if (radial.gameObject.activeSelf)
            {
                radial.InstantClose();
            }

            radial.m_isClosing = false;
        }

        internal static bool ForcePlaceMode;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern System.IntPtr GetActiveWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ClientToScreen(System.IntPtr window, ref NativePoint point);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        internal static void CenterCursor()
        {
            try
            {
                NativePoint point = new NativePoint { X = Screen.width / 2, Y = Screen.height / 2 };
                if (ClientToScreen(GetActiveWindow(), ref point))
                {
                    SetCursorPos(point.X, point.Y);
                }
            }
            catch (System.Exception)
            {
            }
        }

        internal static void Configure(Valheim.UI.RadialBase radial, System.Func<bool> holding)
        {
            radial.GetConfirm = () => false;
            radial.GetReleaseToUse = () => !holding();
            radial.GetClose = () => (!holding() && radial.Selected == null) || UnityEngine.Input.GetKeyDown(KeyCode.Escape);
            radial.GetBack = () => false;
            radial.GetMouseDirection = () =>
            {
                Vector2 offset = (Vector2)UnityEngine.Input.mousePosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return offset.magnitude < 40f ? Vector2.zero : offset;
            };
            CenterCursor();
        }


        internal static bool Active
        {
            get
            {
                Hud hud = Hud.instance;
                return hud != null && hud.m_radialMenu != null && hud.m_radialMenu.Active && !hud.m_radialMenu.m_isClosing && !hud.m_radialMenu.m_closeQueued;
            }
        }

        internal static bool Open(string title, Sprite sprite, System.Collections.Generic.List<RadialItem> items, System.Func<bool> holding)
        {
            try
            {
                Hud hud = Hud.instance;
                Valheim.UI.RadialBase radial = hud != null ? hud.m_radialMenu : null;
                if (radial == null)
                {
                    return false;
                }

                if (Valheim.UI.RadialData.SO == null)
                {
                    Valheim.UI.RadialDataSO[] found = Resources.FindObjectsOfTypeAll<Valheim.UI.RadialDataSO>();
                    if (found.Length > 0)
                    {
                        Valheim.UI.RadialData.Init(found[0]);
                    }
                }

                if (Valheim.UI.RadialData.SO == null)
                {
                    return false;
                }

                RustRing.Restore();
                NativeRadial.ClearStale(radial);
                radial.CanOpen = true;

                if (!radial.gameObject.activeSelf)
                {
                    radial.gameObject.SetActive(true);
                }

                ForcePlaceMode = true;
                radial.Open(new NativeRadial(title, sprite, items), null);
                if (!radial.Active)
                {
                    radial.CanOpen = true;
                    radial.Open(new NativeRadial(title, sprite, items), null);
                }
                ForcePlaceMode = false;

                if (!radial.Active)
                {
                    Plugin.Log.LogWarning("Native radial (" + title + ") did not open: self=" + radial.gameObject.activeSelf + " place=" + (Player.m_localPlayer != null && Player.m_localPlayer.InPlaceMode()) + " repair=" + (Player.m_localPlayer != null && Player.m_localPlayer.InRepairMode()) + " canOpen=" + radial.CanOpen + " config=" + (radial.CurrentConfig != null) + " elems=" + (radial.m_elements != null ? radial.m_elements.Count : -1));
                    return false;
                }

                Configure(radial, holding);
                RustRing.Attach(radial, items, title, sprite);
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Native radial failed, opening the window instead: " + e.Message);
                return false;
            }
        }
    }
}


namespace VRSAutomation
{
    [HarmonyPatch(typeof(Valheim.UI.RadialBase), "get_InRadialBlockingPlaceMode")]
    internal static class RadialPlaceModePatch
    {
        private static void Postfix(ref bool __result)
        {
            __result = NativeRadial.ForcePlaceMode ? false : __result;
        }
    }
}

namespace VRSAutomation
{
    [HarmonyPatch(typeof(Player), "HandleRadialInput")]
    internal static class OwnRadialGuardPatch
    {
        private static bool Prefix()
        {
            if (MachineMenu.Busy || ConveyorMenu.Pending || PipeColors.WheelOpen || NativeRadial.Active)
            {
                return false;
            }

            return !(SocketPreview.PipeActive && UnityEngine.Input.GetKey(KeyCode.R));
        }
    }
}
