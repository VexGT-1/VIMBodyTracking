using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Valve.VR;

namespace VIMBodyTracking
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log;

        // ── Config entries ──────────────────────────────────────────────────
        public static ConfigEntry<bool>  CfgEnabled;
        public static ConfigEntry<float> CfgSmoothingChest;
        public static ConfigEntry<float> CfgSmoothingHips;
        public static ConfigEntry<float> CfgSmoothingLegs;
        public static ConfigEntry<float> CfgOffsetChestY;
        public static ConfigEntry<float> CfgOffsetChestZ;
        public static ConfigEntry<float> CfgOffsetHipY;
        public static ConfigEntry<float> CfgOffsetHipZ;
        public static ConfigEntry<float> CfgOffsetLeftFootY;
        public static ConfigEntry<float> CfgOffsetRightFootY;
        public static ConfigEntry<KeyboardShortcut> CfgMenuKey;

        private Harmony _harmony;

        void Awake()
        {
            Instance = this;
            Log = Logger;

            // ── Bind config ─────────────────────────────────────────────────
            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master toggle for VIM Body Tracking");

            CfgSmoothingChest = Config.Bind("Smoothing", "ChestSmoothing", 8f,
                "Lerp speed for chest tracker (higher = snappier, 1-20)");
            CfgSmoothingHips = Config.Bind("Smoothing", "HipSmoothing", 8f,
                "Lerp speed for hip tracker");
            CfgSmoothingLegs = Config.Bind("Smoothing", "LegSmoothing", 10f,
                "Lerp speed for foot trackers");

            CfgOffsetChestY = Config.Bind("Offsets", "ChestOffsetY", 0f,
                "Vertical offset applied to chest tracker position");
            CfgOffsetChestZ = Config.Bind("Offsets", "ChestOffsetZ", 0f,
                "Forward/back offset applied to chest tracker");
            CfgOffsetHipY = Config.Bind("Offsets", "HipOffsetY", 0f,
                "Vertical offset applied to hip tracker");
            CfgOffsetHipZ = Config.Bind("Offsets", "HipOffsetZ", 0f,
                "Forward/back offset applied to hip tracker");
            CfgOffsetLeftFootY = Config.Bind("Offsets", "LeftFootOffsetY", 0f,
                "Vertical offset for left foot");
            CfgOffsetRightFootY = Config.Bind("Offsets", "RightFootOffsetY", 0f,
                "Vertical offset for right foot");

            CfgMenuKey = Config.Bind("Controls", "MenuKey",
                new KeyboardShortcut(KeyCode.B),
                "Key to open/close the in-game settings menu");

            // ── Harmony patches ─────────────────────────────────────────────
            _harmony = new Harmony(PluginInfo.GUID);
            _harmony.PatchAll();

            // ── Spawn manager ────────────────────────────────────────────────
            GameObject go = new GameObject("VIMBodyTrackingManager");
            DontDestroyOnLoad(go);
            go.AddComponent<BodyTrackingManager>();
            go.AddComponent<MenuController>();

            Log.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} loaded — press {CfgMenuKey.Value.MainKey} to open settings.");
        }

        void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    internal static class PluginInfo
    {
        public const string GUID    = "com.vex.vimbodytracking";
        public const string Name    = "VIM Body Tracking";
        public const string Version = "1.0.0";
    }
}
