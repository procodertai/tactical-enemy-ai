# Learn the foundation in small steps

The game work is here so your group can concentrate on learning AI later. Start with the playable loop and make one small change at a time. All course algorithms remain for the team to implement.

## Session 1: play and explore, without writing code

1. Complete Substation once using the running guide.
2. Stop Play mode and expand the scene **Hierarchy**.
3. Select **PLAYER** under **GAMEPLAY**. In the **Inspector**, find **Player Controller**. Change **Walk Speed** from 5 to 3, press Play, and feel the difference.
4. Stop Play and restore the value to 5.
5. Select the player again and inspect **Rifle**: 24-round magazine, 120 reserve, 25 damage, and 1.7-second reload.
6. Select an objective terminal. Its child objects are its model; the **Objective Terminal** component provides interaction behavior.
7. Select a crate or concrete barrier. Use Unity's Move tool to change its position. Save and play to see how cover changes the route. Keep paths wide enough for a player capsule.

This shows Unity's component model: one GameObject can combine Transform, collider, renderer and several C# behaviors. Geometry is ordinary scene content; it is not generated again every time you press Play.

## Session 2: read the smallest gameplay pieces

Read these in order, using the scripts under `Assets/TacticalGame/Scripts`:

| Script | What to learn |
| --- | --- |
| `Health.cs` | State, clamping values, and C# events. Damage changes health; a death event tells other components what happened. |
| `WorldInteractable.cs` | A small abstract interface: anything the player can use provides a prompt and an `Interact` method. |
| `ObjectiveTerminal.cs` | One interaction changes a terminal to completed, updates its visuals and notifies the mission. Repeat interaction has no effect. |
| `SupplyCrate.cs` | The same interaction mechanism can add ammo or heal. |
| `ExitGate.cs` | A door checks mission state and ends a round when both objectives are complete. |
| `GameSession.cs` | A round moves through Briefing, Playing, Paused, Won and Lost. It also owns the clock and restart. |

Try one exercise: change a terminal's **Display Name** in the Inspector and confirm that its mission text changes. Then change the rifle's damage to 20 and predict how many hits defeat a target before testing it.

## Session 3: understand movement and shooting

`PlayerController.cs` reads keyboard/mouse input and moves a **CharacterController**. The controller handles collision, while the script supplies horizontal movement and gravity. Looking rotates the player horizontally and the camera vertically.

`Rifle.cs` sends one ray from the camera through the centre crosshair. The first solid collider stops it. If that collider belongs to a living `Health` component, the rifle deals damage. This makes cover block shots using the same physical walls you can see. The view-model gun and the player's capsule use Unity's **Ignore Raycast** layer so the rifle cannot hit itself.

Reloading transfers only the missing rounds, bounded by available reserve. It does not create free ammo. The tracer, hit marker, muzzle light and sound provide feedback; the raycast determines the actual hit.

`GameHud.cs` draws menus and displays information. `GameAudio.cs` plays the template rifle/footstep clips and generates interface tones. `DamageZone.cs` is a fixed environmental hazard. `TargetDummy.cs` handles enemy damage visuals; the separate `BasicGuard.cs` makes the template characters turn and fire on a fixed timer. Read [the basic guard walkthrough](BASIC_GUARDS.md) before changing it.

Use the [CharacterController.Move documentation](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/charactercontroller/move) to connect the code to Unity's API.

## Where the later AI will connect

Each orange guard root has **Health**, **TargetDummy**, and the temporary **BasicGuard** component. Disable BasicGuard before adding the future tactical controller. Keep perception, decisions, navigation and coordination in their own scripts so you can understand and demonstrate each piece.

| Future course work | Existing foundation you can reuse |
| --- | --- |
| Perception | Walls have 3D colliders; player and target roots have transforms. You will implement viewing angle, sight raycasts and memory. |
| A* | Ground uses the XZ plane with metre-scale geometry. You will build the grid, clearance checks, search and movement execution. |
| Utility AI | Health is already available. Enemy ammo/actions and the scoring system still need to be written. |
| Squad coordination | Three target instances and named data-only destination markers are placed. You will implement roles, reports and destination reservations. |
| Enemy attacks | BasicGuard already exercises `Health.TakeDamage` with fixed-timer attacks. Replace its decision shortcut and retain a shared weapon/executor for your tactical controller. The player rifle reads human input and should not be attached directly as enemy AI. |

The **AI LESSON MARKERS** object contains `TacticalMarker` components. These are suggestions for positions, visible as Scene-view gizmos. They do not check whether a position is good cover, reachable, visible, or claimed. Verify and refine them when you implement positioning.

The basic guards refer directly to the player's position and check whether their shot is physically obstructed. This is a temporary gameplay shortcut, not a viewing-cone or memory-based perception system. There is no NavMeshAgent, A*, Utility AI scoring, shared memory, reinforcement learning, or network code to study around.

## Editing and sharing

Save new maps under different names before using the **Rebuild starter scenes** authoring command. That command replaces the starter scene files and assets. Normal scene editing does not require that command.

If you create a new map, add it to Build Profiles and update scene-selection code as needed; the supplied menu switches between the two provided scenes by name. The supplied prefabs are reusable starting points. Scene objects are independent editable copies, so editing a prefab does not automatically change the already-authored scene objects.

Commit `Assets` **with their `.meta` files**, `Packages`, `ProjectSettings`, scripts and docs. Do not commit `Library`, `Temp`, `Logs`, `Artifacts` or build outputs. Coordinate ownership of map scenes so two people do not edit the same `.unity` file simultaneously.

Finish the foundation walkthrough before beginning the first AI lesson. The existing `project-3-3d-plan.md` remains your broader roadmap; this starter covers its game foundation stage.
