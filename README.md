# Last Light — 3D game foundation

A single-player 3D first-person game for the Tactical Enemy AI course. **The main Unity project targets 6000.6.0f1.** Open this repository folder as the project; do not create another Unity project inside it.

Two editable industrial maps: **Substation** and **Rail Depot**. Restore two terminals in either order and interact with the exit before the six-minute clock expires. Play in first person, watch live guard coordination in the arena-wide 3D God View, or open the playable overhead map.

Each map has three guards with a viewing cone, hearing, and short-term memory. In Squad mode, guards patrol until they have information to act on, then use Utility AI to fire, reload, investigate, flank, seek cover, or fall back. Guards can search around a last known position and share confirmed sightings. A* follows the chosen route and reports outcomes to the squad coordinator, which manages roles and exclusive destination claims. Defeating the guards is optional. Completing the terminals and extracting is the mission.

The graphics now reuse **Unity Multiplayer FPS template 1.0.8**: rigged Armature characters and pose, assault rifles, modular environment meshes, PBR textures, and rifle/footstep sounds. Materials are adapted from the template's URP shaders to the starter's Built-in Render Pipeline. See [asset credits and adaptation details](Docs/TEMPLATE_ASSETS.md).

Start with [the step-by-step running guide](Docs/RUNNING.md), then [the short learning guide](Docs/LEARNING.md).

The original preview under `Builds/Compatibility2021` predates the integrated squad. Use the Windows build command below to package your current checkout.

## Quick start

1. In Unity Hub, add this folder with **Add → Add project from disk**: `D:\Machine Learning Module\tactical-enemy-ai`.
2. Open it using **Unity 6000.6.0f1** and wait for asset import.
3. Open `Assets/TacticalGame/Scenes/Substation.unity`.
4. Press the editor's **Play** button, then **Begin Mission** in the Game view.
5. Walk to cyan terminals **A** and **B**, look at each screen and press **E**. Then use **E** at the exit gate.

| Input | Action |
| --- | --- |
| WASD / arrow keys | Move |
| Mouse | Look in first person or aim the cursor in either external view |
| Left Shift | Sprint |
| Space | Jump |
| Left / right mouse | Fire / hold to aim in first person |
| R | Reload |
| E | Interact with the nearby object you are pointing at |
| Tab | Open or close the playable overhead map |
| V | Switch between first person and arena-wide 3D God View |
| F2 | Show Guard 01's Utility AI scores |
| F3 | Toggle the live squad panel |
| F4 | Open or close the isolated squad example while the panel is open |
| F5 | Toggle coordination while the panel is open |
| F6 | Choose Squad or Chase Only on the briefing screen |
| Hold T | Temporarily show objective markers |
| Escape | Pause / resume and release the mouse |

## Windows build

Close Unity for this project and run this command from the repository root in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build-Windows.ps1
```

The script uses Unity **6000.6.0f1** with Windows Build Support, builds the current checkout, and places the game in `Builds/LastLight`. It saves the previous build in a timestamped folder. Keep the entire game folder together when copying it. The editor menu **Tactical Game → Build Windows game** also builds both maps. See [running and troubleshooting](Docs/RUNNING.md).

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

Uses Unity's **Built-in Render Pipeline** and built-in **Input Manager**. The required template visuals are included under `Assets/ThirdParty/UnityMultiplayerFPS`; the game needs no networking, DOTS, NavMesh, or ML-Agents packages. Our code uses the repository license; the included Unity art retains its separate Unity Companion License.

## Editor tools

Coordination code lives under `Assets/TacticalGame/Scripts/Coordination`. Use **Tactical Game → Coordination → Run full coordination checks** to exercise the roster, shared sightings, roles, reservations, and movement feedback on both maps.

**Tactical Game → AI Evaluation → Run chase-only comparison** runs the same two scripted walking routes per map in Squad and Chase Only modes. It writes per-run and summary CSVs to `Artifacts/Comparison`.

The **Tactical Game** menu offers **Validate both maps**, **Run gameplay smoke checks**, and **Build Windows game**. Stop Play mode before running these tools. Save your scene changes before validation; validation opens both scenes. Gameplay checks temporarily run both rounds and write `Artifacts/gameplay-checks.txt`.

**Rebuild starter scenes** is an authoring tool that replaces the two generated scene files, starter materials, and prefabs. It shows a warning first. Save your own maps with new filenames before using it. You do not need to rebuild scenes to play.
