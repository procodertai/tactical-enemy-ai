# Run the game, step by step

## 1. Repair the local Unity editor first

On this computer the installed **6000.6.0f1** editor fails before the project loads. Its log says:

> Could not find Unity Package Manager local server application

The missing file is `D:\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Resources\PackageManager\Server\UnityPackageManager.exe`. The `Data/Tools` and `Data/PlaybackEngines` directories are also missing. These are editor installation files, outside this repository. Changing scripts or downloading an art pack will not restore them.

1. Open **Unity Hub → Installs**.
2. Find **6000.6.0f1** and use a repair option if your Hub offers one. Otherwise uninstall and reinstall that editor version through Hub. Keep this project folder; removing an editor does not require deleting your project.
3. Include **Windows Build Support** if you want an executable. The editor is enough to use Play mode.
4. Sign in or activate a Unity Personal license through Hub if Unity asks. Do that yourself; the project needs no account credentials.

The main project is already set to your requested version. Do not open the main folder in Unity 2021 to work around this installation issue; that can downgrade project settings. A separate verification copy, if present, is under `Artifacts/CompatibilityUnity2021`.

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

Taking damage shows a red screen vignette (stronger and pulsing below 35% health), a red bar pointing toward the shooter for enemy fire, and a brief camera kick.

**Tab** shows the level from above and freezes player movement, while the mission clock continues. Press **Tab** again to return. **Escape** pauses the clock and frees the pointer; the pause menu has sensitivity, restart, map selection, and quit controls. Switching away from the game also pauses it.

## 5. Run without the editor

With a repaired editor and Windows build support installed:

1. Stop Play mode and save your scenes.
2. Select **Tactical Game → Build Windows game**.
3. Wait for the build to finish.
4. Open `Builds/LastLight/LastLight.exe`.

Keep the entire `Builds/LastLight` folder together, including the `LastLight_Data` directory and Unity DLLs. Copy the whole folder when sharing the playable game. The output is ignored by Git because it can be regenerated.

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
| Guards stand in place | Expected for this temporary baseline. They turn and fire when a clear shot is available; tactical movement comes later. |
| Guards are too difficult | Increase Warning Seconds or Shot Interval on their Basic Guard components; see `BASIC_GUARDS.md`. |
| Red Console error | Read the first red error. For this initial installation, check missing editor tools before changing the game code. |

Unity calls this input API [Legacy Input](https://docs.unity.com/en-us/engine/6000.6/manual/input/legacy). The self-contained starter uses it to avoid an additional package and input-action setup; you can learn the new Input System separately later.
