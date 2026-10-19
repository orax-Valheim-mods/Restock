using BepInEx;
using BepInEx.Configuration;
using Jotunn.Configs;
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

        // Available for future localization of the feedback messages.
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization { get; private set; }

        internal static Restock Instance { get; private set; }

        /// <summary>Hotkey button, registered through Jotunn so it can be rebound.</summary>
        internal ButtonConfig RestockButton { get; private set; }

        private void Awake()
        {
            Instance = this;
            Localization = LocalizationManager.Instance.GetLocalization();

            ConfigEntry<KeyCode> hotkey = Config.Bind(
                "Input",
                "Hotkey",
                KeyCode.B,
                "Key pressed while a container is open to pull the items matching the "
                    + "selected one (plain left click) into the player inventory."
            );

            // ActiveInGUI is required: without it Jotunn refuses the input while a
            // game GUI such as the inventory is open.
            RestockButton = new ButtonConfig
            {
                Name = "Restock",
                Config = hotkey,
                ActiveInGUI = true,
            };
            InputManager.Instance.AddButton(PluginGUID, RestockButton);

            Jotunn.Logger.LogInfo($"{PluginName} {PluginVersion} loaded (hotkey: {hotkey.Value})");
        }

        private void Update()
        {
            Restocker.Update();
        }
    }
}
