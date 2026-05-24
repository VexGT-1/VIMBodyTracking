using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VIMBodyTracking
{
    [BepInPlugin("com.vex.vimbodytracking", "VIM Body Tracking", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log;

        public static ConfigEntry<bool> CfgEnabled;
        public static ConfigEntry<float> CfgSmoothingChest;
        public static ConfigEntry<float> CfgOffsetChestY;
        public static ConfigEntry<float> CfgOffsetChestZ;
        public static ConfigEntry<KeyboardShortcut> CfgMenuKey;

        private Harmony _harmony;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            CfgEnabled = Config.Bind("General", "Enabled", true, "Master toggle");
            CfgSmoothingChest = Config.Bind("Smoothing", "ChestSmoothing", 8f, "Chest lerp speed 1-20");
            CfgOffsetChestY = Config.Bind("Offsets", "ChestOffsetY", 0f, "Chest Y offset");
            CfgOffsetChestZ = Config.Bind("Offsets", "ChestOffsetZ", 0f, "Chest Z offset");
            CfgMenuKey = Config.Bind("Controls", "MenuKey", new KeyboardShortcut(KeyCode.B), "Menu key");

            _harmony = new Harmony("com.vex.vimbodytracking");
            _harmony.PatchAll();

            GameObject go = new GameObject("VIMBodyTrackingManager");
            DontDestroyOnLoad(go);
            go.AddComponent<BodyTrackingManager>();
            go.AddComponent<MenuController>();

            Log.LogInfo("VIM Body Tracking loaded! Press B x3 to open menu.");
        }

        void OnDestroy() { _harmony?.UnpatchSelf(); }
    }
}
