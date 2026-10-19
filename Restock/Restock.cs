using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Restock
{
    /// <summary>How the item used as a template by the restock is picked.</summary>
    internal enum SelectionMode
    {
        /// <summary>The item selected with a plain left click.</summary>
        Click,

        /// <summary>The item under the mouse cursor when the key is pressed.</summary>
        Hover,

        /// <summary>The hovered item when there is one, otherwise the clicked one.</summary>
        Both,
    }

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    internal class Restock : BaseUnityPlugin
    {
        public const string PluginGUID = "orax.Restock";
        public const string PluginName = "Restock";
        public const string PluginVersion = "0.1.0";

        private const string MessageHint = " Leave it empty to disable the message.";

        // Available for future localization of the feedback messages.
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization { get; private set; }

        internal static Restock Instance { get; private set; }

        /// <summary>Key combination triggering the restock, e.g. R + LeftShift.</summary>
        internal ConfigEntry<KeyboardShortcut> Hotkey { get; private set; }

        /// <summary>Center message shown when nothing is selected.</summary>
        internal ConfigEntry<string> MessageNothingSelected { get; private set; }

        /// <summary>Center message shown when the container holds no matching item.</summary>
        internal ConfigEntry<string> MessageNoMatchingItems { get; private set; }

        /// <summary>Center message shown when the transfer stops on a full inventory.</summary>
        internal ConfigEntry<string> MessageNoSpace { get; private set; }

        /// <summary>Center message shown when the transfer stops on the weight limit.</summary>
        internal ConfigEntry<string> MessageTooHeavy { get; private set; }

        /// <summary>Whether the selection is put down once the restock succeeded.</summary>
        internal ConfigEntry<bool> DeselectAfterRestock { get; private set; }

        /// <summary>How the item to restock is picked: click, hover or both.</summary>
        internal ConfigEntry<SelectionMode> ItemSelectionMode { get; private set; }

        private void Awake()
        {
            Instance = this;
            Localization = LocalizationManager.Instance.GetLocalization();

            Hotkey = Config.Bind(
                "Input",
                "Hotkey",
                new KeyboardShortcut(KeyCode.R, KeyCode.LeftShift),
                "Key combination pressed while a container is open to pull the items "
                    + "matching the selected one into the player inventory. The main key "
                    + "is written first and the modifiers after it: R + LeftShift is the "
                    + "combination Shift + R. A single key (B) works too, and None "
                    + "disables the hotkey."
            );

            DeselectAfterRestock = Config.Bind(
                "Behaviour",
                "DeselectAfterRestock",
                true,
                "Put the selection down after a restock: the item stays where it was and "
                    + "the next key press needs a new selection. Set it to false to keep "
                    + "the selection and press the key again right away."
            );

            ItemSelectionMode = Config.Bind(
                "Behaviour",
                "SelectionMode",
                SelectionMode.Both,
                "How the item to restock is picked. Click: the item selected with a plain "
                    + "left click. Hover: the item under the mouse cursor when the key is "
                    + "pressed, no click needed. Both: the hovered item when there is one, "
                    + "otherwise the clicked one."
            );

            MessageNothingSelected = Config.Bind(
                "Messages",
                "NothingSelected",
                "Restock: select an item first",
                "Shown when the hotkey is pressed without a hovered or selected item." + MessageHint
            );
            MessageNoMatchingItems = Config.Bind(
                "Messages",
                "NoMatchingItems",
                "Restock: no matching items in the container",
                "Shown when the container holds no item identical to the selected one."
                    + MessageHint
            );
            MessageNoSpace = Config.Bind(
                "Messages",
                "NoSpace",
                "Restock: not enough room in your inventory",
                "Shown when the transfer stops because your inventory has no room left."
                    + MessageHint
            );
            MessageTooHeavy = Config.Bind(
                "Messages",
                "TooHeavy",
                "Restock: that would make you overburdened",
                "Shown when the transfer stops because it would exceed your maximum "
                    + "carry weight."
                    + MessageHint
            );

            Jotunn.Logger.LogInfo(
                $"{PluginName} {PluginVersion} loaded (hotkey: {Hotkey.Value.Serialize()})"
            );
        }

        /// <summary>
        ///     True on the frame the configured combination is pressed. BepInEx requires
        ///     the main key to be pressed while the modifiers are held, and no other key
        ///     to be pressed at the same time.
        /// </summary>
        internal bool IsHotkeyPressed()
        {
            return Hotkey.Value.IsDown();
        }

        private void Update()
        {
            Restocker.Update();
        }
    }
}
