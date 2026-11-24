# Temporary enemy attacks

`BasicGuard.cs` is a deliberately small combat baseline that you can replace in a later course lesson. It makes the new character models dangerous enough to test shooting, health, cover, and the mission loop.

Each guard stays at its spawn point. During normal first-person play it checks whether the player is within **19 metres**, and whether the first collider along its weapon ray belongs to the player. Solid geometry and other guards obstruct the shot. This direct player reference is a shortcut; it is not a proper perception or knowledge model.

When a clear shot becomes available, the guard turns toward the player and shows a pulsing warning light for **1.2 seconds**. It then fires **8 damage** every **1.6 seconds**. Losing the clear shot resets the warning. A dead guard stops attacking. Pause suspends attacks. Combat and the mission clock continue in God View and on the map.

The values are public Inspector fields on the **Basic Guard** component. To make your first walkthrough easier, increase **Warning Seconds** or **Shot Interval**. To return to harmless shooting practice, disable the Basic Guard component on all three enemies before entering Play mode.

There is no A*, movement planning, cover selection, Utility AI scoring, viewing cone, last-seen memory, destination reservation, role assignment, squad communication or reinforcement learning. The basic distance/raycast check exists only to allow a sensible, physically blocked attack; your course perception system will replace this shortcut.

The regular maps keep `Health` and the target's visual response. Their runtime bootstrap installs the Utility AI controller, which disables `BasicGuard` so one controller owns combat. The Chase Only mode uses the same runtime weapon, perception, and navigation as Squad mode.

World-space enemy names and decorative map text are hidden. Terminal status is attached to the console screen, and the exit has a small physical sign. Hold **T** for objective markers, press **Tab** for the overhead map, or press **V** for the angled 3D God View.
