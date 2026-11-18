# Last Light — 3D game foundation

A single-player 3D first-person game for the Tactical Enemy AI course. **The main Unity project targets 6000.6.0f1.** Open this repository folder as the project; do not create another Unity project inside it.

Two editable industrial maps: **Substation** and **Rail Depot**. Restore two terminals in either order and interact with the exit before the six-minute clock expires. Includes walking, sprinting, jumping, mouse look, aiming, rifle fire, reloading, health, ammo and medical supplies, shooting feedback, synthesized sound, mission menus, pause, restart, and an overhead camera.

Each map has three guards with a viewing cone, hearing, and short-term memory. A confirmed sighting can be shared with the squad. Guards warn before firing and need a clear shot. In Substation, the temporary A* chase investigates a position the guard saw, heard, or learned from the squad. Defeating the guards is optional. Completing the terminals and extracting is the mission.

The graphics now reuse **Unity Multiplayer FPS template 1.0.8**: rigged Armature characters and pose, assault rifles, modular environment meshes, PBR textures, and rifle/footstep sounds. Materials are adapted from the template's URP shaders to the starter's Built-in Render Pipeline. See [asset credits and adaptation details](Docs/TEMPLATE_ASSETS.md).

Start with [the step-by-step running guide](Docs/RUNNING.md), then [the short learning guide](Docs/LEARNING.md).

A local playable preview is available at `Builds/Compatibility2021/LastLight.exe`. It was built and checked with the complete Unity 2021.3.45f2 editor while the requested Unity 6000.6 installation awaits repair. See [exact verification results and limits](Docs/VERIFICATION.md).

## Quick start

1. In Unity Hub, add this folder with **Add → Add project from disk**: `D:\Machine Learning Module\tactical-enemy-ai`.
2. Open it using **Unity 6000.6.0f1** and wait for asset import.
3. Open `Assets/TacticalGame/Scenes/Substation.unity`.
4. Press the editor's **Play** button, then **Begin Mission** in the Game view.
5. Walk to cyan terminals **A** and **B**, look at each screen and press **E**. Then use **E** at the exit gate.

| Input | Action |
| --- | --- |
| WASD / arrow keys | Move |
| Mouse | Look |
| Left Shift | Sprint |
| Space | Jump |
| Left / right mouse | Fire / hold to aim |
| R | Reload |
| E | Interact with the object you are looking at |
| Tab | Toggle overhead camera; the clock continues |
| Hold T | Temporarily show objective markers |
| Escape | Pause / resume and release the mouse |

## Local editor installation issue

The installed copy at `D:\Unity\Hub\Editor\6000.6.0f1` is incomplete: Unity reports that its Package Manager executable is missing, and its `Data/Tools` and Windows playback support folders are also absent. This prevents this editor from importing/compiling any project normally. Repair or reinstall **6000.6.0f1** through Unity Hub before using the quick start. See [running and troubleshooting](Docs/RUNNING.md).

The game source can be compiled separately against the installed Unity 6000.6.0f1 assemblies with `Tools/Check-UnityCompilation.ps1`. A separate Unity 2021.3 verification copy is kept under the ignored `Artifacts` directory for runtime checks; the main project remains on 6000.6.0f1. This does not establish a complete Unity 6000.6 editor or player test; repeat the supplied checks after repairing that installation.

## Project structure

| Location | Purpose |
| --- | --- |
| `Assets/TacticalGame/Scenes` | Two ordinary editable Unity scenes |
| `Assets/TacticalGame/Scripts` | Small gameplay components |
| `Assets/TacticalGame/Prefabs` | Player, basic guard, terminal, ammo and medical kit |
| `Assets/ThirdParty/UnityMultiplayerFPS` | Included template models, textures, materials, pose and sounds |
| `Assets/TacticalGame/Materials` | Shared built-in pipeline materials |
| `Assets/TacticalGame/Editor` | Scene-authoring, validation and build tools |
| `Docs` | Running instructions, learning steps and verification notes |
| `project-3-3d-plan.md` | Your longer course plan, preserved |

Uses Unity's **Built-in Render Pipeline** and built-in **Input Manager**. The required template visuals are included under `Assets/ThirdParty/UnityMultiplayerFPS`; the game needs no networking, DOTS, NavMesh, or ML-Agents packages. Our code uses the repository license; the included Unity art retains its separate Unity Companion License. This is the game foundation and a temporary combat baseline, not the final tactical AI submission.

## Editor tools

Coordination code lives under `Assets/TacticalGame/Scripts/Coordination`. Use **Tactical Game → Coordination → Run full coordination checks** to exercise the roster, shared sightings, roles, reservations, and movement feedback on both maps.

The **Tactical Game** menu offers **Validate both maps**, **Run gameplay smoke checks**, and **Build Windows game**. Stop Play mode before running these tools. Save your scene changes before validation; validation opens both scenes. Gameplay checks temporarily run both rounds and write `Artifacts/gameplay-checks.txt`.

**Rebuild starter scenes** is an authoring tool that replaces the two generated scene files, starter materials, and prefabs. It shows a warning first. Save your own maps with new filenames before using it. You do not need to rebuild scenes to play.
