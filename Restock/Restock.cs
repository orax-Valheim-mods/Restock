using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Restock
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    internal class Restock : BaseUnityPlugin
    {
        public const string PluginGUID = "orax.Restock";
        public const string PluginName = "Restock";
        public const string PluginVersion = "0.1.0";

        private const string DefaultHotkey = "Shift + R";

        // Available for future localization of the feedback messages.
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization { get; private set; }

        internal static Restock Instance { get; private set; }

        private const string MessageHint = " Leave it empty to disable the message.";

        /// <summary>Configured key combination, e.g. "Shift + R" or "B".</summary>
        internal ConfigEntry<string> Hotkey { get; private set; }

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

        /// <summary>
        ///     Parsed combination: the last entry is the key to press, the entries before
        ///     it must be held. Swapped as a whole whenever the setting changes.
        /// </summary>
        private KeyCode[] _hotkey = new KeyCode[0];

        /// <summary>Spellings accepted in the configuration besides the KeyCode names.</summary>
        private static readonly Dictionary<string, KeyCode> HotkeyAliases = new Dictionary<
            string,
            KeyCode
        >(StringComparer.OrdinalIgnoreCase)
        {
            { "shift", KeyCode.LeftShift },
            { "lshift", KeyCode.LeftShift },
            { "rshift", KeyCode.RightShift },
            { "ctrl", KeyCode.LeftControl },
            { "control", KeyCode.LeftControl },
            { "lctrl", KeyCode.LeftControl },
            { "rctrl", KeyCode.RightControl },
            { "alt", KeyCode.LeftAlt },
            { "lalt", KeyCode.LeftAlt },
            { "ralt", KeyCode.RightAlt },
            { "win", KeyCode.LeftWindows },
            { "windows", KeyCode.LeftWindows },
            { "meta", KeyCode.LeftWindows },
            { "esc", KeyCode.Escape },
            { "enter", KeyCode.Return },
            { "del", KeyCode.Delete },
            { "pgup", KeyCode.PageUp },
            { "pgdn", KeyCode.PageDown },
        };

        private void Awake()
        {
            Instance = this;
            Localization = LocalizationManager.Instance.GetLocalization();

            Hotkey = Config.Bind(
                "Input",
                "Hotkey",
                DefaultHotkey,
                "Key combination pressed while a container is open to pull the items "
                    + "matching the selected one into the player inventory. Write the keys "
                    + "joined with '+' in any order; the key that is not a modifier "
                    + "(Shift, Ctrl, Alt, Win) is the one to press. 'None' disables it. "
                    + "Examples: Shift + R, LeftControl + B, B."
            );

            Hotkey.SettingChanged += (sender, args) => ParseHotkey();
            ParseHotkey();

            DeselectAfterRestock = Config.Bind(
                "Behaviour",
                "DeselectAfterRestock",
                true,
                "Put the selection down after a restock: the item stays where it was and "
                    + "the next key press needs a new selection. Set it to false to keep "
                    + "the selection and press the key again right away."
            );

            MessageNothingSelected = Config.Bind(
                "Messages",
                "NothingSelected",
                "Restock: select an item first",
                "Shown when the hotkey is pressed without a selected item." + MessageHint
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
                $"{PluginName} {PluginVersion} loaded (hotkey: {DescribeHotkey()})"
            );
        }

        /// <summary>
        ///     True on the frame the configured combination is pressed: the main key must
        ///     just have been pressed while every other key of the combination is held.
        /// </summary>
        internal bool IsHotkeyPressed()
        {
            KeyCode[] hotkey = _hotkey;
            if (hotkey.Length == 0)
            {
                return false;
            }

            KeyCode mainKey = hotkey[hotkey.Length - 1];
            if (!ZInput.GetKeyDown(mainKey, logWarning: false))
            {
                return false;
            }

            for (int i = 0; i < hotkey.Length - 1; i++)
            {
                if (!ZInput.GetKey(hotkey[i], logWarning: false))
                {
                    return false;
                }
            }
            return true;
        }

        private void ParseHotkey()
        {
            KeyCode[] hotkey = ParseKeyCombination(Hotkey.Value);
            if (hotkey == null)
            {
                Jotunn.Logger.LogWarning(
                    $"Invalid hotkey \"{Hotkey.Value}\", using \"{DefaultHotkey}\" instead. "
                        + "Key names such as Shift, LeftControl, A or F5 are joined with '+'."
                );
                hotkey = ParseKeyCombination(DefaultHotkey);
            }
            _hotkey = hotkey ?? new KeyCode[0];
        }

        /// <summary>
        ///     Parses a combination like "Shift + R" into an array whose last entry is the
        ///     key to press, or an empty array when the hotkey is disabled. Returns null
        ///     when the value cannot be understood.
        /// </summary>
        private static KeyCode[] ParseKeyCombination(string value)
        {
            if (
                string.IsNullOrWhiteSpace(value)
                || value.Trim().Equals("None", StringComparison.OrdinalIgnoreCase)
            )
            {
                return new KeyCode[0];
            }

            List<KeyCode> keys = new List<KeyCode>();
            string[] tokens = value.Split(
                new[] { '+', ',', ';', '|', ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            );

            foreach (string token in tokens)
            {
                KeyCode key = ParseKey(token);
                if (key == KeyCode.None)
                {
                    return null;
                }
                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            if (keys.Count == 0)
            {
                return new KeyCode[0];
            }

            // Whatever the order in the configuration, the key that is not a modifier is
            // the one the player presses: "Shift + R" and "R + Shift" behave the same.
            int mainIndex = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                if (!IsModifierKey(keys[i]))
                {
                    mainIndex = i;
                    break;
                }
            }

            List<KeyCode> combination = new List<KeyCode>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                if (i != mainIndex)
                {
                    combination.Add(keys[i]);
                }
            }
            combination.Add(keys[mainIndex]);
            return combination.ToArray();
        }

        private static KeyCode ParseKey(string token)
        {
            if (HotkeyAliases.TryGetValue(token, out KeyCode key))
            {
                return key;
            }

            // The digit keys are written "1", while their KeyCode name is "Alpha1".
            if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
            {
                token = "Alpha" + token;
            }

            KeyCode parsed;
            if (Enum.TryParse(token, true, out parsed) && Enum.IsDefined(typeof(KeyCode), parsed))
            {
                return parsed;
            }
            return KeyCode.None;
        }

        private static bool IsModifierKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                case KeyCode.LeftWindows:
                case KeyCode.RightWindows:
                case KeyCode.AltGr:
                    return true;
                default:
                    return false;
            }
        }

        private string DescribeHotkey()
        {
            if (_hotkey.Length == 0)
            {
                return "none";
            }

            string[] names = new string[_hotkey.Length];
            for (int i = 0; i < _hotkey.Length; i++)
            {
                names[i] = _hotkey[i].ToString();
            }
            return string.Join(" + ", names);
        }

        private void Update()
        {
            Restocker.Update();
        }
    }
}
