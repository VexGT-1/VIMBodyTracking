using UnityEngine;

namespace VIMBodyTracking
{
    public class MenuController : MonoBehaviour
    {
        private bool _menuOpen;
        private Rect _windowRect = new Rect(20, 20, 320, 380);
        private int _tabIndex;
        private int _keyPressCount;
        private float _keyResetTimer;

        void Update()
        {
            if (Input.GetKeyDown(Plugin.CfgMenuKey.Value.MainKey)) { _keyPressCount++; _keyResetTimer = 0f; }
            _keyResetTimer += Time.deltaTime;
            if (_keyResetTimer > 0.6f) _keyPressCount = 0;
            if (_keyPressCount >= 3) { _menuOpen = !_menuOpen; _keyPressCount = 0; }
        }

        void OnGUI()
        {
            if (!_menuOpen) return;
            if (Camera.current != null) return;
            _windowRect = GUI.Window(9001, _windowRect, DrawWindow, "VIM Body Tracking");
        }

        private void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 22));
            GUILayout.Space(4);

            bool en = Plugin.CfgEnabled.Value;
            bool newEn = GUILayout.Toggle(en, en ? "Tracking ENABLED" : "Tracking DISABLED", GUILayout.Height(26));
            if (newEn != en) Plugin.CfgEnabled.Value = newEn;

            GUILayout.Space(6);
            string[] tabs = { "Trackers", "Offsets", "Smoothing" };
            _tabIndex = GUILayout.SelectionGrid(_tabIndex, tabs, 3, GUILayout.Height(26));
            GUILayout.Space(6);

            if (_tabIndex == 0) DrawTrackersTab();
            else if (_tabIndex == 1) DrawOffsetsTab();
            else DrawSmoothingTab();

            GUILayout.FlexibleSpace();
            GUILayout.Label("Press B x3 to close");
        }

        private void DrawTrackersTab()
        {
            var mgr = BodyTrackingManager.Instance;
            if (mgr == null) { GUILayout.Label("Manager not loaded."); return; }

            GUILayout.Label("Chest Tracker (controls body rotation)");
            GUILayout.Space(4);

            if (mgr.DetectedTrackers.Count == 0)
                GUILayout.Label("No trackers found. Turn on SteamVR trackers.");
            else
                foreach (var t in mgr.DetectedTrackers)
                    GUILayout.Label($"[{t.Index}] {t.Serial} {(t.Index == mgr.ChestTrackerIndex ? "<- CHEST" : "")}");

            GUILayout.Space(8);
            if (GUILayout.Button("Auto-Assign Chest Tracker", GUILayout.Height(28)))
                mgr.AutoAssignTracker();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Manual:", GUILayout.Width(60));
            if (GUILayout.Button("<", GUILayout.Width(28))) mgr.ChestTrackerIndex = Mathf.Max(-1, mgr.ChestTrackerIndex - 1);
            GUILayout.Label(mgr.ChestTrackerIndex < 0 ? "None" : $"[{mgr.ChestTrackerIndex}]", GUILayout.Width(50));
            if (GUILayout.Button(">", GUILayout.Width(28))) mgr.ChestTrackerIndex = Mathf.Min(mgr.DetectedTrackers.Count - 1, mgr.ChestTrackerIndex + 1);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Refresh Trackers", GUILayout.Height(26))) mgr.RefreshTrackerList();
        }

        private void DrawOffsetsTab()
        {
            GUILayout.Label("Chest Tracker Offsets");
            GUILayout.Space(6);
            DrawFloatControl("Chest Y", Plugin.CfgOffsetChestY, 0.05f, -1f, 1f);
            DrawFloatControl("Chest Z", Plugin.CfgOffsetChestZ, 0.05f, -1f, 1f);
            GUILayout.Space(8);
            if (GUILayout.Button("Reset Offsets", GUILayout.Height(26))) { Plugin.CfgOffsetChestY.Value = 0f; Plugin.CfgOffsetChestZ.Value = 0f; }
        }

        private void DrawSmoothingTab()
        {
            GUILayout.Label("Smoothing (higher = snappier)");
            GUILayout.Space(6);
            DrawFloatControl("Chest", Plugin.CfgSmoothingChest, 1f, 1f, 20f);
            GUILayout.Space(8);
            if (GUILayout.Button("Reset Smoothing", GUILayout.Height(26))) Plugin.CfgSmoothingChest.Value = 8f;
        }

        private static void DrawFloatControl(string label, BepInEx.Configuration.ConfigEntry<float> entry, float step, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(70));
            if (GUILayout.Button("-", GUILayout.Width(26))) entry.Value = Mathf.Clamp(entry.Value - step, min, max);
            GUILayout.Label(entry.Value.ToString("F2"), GUILayout.Width(50));
            if (GUILayout.Button("+", GUILayout.Width(26))) entry.Value = Mathf.Clamp(entry.Value + step, min, max);
            GUILayout.EndHorizontal();
        }
    }
}
