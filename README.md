# 🦍 VIM Body Tracking — BepInEx Mod for Gorilla Tag

Replicates the **V.I.M subscription body tracking** feature as a PCVR BepInEx mod.
Drives your gorilla avatar's chest, hips, and feet using SteamVR-compatible trackers
(Vive Trackers, Tundra Trackers, SlimeVR, etc.).

---

## What This Does

| VIM Subscription Feature | This Mod |
|---|---|
| Full body tracking (Quest 3 / PCVR) | ✅ PCVR with SteamVR trackers |
| Chest / body orientation | ✅ Chest tracker → spine bone |
| Hip tracking | ✅ Hip tracker → hip bone |
| Foot tracking | ✅ Left + Right foot trackers |
| In-game configuration | ✅ IMGUI menu (press B×3) |
| Network sync to other players | ⚠️ Local avatar only (no Photon hook yet) |

---

## Requirements

- Gorilla Tag on **Steam / PCVR** (not standalone Quest)
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) installed for Gorilla Tag
- At least **1 SteamVR tracker** (chest); 2 for chest+hip; 4 for full body
- Compatible trackers: Vive Tracker 2.0/3.0, Tundra Tracker, SlimeVR (with SteamVR bridge)

---

## Build Instructions

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (Community is free)
  - Workload: **.NET desktop development**
- .NET Framework 4.6 targeting pack (installed via Visual Studio)

### Steps

1. **Open the solution**
   - Double-click `VIMBodyTracking.sln`

2. **Set your game path**
   - Open `VIMBodyTracking/VIMBodyTracking.csproj`
   - Find this line and update it to your actual Gorilla Tag folder:
     ```xml
     <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag</GamePath>
     ```

3. **Build**
   - In Visual Studio: **Build → Build Solution** (or press `Ctrl+Shift+B`)
   - The `VIMBodyTracking.dll` will be placed automatically into:
     `<GamePath>\BepInEx\plugins\VIMBodyTracking\VIMBodyTracking.dll`

4. **Launch Gorilla Tag** via SteamVR with your trackers powered on.

---

## In-Game Menu

Press **B three times quickly** to open the settings panel.

### Status Tab
- Shows all detected SteamVR trackers with their assigned roles.
- **Refresh Tracker List** — re-scans for trackers.

### Assign Tab
- **Auto-Assign** — automatically assigns trackers by height (highest = chest, etc.)
  and X position (left/right foot).
- **Manual Assignment** — use ◀ ▶ arrows to pick a tracker index for each body part.
- **Clear All** — removes all assignments.

### Offsets Tab
Buttons to nudge each tracker's position in Unity units (≈ 1 unit = 1 metre):

| Control | Effect |
|---|---|
| Chest Y | Move chest tracker up/down |
| Chest Z | Move chest tracker forward/back |
| Hip Y | Move hip tracker up/down |
| Hip Z | Move hip tracker forward/back |
| L/R Foot Y | Move foot tracker up/down |

Press **Reset All Offsets** to return everything to zero.

### Smoothing Tab
Controls lerp speed per body part (range 1–20):

- **Higher** = snappier, more responsive, but may feel twitchy
- **Lower** = floatier, more lag, but smoother looking

---

## Config File

Settings are also saved to:
`BepInEx\config\com.vex.vimbodytracking.cfg`

You can edit this file in a text editor while the game is closed.

---

## Tracker Placement Tips

| Tracker | Wear it on |
|---|---|
| Chest | Sternum / upper chest strap |
| Hip | Lower back / belt |
| Left Foot | Top of left shoe or ankle strap |
| Right Foot | Top of right shoe or ankle strap |

---

## Troubleshooting

**"No trackers detected"**
- Make sure SteamVR is running and trackers are powered on *before* launching Gorilla Tag.
- Check SteamVR's device list to confirm trackers are visible to the system.

**Avatar bones not moving**
- The mod searches for `Local VRRig` in the scene. If Gorilla Tag updates and renames
  this object, the bone lookup will fail. Check BepInEx logs for messages about bones.

**Game crashes on startup**
- Verify your `<GamePath>` in the `.csproj` points to the correct folder.
- Make sure all DLL references resolve (no yellow triangles in Visual Studio).

---

## Notes

- This mod only affects **your local avatar**. Other players see the default animation
  unless they also install this mod (network sync is a future feature).
- Use only in **private / modded lobbies** to avoid bans.
- Tested with BepInEx 5.4.x and Gorilla Tag build as of May 2026.

---

## Credits

Inspired by the GorillaBody / GorillaTrack open-source mods.
Built for the Gorilla Tag PCVR modding community.
