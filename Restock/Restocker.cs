using System;
using System.Collections.Generic;
using UnityEngine;

namespace Restock
{
    /// <summary>
    ///     Restock action: while a container is open, an item is taken as a template and
    ///     every matching item found in the container is moved into the player inventory
    ///     until the free slots or the maximum carry weight are exhausted. The template is
    ///     the item under the mouse cursor, the item picked with a plain left click, or
    ///     both, following the configured selection mode.
    /// </summary>
    internal static class Restocker
    {
        private enum Result
        {
            Moved,
            NothingSelected,
            NoMatchingItems,
            NoSpace,
            TooHeavy,
            Cancelled,
        }

        private static InventoryGui _boundGui;
        private static Container _boundContainer;
        private static ItemDrop.ItemData _selected;

        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null)
            {
                ResetState();
                return;
            }

            if (!ReferenceEquals(_boundGui, gui))
            {
                Rebind(gui);
            }

            Container container = gui.m_currentContainer;
            if (!ReferenceEquals(_boundContainer, container))
            {
                // A selection only makes sense for the container it was made in.
                _boundContainer = container;
                _selected = null;
            }

            if (container == null)
            {
                return;
            }
            if (
                InventoryGui.WaitForStack()
                || (gui.m_splitDialog != null && gui.m_splitDialog.IsActive)
            )
            {
                return;
            }
            if (
                Console.IsVisible()
                || TextInput.IsVisible()
                || (Chat.instance != null && Chat.instance.HasFocus())
            )
            {
                return;
            }

            if (!Restock.Instance.IsHotkeyPressed())
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting())
            {
                return;
            }

            try
            {
                Result result = RestockFromContainer(gui, player, container);
                if (result != Result.Moved && result != Result.Cancelled)
                {
                    string message = Message(result);
                    if (!string.IsNullOrEmpty(message))
                    {
                        player.Message(MessageHud.MessageType.Center, message);
                    }
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Restock failed: {ex}");
            }
        }

        private static Result RestockFromContainer(
            InventoryGui gui,
            Player player,
            Container container
        )
        {
            _selected = ResolveSelection(gui, player, container);
            if (_selected == null)
            {
                return Result.NothingSelected;
            }
            if (!container.IsOwner())
            {
                // Another peer owns the chest: writing into it would not be saved.
                Jotunn.Logger.LogWarning(
                    "Restock: container is not owned by this player, cancelled"
                );
                return Result.Cancelled;
            }

            Inventory playerInventory = player.GetInventory();
            Inventory containerInventory = container.GetInventory();
            if (playerInventory == null || containerInventory == null)
            {
                return Result.Cancelled;
            }

            // Snapshot: the container inventory is mutated while we walk through it.
            List<ItemDrop.ItemData> sources = containerInventory
                .GetAllItems()
                .FindAll(item => item != null && item.m_stack > 0 && item.IsSameType(_selected));
            if (sources.Count == 0)
            {
                return Result.NoMatchingItems;
            }

            int moved = 0;
            int sourceIndex = 0;
            bool outOfWeight = false;

            // 1) top up the stacks of the same item already carried by the player.
            foreach (
                ItemDrop.ItemData target in new List<ItemDrop.ItemData>(
                    playerInventory.GetAllItems()
                )
            )
            {
                if (outOfWeight || sourceIndex >= sources.Count)
                {
                    break;
                }
                if (!target.IsSameType(_selected))
                {
                    continue;
                }

                while (
                    sourceIndex < sources.Count && target.m_stack < target.m_shared.m_maxStackSize
                )
                {
                    ItemDrop.ItemData source = sources[sourceIndex];
                    int allowed = AllowedUnits(player, playerInventory, source);
                    if (allowed <= 0)
                    {
                        outOfWeight = true;
                        break;
                    }

                    int space = target.m_shared.m_maxStackSize - target.m_stack;
                    int amount = Math.Min(Math.Min(space, source.m_stack), allowed);
                    int transferred = Transfer(
                        playerInventory,
                        containerInventory,
                        source,
                        amount,
                        target.m_gridPos
                    );
                    if (transferred <= 0)
                    {
                        break;
                    }

                    moved += transferred;
                    if (source.m_stack <= 0)
                    {
                        sourceIndex++;
                    }
                }
            }

            // 2) then fill the empty slots.
            while (sourceIndex < sources.Count && !outOfWeight)
            {
                Vector2i slot = FindFreeSlot(playerInventory, _selected);
                if (slot.x < 0)
                {
                    break;
                }

                ItemDrop.ItemData source = sources[sourceIndex];
                int allowed = AllowedUnits(player, playerInventory, source);
                if (allowed <= 0)
                {
                    outOfWeight = true;
                    break;
                }

                int amount = Math.Min(
                    Math.Min(source.m_shared.m_maxStackSize, source.m_stack),
                    allowed
                );
                int transferred = Transfer(
                    playerInventory,
                    containerInventory,
                    source,
                    amount,
                    slot
                );
                if (transferred <= 0)
                {
                    break;
                }

                moved += transferred;
                if (source.m_stack <= 0)
                {
                    sourceIndex++;
                }
            }

            if (moved <= 0)
            {
                return outOfWeight ? Result.TooHeavy : Result.NoSpace;
            }

            ReleaseStaleDrag(gui);
            gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
            Jotunn.Logger.LogInfo(
                $"Restocked {moved} x {_selected.m_shared.m_name} "
                    + $"from '{containerInventory.GetName()}'"
            );
            if (Restock.Instance != null && Restock.Instance.DeselectAfterRestock.Value)
            {
                DropSelection(gui);
            }
            return Result.Moved;
        }

        /// <summary>
        ///     The item used as a template by this restock, following the configured
        ///     selection mode: the item under the mouse cursor, the item picked with a plain
        ///     left click, or the hovered one when there is one and the clicked one
        ///     otherwise. Returns null when neither mode has anything to offer.
        /// </summary>
        private static ItemDrop.ItemData ResolveSelection(
            InventoryGui gui,
            Player player,
            Container container
        )
        {
            Restock plugin = Restock.Instance;
            SelectionMode mode =
                plugin == null ? SelectionMode.Click : plugin.ItemSelectionMode.Value;
            if (mode == SelectionMode.Click)
            {
                return _selected;
            }

            ItemDrop.ItemData hovered = FindHoveredItem(
                gui.ContainerGrid,
                container.GetInventory()
            );
            if (hovered == null)
            {
                hovered = FindHoveredItem(gui.m_playerGrid, player.GetInventory());
            }
            if (hovered != null)
            {
                return hovered;
            }
            return mode == SelectionMode.Hover ? null : _selected;
        }

        /// <summary>
        ///     The item whose slot is under the mouse cursor, mirroring the private
        ///     InventoryGrid.GetHoveredElement() the vanilla highlight uses.
        /// </summary>
        private static ItemDrop.ItemData FindHoveredItem(InventoryGrid grid, Inventory inventory)
        {
            if (grid == null || inventory == null)
            {
                return null;
            }

            foreach (InventoryElement element in grid.m_elements)
            {
                if (element == null)
                {
                    continue;
                }

                RectTransform slot = element.GetElementRectTransform();
                Vector2 point = slot.InverseTransformPoint(ZInput.pointerPosition);
                if (!slot.rect.Contains(point))
                {
                    continue;
                }

                Vector2i pos = element.Position;
                return inventory.GetItemAt(pos.x, pos.y);
            }
            return null;
        }

        /// <summary>
        ///     How many units of <paramref name="item"/> may still be added without making
        ///     the player exceed the maximum carry weight.
        /// </summary>
        private static int AllowedUnits(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            float perUnit = item.GetWeight(1);
            if (perUnit <= 0f)
            {
                // Weightless items are only limited by the free slots.
                return int.MaxValue;
            }

            float maxWeight = player.GetMaxCarryWeight();
            float free = maxWeight - inventory.GetTotalWeight();
            if (free <= 0f)
            {
                return 0;
            }

            double raw = free / perUnit;
            if (raw >= int.MaxValue)
            {
                return int.MaxValue;
            }

            int units = (int)raw;
            while (units > 0 && inventory.GetTotalWeight() + perUnit * units > maxWeight)
            {
                units--;
            }
            return units;
        }

        /// <summary>
        ///     Moves part of a stack into the destination inventory and returns the amount
        ///     actually moved.
        /// </summary>
        private static int Transfer(
            Inventory to,
            Inventory from,
            ItemDrop.ItemData source,
            int amount,
            Vector2i pos
        )
        {
            if (amount <= 0)
            {
                return 0;
            }

            int before = source.m_stack;
            to.MoveItemToThis(from, source, amount, pos.x, pos.y);
            return Math.Max(0, before - source.m_stack);
        }

        /// <summary>Mirrors Inventory.FindEmptySlot(), which is private in the game.</summary>
        private static Vector2i FindFreeSlot(Inventory inventory, ItemDrop.ItemData selected)
        {
            bool topFirst =
                selected.IsWeapon()
                || selected.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool
                || selected.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield
                || selected.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility
                || selected.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Misc
                || selected.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trinket;
            int lastRow = inventory.GetHeight() - 1;

            for (int i = 0; i <= lastRow; i++)
            {
                int y = topFirst ? i : lastRow - i;
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    if (inventory.GetItemAt(x, y) == null)
                    {
                        return new Vector2i(x, y);
                    }
                }
            }
            return new Vector2i(-1, -1);
        }

        /// <summary>
        ///     Puts down what the cursor holds: as in vanilla, the item stays in its
        ///     inventory slot and only the drag visual is cleared.
        /// </summary>
        private static void DropSelection(InventoryGui gui)
        {
            _selected = null;
            ClearDrag(gui);
        }

        /// <summary>
        ///     Clears the drag state, replicating the clearing branch of the private
        ///     InventoryGui.SetupDragItem().
        /// </summary>
        private static void ClearDrag(InventoryGui gui)
        {
            if (gui.m_dragGo != null)
            {
                UnityEngine.Object.Destroy(gui.m_dragGo);
                gui.m_dragGo = null;
            }
            gui.m_dragItem = null;
            gui.m_dragInventory = null;
            gui.m_dragAmount = 0;
        }

        /// <summary>
        ///     Releases the item held by the cursor when it just left the inventory it was
        ///     picked up from, so no ghost item keeps following the mouse.
        /// </summary>
        private static void ReleaseStaleDrag(InventoryGui gui)
        {
            ItemDrop.ItemData dragItem = gui.m_dragItem;
            if (dragItem == null || gui.m_dragInventory == null)
            {
                return;
            }
            if (gui.m_dragInventory.ContainsItem(dragItem))
            {
                return;
            }

            ClearDrag(gui);
        }

        private static void Rebind(InventoryGui gui)
        {
            if (_boundGui != null && !ReferenceEquals(_boundGui, gui))
            {
                Unhook(_boundGui);
            }
            _boundGui = gui;
            _selected = null;
            Hook(gui);
        }

        private static void Hook(InventoryGui gui)
        {
            if (gui.m_playerGrid != null)
            {
                gui.m_playerGrid.m_onSelected -= OnItemSelected;
                gui.m_playerGrid.m_onSelected += OnItemSelected;
            }
            if (gui.ContainerGrid != null)
            {
                gui.ContainerGrid.m_onSelected -= OnItemSelected;
                gui.ContainerGrid.m_onSelected += OnItemSelected;
            }
        }

        private static void Unhook(InventoryGui gui)
        {
            if (gui.m_playerGrid != null)
            {
                gui.m_playerGrid.m_onSelected -= OnItemSelected;
            }
            if (gui.ContainerGrid != null)
            {
                gui.ContainerGrid.m_onSelected -= OnItemSelected;
            }
        }

        private static void ResetState()
        {
            if (_boundGui != null)
            {
                Unhook(_boundGui);
            }
            _boundGui = null;
            _boundContainer = null;
            _selected = null;
        }

        // Fired by InventoryGrid.OnLeftDown, before vanilla turns the click into a drag.
        private static void OnItemSelected(
            InventoryGrid grid,
            ItemDrop.ItemData item,
            Vector2i pos,
            InventoryGrid.Modifier modifier
        )
        {
            // Plain click only: shift splits the stack and ctrl moves the item at once.
            if (modifier != InventoryGrid.Modifier.Select)
            {
                return;
            }
            _selected = item;
        }

        private static string Message(Result result)
        {
            Restock plugin = Restock.Instance;
            if (plugin == null)
            {
                return string.Empty;
            }

            switch (result)
            {
                case Result.NothingSelected:
                    return plugin.MessageNothingSelected.Value;
                case Result.NoMatchingItems:
                    return plugin.MessageNoMatchingItems.Value;
                case Result.NoSpace:
                    return plugin.MessageNoSpace.Value;
                case Result.TooHeavy:
                    return plugin.MessageTooHeavy.Value;
                default:
                    return string.Empty;
            }
        }
    }
}
