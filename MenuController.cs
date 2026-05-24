using UnityEngine;

namespace VIMBodyTracking
{
    /// <summary>
    /// Renders an in-game IMGUI panel for tweaking all body tracking settings.
    /// Open/close with the configured key (default B).
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        private bool   _menuOpen;
        private Rect   _windowRect = new Rect(20, 20, 340, 580);
        private int    _tabIndex;  // 0=Status 1=Assign 2=Offsets 3=Smoothing

        private const float StepSmall = 0.05f;
        private const float StepBig   = 1f;

        private float _keyHoldTimer;
        private int   _keyPressCount;
        private float _keyResetTimer;

        void Update()
        {
            var menuKey = Plugin.CfgMenuKey.Value.MainKey;

            if (Input.GetKeyDown(menuKey))
            {
                _keyPressCount++;
                _keyResetTimer = 0f;
            }

            _keyResetTimer += Time.deltaTime;
            if (_keyResetTimer > 0.6f) _keyPressCount = 0;

            // Triple-press to open (matches community mod convention)
            if (_keyPressCount >= 3)
            {
                _menuOpen = !_menuOpen;
                _keyPressCount = 0;
            }
        }

        void OnGUI()
        {
            if (!_menuOpen) return;

            // ── Desktop-only guard ───────────────────────────────────────────
            // Unity calls OnGUI once per VR eye AND once for the desktop mirror
            // window.  Camera.current is null during the desktop pass; VR eye
            // passes always have a non-null stereo camera.  We skip every call
            // where a camera IS active (i.e. a VR eye), so the menu only draws
            // on the flat PC screen and never appears inside the headset.
            if (Camera.current != null) return;

            GUI.skin = null;
            _windowRect = GUI.Window(9001, _windowRect, DrawWindow, "🦍 VIM Body Tracking v1.0");
        }

        private void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 22));

            GUILayout.Space(4);

            // ── Master toggle ────────────────────────────────────────────────
            bool enabled = Plugin.CfgEnabled.Value;
            bool newEnabled = GUILayout.Toggle(enabled,
                enabled ? "  ✅ Tracking ENABLED" : "  ❌ Tracking DISABLED",
                GUILayout.Height(24));
            if (newEnabled != enabled) Plugin.CfgEnabled.Value = newEnabled;

            GUILayout.Space(6);

            // ── Tab bar ──────────────────────────────────────────────────────
            string[] tabs = { "Status", "Assign", "Offsets", "Smoothing" };
            _tabIndex = GUILayout.SelectionGrid(_tabIndex, tabs, 4, GUILayout.Height(26));

            GUILayout.Space(6);

            switch (_tabIndex)
            {
                case 0: DrawStatusTab();    break;
                case 1: DrawAssignTab();    break;
                case 2: DrawOffsetsTab();   break;
                case 3: DrawSmoothingTab(); break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("<size=9><color=#888>Press B×3 to close | Drop DLL in BepInEx/plugins</color></size>");
        }

        // ── TAB: Status ───────────────────────────────────────────────────────
        private void DrawStatusTab()
        {
            var mgr = BodyTrackingManager.Instance;
            if (mgr == null) { GUILayout.Label("Manager not loaded."); return; }

            GUILayout.Label("<b>Detected SteamVR Trackers</b>");
            GUILayout.Space(4);

            if (mgr.DetectedTrackers.Count == 0)
            {
                GUILayout.Label("  No trackers detected.\n  Make sure SteamVR trackers are on.");
            }
            else
            {
                foreach (var t in mgr.DetectedTrackers)
                {
                    string role = "None";
                    if (t.Index == mgr.ChestTrackerIndex)     role = "Chest";
                    else if (t.Index == mgr.HipTrackerIndex)  role = "Hip";
                    else if (t.Index == mgr.LeftFootTrackerIndex)  role = "Left Foot";
                    else if (t.Index == mgr.RightFootTrackerIndex) role = "Right Foot";

                    GUILayout.Label($"  [{t.Index}] {t.Serial}  →  <b>{role}</b>");
                }
            }

            GUILayout.Space(8);
            if (GUILayout.Button("🔄 Refresh Tracker List", GUILayout.Height(28)))
                mgr.RefreshTrackerList();
        }

        // ── TAB: Assign ───────────────────────────────────────────────────────
        private void DrawAssignTab()
        {
            var mgr = BodyTrackingManager.Instance;
            if (mgr == null) return;

            GUILayout.Label("<b>Auto-Assignment</b>");
            if (GUILayout.Button("⚡ Auto-Assign All Trackers", GUILayout.Height(30)))
                mgr.AutoAssignTrackers();

            GUILayout.Space(10);
            GUILayout.Label("<b>Manual Assignment</b>");
            GUILayout.Label("Select a tracker index for each body part:");
            GUILayout.Space(4);

            mgr.ChestTrackerIndex     = DrawTrackerSelector("Chest",      mgr.ChestTrackerIndex,     mgr);
            mgr.HipTrackerIndex       = DrawTrackerSelector("Hip",        mgr.HipTrackerIndex,       mgr);
            mgr.LeftFootTrackerIndex  = DrawTrackerSelector("Left Foot",  mgr.LeftFootTrackerIndex,  mgr);
            mgr.RightFootTrackerIndex = DrawTrackerSelector("Right Foot", mgr.RightFootTrackerIndex, mgr);

            GUILayout.Space(8);
            if (GUILayout.Button("🗑  Clear All Assignments", GUILayout.Height(26)))
            {
                mgr.ChestTrackerIndex = mgr.HipTrackerIndex =
                    mgr.LeftFootTrackerIndex = mgr.RightFootTrackerIndex = -1;
            }
        }

        private int DrawTrackerSelector(string label, int current, BodyTrackingManager mgr)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(80));

            if (GUILayout.Button("◀", GUILayout.Width(26)))
                current = Mathf.Max(-1, current - 1);

            string display = current < 0 ? "None" : $"[{current}]";
            if (current >= 0 && current < mgr.DetectedTrackers.Count)
                display += $" {mgr.DetectedTrackers[current].Serial}";

            GUILayout.Label(display, GUILayout.Width(140));

            if (GUILayout.Button("▶", GUILayout.Width(26)))
                current = Mathf.Min(mgr.DetectedTrackers.Count - 1, current + 1);

            GUILayout.EndHorizontal();
            return current;
        }

        // ── TAB: Offsets ──────────────────────────────────────────────────────
        private void DrawOffsetsTab()
        {
            GUILayout.Label("<b>Position Offsets  (use to fine-tune tracker placement)</b>");
            GUILayout.Space(6);

            DrawFloatControl("Chest  Y", Plugin.CfgOffsetChestY,  StepSmall);
            DrawFloatControl("Chest  Z", Plugin.CfgOffsetChestZ,  StepSmall);
            GUILayout.Space(4);
            DrawFloatControl("Hip    Y", Plugin.CfgOffsetHipY,    StepSmall);
            DrawFloatControl("Hip    Z", Plugin.CfgOffsetHipZ,    StepSmall);
            GUILayout.Space(4);
            DrawFloatControl("L Foot Y", Plugin.CfgOffsetLeftFootY,  StepSmall);
            DrawFloatControl("R Foot Y", Plugin.CfgOffsetRightFootY, StepSmall);

            GUILayout.Space(8);
            if (GUILayout.Button("↩  Reset All Offsets", GUILayout.Height(26)))
            {
                Plugin.CfgOffsetChestY.Value      = 0f;
                Plugin.CfgOffsetChestZ.Value      = 0f;
                Plugin.CfgOffsetHipY.Value        = 0f;
                Plugin.CfgOffsetHipZ.Value        = 0f;
                Plugin.CfgOffsetLeftFootY.Value   = 0f;
                Plugin.CfgOffsetRightFootY.Value  = 0f;
            }
        }

        // ── TAB: Smoothing ────────────────────────────────────────────────────
        private void DrawSmoothingTab()
        {
            GUILayout.Label("<b>Smoothing  (higher = snappier, lower = floatier)</b>");
            GUILayout.Space(6);

            DrawFloatControl("Chest",  Plugin.CfgSmoothingChest, StepBig, 1f, 20f);
            DrawFloatControl("Hips",   Plugin.CfgSmoothingHips,  StepBig, 1f, 20f);
            DrawFloatControl("Legs",   Plugin.CfgSmoothingLegs,  StepBig, 1f, 20f);

            GUILayout.Space(8);
            if (GUILayout.Button("↩  Reset Smoothing to Defaults", GUILayout.Height(26)))
            {
                Plugin.CfgSmoothingChest.Value = 8f;
                Plugin.CfgSmoothingHips.Value  = 8f;
                Plugin.CfgSmoothingLegs.Value  = 10f;
            }
        }

        // ── Generic float control row ─────────────────────────────────────────
        private static void DrawFloatControl(string label,
            BepInEx.Configuration.ConfigEntry<float> entry,
            float step, float min = -1f, float max = 1f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(80));

            if (GUILayout.Button("–", GUILayout.Width(26)))
                entry.Value = Mathf.Clamp(entry.Value - step, min, max);

            GUILayout.Label(entry.Value.ToString("F2"), GUILayout.Width(54));

            if (GUILayout.Button("+", GUILayout.Width(26)))
                entry.Value = Mathf.Clamp(entry.Value + step, min, max);

            GUILayout.EndHorizontal();
        }
    }
}
