# Utility AI

In Substation and Rail Depot, all three guards use perception and squad reports to score combat and movement actions. The standalone one-guard demo is still available for inspecting Fire, Reload, and Hold in isolation.

## 1. Understand the scorer

`UtilityInputs` is the snapshot the decision needs. `Evaluate` reports which actions are available and how to compare them. The guard checks priority rules first, compares scores if no rule applies, and uses Hold as its fallback.

| Action | Available when | Priority or score |
| --- | --- | --- |
| Fire | Visible, in range, loaded, clear shot | 0.65 + 0.25 × health fraction |
| Reload | Magazine has space and reserve remains | Priority rule if empty. Otherwise 0.4 + 0.6 × missing magazine fraction if target hidden, or 0.2 + 0.5 × missing magazine fraction when visible. |
| Hold | No other action is selected | Fallback |
| Investigate | Can move, has a known position, and has no clear shot | 0.55 |
| Take cover | Reachable cover | 1.2 × lost health fraction |
| Flank | Assigned as flanker, a side position is available, and taking it is useful | 0.96 |
| Fall back | Available tactical movement and health at or below 30% | Priority rule before empty-magazine reload. The guard checks for a reachable destination when it tries to move. |
| Support | Assigned as support, a covered position is available, and no clear shot | 0.72 |

The last four tactical moves also require squad coordination and an available movement action. See `UtilityScorer.cs` for the exact conditions.

Health no longer feeds Reload. An earlier version added 0.45 × lost health to Reload, so a wounded guard with a clear shot stopped to reload in the open, which protects nothing. Visible Reload now peaks at 0.62, below Fire's 0.65 floor, so a guard with a shot fires until the magazine is empty. Out of sight, Reload beats Investigate (0.55) once a quarter of the magazine is missing.

Low health now pushes toward cover instead. Take cover is 1.2 × lost health. It passes Investigate below about 54% health, Fire below about 38% (at 30% health: cover 0.84 versus Fire 0.725), Support below 40%, and Flank below 20%. Fall back still takes priority at 30% when a retreat route exists.

Run **Tactical Game → Utility AI → Check decision scenarios**. Expect a PASS message in the Console. Priority rules can interrupt the current action at the next decision. For scored choices, a valid action stays for at least 0.5 seconds, then a challenger must win by more than 0.05. An active reload completes before a new action is selected.

## 2. Understand execution

`EnemyWeapon` owns six loaded rounds, 24 reserve rounds, reload timing, warning timing, shots and feedback. Reloading transfers missing ammunition after 1.7 seconds. Each shot consumes one round and checks the first physical collision again. Defaults preserve 8 damage, 19-metre range, a 1.2-second warning, and 1.6 seconds between shots.

`UtilityEnemyController` supplies state, scores every 0.2 seconds, selects an action and asks the weapon to execute it. Reload is committed until complete. Pause freezes its timers. Combat continues in God View and on the map. Death and round end stop combat. Restart reloads the scene and initializes state again.

## 3. Create and play the demo

1. Open the project with its target Unity 6000.6 editor and let scripts import.
2. Stop Play mode. Choose **Tactical Game → Utility AI → Create standalone demo**. Save any scene changes when Unity asks.
3. The command creates and opens `Assets/TacticalGame/Scenes/UtilityAiDemo.unity`, selects guard 01, disables baseline firing, and equips only guard 01 with Utility AI. Guard 01 keeps A* patrol and search. The command refuses to overwrite an existing demo. Later, open that scene directly.
4. Press Play and Begin Mission. Approach guard 01. The overlay on the right shows perception state, observations, ammunition, current action, scores and the last switch explanation. Press **F2** to show or hide it. In this standalone demo, press **G** to toggle player invulnerability.
5. Watch the magazine drain, Reload complete, and Fire resume. Duck behind a wall with a partial magazine: Fire becomes unavailable and Reload becomes attractive.
6. The standalone guard has no squad role, so it never takes cover; health only lowers its Fire score. Watch the health response on the regular maps, where a wounded guard with reachable cover leaves the fight. The deterministic decision checks cover the exact 50% and 30% health comparisons.

## 4. Verify gameplay in Unity

- Confirm the warning precedes the first shot and health drops by 8 per shot.
- Put solid cover between player and guard; confirm no damage through it.
- Check that reload fills only missing rounds and reduces reserve by the same amount. With Starting Reserve set to zero before Play, an empty enemy must Hold.
- Pause mid-warning and mid-reload. Resume and confirm the remaining delay is preserved. Open the map with Tab or switch to God View with V and confirm that warnings and reloads continue.
- Defeat the enemy; confirm shots stop. Restart and check full health, six rounds and 24 reserve with default settings.
- Let the round end; confirm combat stops. Use the gameplay smoke checks to verify the regular Substation and Rail Depot scenes.

## Tactical movement

`EnemyObservationProvider.Observe(range)` supplies visibility, aim and remembered observations from `GuardPerception`. The utility controller can use a fresher squad sighting when it lacks a current view. A shot still requires local visual confirmation and a clear physical ray.

Flankers seek reachable flank markers. Support guards seek cover when they lack a shot, and injured guards can seek cover or fall back. The coordinator grants one guard each marker. A* reports arrival, blockage, or failure and lets the controller try another marker if a claim is lost. **F2** displays the first guard's action scores. **F3** opens live roles, reports, and claims. **F4** shows sample reservations in an isolated overlay. **F5** toggles coordination so guards use local observations for investigation.

## Squad strategy demo

Open **Tactical Game → Utility AI → Open squad tactics demo**, press **Play**, then **Begin Mission**. The tool creates a local copy of Substation on first use. See the [demo plan](../plan.md) for the six strategies, route animations, assumptions, and controls.

The demo starts with the player controllable from the overhead camera. Move with **WASD**, aim with the mouse, and zoom with the scroll wheel or **U** and **J**. Switch cameras with **Tab** or **0** and **4**. Use **X** for visual contact, **H** to hide, **B** for a wounded guard, **N** for a sound report, and **O** to place or remove a barrier on an active route. **V** switches between watching and playing. Run **Tactical Game → Utility AI → Check squad strategies** to verify strategy switching and role assignment.

Press **M** to cycle Easy, Medium, and Hard. Medium is the initial default. The last choice is restored on retry. Your character takes damage while playing and has a visible animated body from spectator cameras. The scripted intruder remains protected while you watch.

For individual demo actions, an executable shelter or retreat order takes priority. After that, an empty magazine with reserve ammo triggers Reload. The remaining choices use scores, with Hold as the fallback.


Keep Health and TargetDummy on the guard. The controller disables BasicGuard in Awake as a safeguard; the demo also saves it disabled. Do not run another attack controller on the same enemy.

The editor creates the standalone demo scene on demand. The gameplay smoke checks exercise the integrated guards on both regular maps.

The experimental `SquadTactics` scorer evaluates Freestyle, Pin and Flank, Bounding Advance, and Covered Retreat. The **Check decision scenarios** menu tests its play selection and role assignment. The spectator camera prototype is available as source. A playable version of that experiment still requires the demo director and its scene wiring.
