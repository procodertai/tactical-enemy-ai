# Run the game, step by step

## 1. Install the project editor

Install Unity **6000.6.0f1** through Unity Hub. Include **Windows Build Support** to make a standalone executable. If Unity reports a missing Package Manager or build tool, repair that editor installation through Hub, then reopen the project.

## 2. Add this project to Unity Hub

1. Open **Unity Hub → Projects**.
2. Select **Add**, then **Add project from disk** (wording may differ slightly by Hub version).
3. Choose **`D:\Machine Learning Module\tactical-enemy-ai`**. It contains `Assets`, `Packages`, and `ProjectSettings`.
4. Select **6000.6.0f1** as its editor version, then open the project.
5. Wait for the progress indicator and script compilation to finish. The first import takes longer than later opens.

The required Multiplayer FPS template art is already included. Do not install its full DOTS/networking dependency stack to run this single-player adaptation. The project remains on the Built-in Render Pipeline; see `TEMPLATE_ASSETS.md` for exactly which template assets are used.

## 3. Open the first scene

In Unity's **Project** panel (usually at the bottom):

1. Expand **Assets → TacticalGame → Scenes**.
2. Double-click **Substation**.
3. Click the triangular **Play** button at the top of the editor.
4. Select the **Game** tab if Unity is showing the Scene tab.
5. Choose a **16:9** Game view or **Free Aspect**. A larger Game view makes the tutorial text easier to read.
6. Click **Begin Mission**. The game captures the mouse for first-person look.

The Play button runs a temporary instance inside Unity. Press the same button again to stop. Changes to objects during Play mode generally disappear when you stop, which is useful for experimentation.

## 4. Finish a first round

1. Use **WASD** to move. Move the mouse to look. **Shift** sprints; **Space** jumps.
2. Walk around the central transformer island. On Substation, the control room on the left contains **A / Main breaker**. The right maintenance area contains **B / Power relay**.
3. Get within about three metres of a terminal and look directly at its console. When the interaction prompt appears, press **E** once. Its screen and status text change to green.
4. Restore the other terminal. Order does not matter.
5. The exit is at the far north wall, opposite the starting position. Its red light changes to green when both terminals are online.
6. Walk close, look at the gate and press **E**. The extraction results screen appears.
7. Choose **Retry Mission** to reset, or **Play Rail Depot** to switch maps.

Defeating the orange armored guards is optional, but they can shoot you. Their warning lights give you time to move behind solid cover. Use **left click** to fire and hold **right click** to aim. Four hits disable a 100-health guard because the rifle does 25 damage. Use **R** to reload; ammunition transfers from reserve into the 24-round magazine. Orange supply boxes add reserve ammunition when you look at them and press **E**. Hold **T** when you need objective directions; floating markers are hidden during normal aiming.

Your health starts at 100. The **red floor plate with yellow stripes in the southeast corner** deals damage while you stand on it. It is an optional electrical hazard. The white medical kit nearby restores up to 50 health. A full-health player leaves the kit available.

**Tab** shows the level from above and freezes player movement, while the mission clock continues. Press **Tab** again to return. **Escape** pauses the clock and frees the pointer; the pause menu has sensitivity, restart, map selection, and quit controls. Switching away from the game also pauses it.

## 5. Run without the editor

With Unity 6000.6.0f1 and Windows Build Support installed, close the project in Unity and run this from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build-Windows.ps1
```

The command opens Unity in batch mode and publishes a verified build to `Builds/LastLight`. It preserves the previous build in a timestamped folder. If Unity is installed elsewhere, pass `-UnityExe "C:\path\to\Unity.exe"`. Build logs are written to `Logs`.

To build from the Unity editor:

1. Stop Play mode and save your scenes.
2. Select **Tactical Game → Build Windows game**.
3. Wait for the build to finish.
4. Open `Builds/LastLight/LastLight.exe`.

Keep the entire `Builds/LastLight` folder together, including the `LastLight_Data` directory and Unity DLLs. Copy the whole folder when sharing the playable game. The build packages the branch you have checked out. The output is ignored by Git because it can be regenerated.

On the briefing screen, press **F6** to choose Squad or Chase Only before starting the mission.

To watch live coordination, start Substation in Squad mode and press **F3**. Let a guard spot you long enough to report a sighting. The panel shows who reported it, each guard's decision, and any marker claim. Press **Tab** to follow blue A* routes and green claim lines from overhead. The overhead view suspends enemy fire. **F4** opens a separate example with sample reservations.

You can also use **File → Build Profiles**, choose Windows and check that **Substation** and **RailDepot** are both in the scene list. Substation should be first. Unity documents the build process in its [Unity 6000.6 build script guide](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/build-customize-build-pipeline/build-script-build).

If a compatibility executable exists at `Builds/Compatibility2021/LastLight.exe`, it is a separate convenience build made with **2021.3.45f2**. It is not a Unity 6000.6 build and does not replace testing in your requested editor.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Package Manager missing / editor immediately exits | Repair the incomplete editor installation described above. |
| Cannot move or mouse stays visible | Click Begin Mission, then click inside the Game view. Press Escape to resume if paused. |
| Mouse is trapped | Press Escape; in the editor you can also stop Play mode. |
| Input exception about active input handling | **Edit → Project Settings → Player → Other Settings → Active Input Handling** should be **Input Manager (Old)** or **Both**. Restart if prompted. This project intentionally uses built-in input with no downloaded input package. |
| Pink objects | Use the Built-in Render Pipeline. In Graphics settings leave the scriptable render pipeline asset empty, and check the Quality override too. Do not convert these Standard materials to URP just to run the starter. |
| Blank scene | Double-click `Assets/TacticalGame/Scenes/Substation.unity`. |
| No interaction prompt | Get closer and point at the actual terminal, gate, or supply box. Walls block interactions. |
| Gun does not fire | Begin/resume the round, return from overhead view, and reload with R if the magazine is empty. |
| Medical kit cannot be collected | Health is already full. The kit stays available for later. |
| Next map cannot load | Both scenes must be in Build Profiles / scene list; the supplied build tool sets them. |
| Build target not supported | Add Windows Build Support to the same editor installation in Unity Hub. |
| Guards stand in place | Guards hold until they know where to investigate or have a tactical position to move to. Press F3 to inspect live squad state. |
| Guards are too difficult | Increase Warning Seconds or Shot Interval on their Basic Guard components; see `BASIC_GUARDS.md`. |
| Red Console error | Read the first red error in the Console. Check Unity Hub for missing modules if the error names an editor tool. |

Unity calls this input API [Legacy Input](https://docs.unity.com/en-us/engine/6000.6/manual/input/legacy). The self-contained starter uses it to avoid an additional package and input-action setup; you can learn the new Input System separately later.
