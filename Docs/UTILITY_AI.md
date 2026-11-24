# Utility AI: first combat milestone

One stationary guard chooses Fire, Reload, or Hold. Perception, navigation and squad coordination remain future integrations.

## 1. Understand the scorer

Read `Assets/TacticalGame/Scripts/UtilityAI/UtilityScorer.cs` first. `UtilityInputs` is the snapshot the decision needs; `Evaluate` produces availability, scores and explanations without scene access. An unavailable action cannot win. Scores use these rules:

| Action | Available when | Score |
| --- | --- | --- |
| Fire | Visible, in range, loaded | 0.65 + 0.25 × health fraction |
| Reload | Magazine not full and reserve remains | 1 if empty; 0.95 if target hidden; otherwise 0.2 + 0.5 × missing magazine fraction + 0.45 × lost health fraction |
| Hold | Always | 0.1 |
| Chase | Can move, player position known (seen or remembered), ammo loaded, player hidden or out of range | 0.35 + 0.45 × health fraction |
| Retreat | Can move, player position known, a cover marker was found | 0.90 × (1 − health)² + 0.30 × under-fire |

Movement inputs: `KnowsTarget` (observed distance is finite), `CanMove` (a `PathMover` exists), `HasCover` (`Positioning.FindCover` returned a marker, re-queried at most once per second) and `UnderFire` (1 right after the guard is hit, fading linearly to 0 over 3 s). Outcomes: full health under fire, Fire 0.90 beats Retreat 0.30; 25% health just hit with 3/6 rounds, Retreat 0.81 beats Reload 0.79 and Fire 0.71; same but not hit, Reload 0.79 beats Retreat 0.51; healthy, full magazine, player hidden, Chase 0.80. `AutoChase` is disabled on the Utility AI guard because movement is now a Utility AI action.

Compiled against Unity 2021.3 assemblies and the decision cases checked with Mono, but not run in Unity Play mode.

The health coefficient is 0.45 rather than the initially proposed 0.25: the original formula did not produce the promised health-dependent switch. With three of six rounds, full health yields Fire 0.90 versus Reload 0.45. At 25% health, Fire is 0.7125 versus Reload 0.7875.

Run **Tactical Game → Utility AI → Check decision scenarios**. Expect a PASS message in the Console. Inspect `Select`: a valid action stays for at least 0.5 seconds, then a challenger must win by more than 0.05. Invalid actions are interrupted immediately. Initial ties prefer Reload, then Fire, then Hold.

## 2. Understand execution

`EnemyWeapon` owns six loaded rounds, 24 reserve rounds, reload timing, warning timing, shots and feedback. Reloading transfers missing ammunition after 1.7 seconds. Each shot consumes one round and checks the first physical collision again. Defaults preserve 8 damage, 19-metre range, a 1.2-second warning, and 1.6 seconds between shots.

`UtilityEnemyController` supplies state, scores every 0.2 seconds, selects an action and asks the weapon to execute it. Reload is committed until complete. Pause and overhead inspection freeze its timers. Death and round end stop combat. Restart reloads the scene and initializes state again.

## 3. Create and play the demo

1. Open the project with its target Unity 6000.6 editor and let scripts import.
2. Stop Play mode. Choose **Tactical Game → Utility AI → Create standalone demo**. Save any scene changes when Unity asks.
3. The command creates and opens `Assets/TacticalGame/Scenes/UtilityAiDemo.unity`, selects guard 01, disables all baseline guards, stops guards 02 and 03 from chasing, and equips only guard 01 with Utility AI. Guard 01 moves only through its Chase and Retreat actions. It refuses to overwrite an existing demo. Later, open that scene directly.
4. Press Play and Begin Mission. Approach guard 01. The overlay on the right shows perception state, observations, ammunition, current action, scores and the last switch explanation. A label above guard 01's head shows the live loop: SEES (inputs), THINKS (chosen action and best rival score), DOES (what EnemyWeapon is actually doing), MOVES (A* path), and the last three decision changes. Press F2 to hide or show it (F3 belongs to the squad debug panel). Press G to make the player invincible (hits still flash, health stays) so the guard can be watched for a long time; press G again before a real run.
5. Watch the magazine drain, Reload complete, and Fire resume. Duck behind a wall with a partial magazine: Fire becomes unavailable and Reload becomes attractive.
6. For a health demonstration, shoot the guard three times with the player's 25-damage rifle while its magazine is near half full. Watch health and candidate scores; avoid a fourth hit, which defeats it. The deterministic decision checks provide the exact half-ammunition comparison without timing a live fight.

## 4. Verify gameplay in Unity

- Confirm the warning precedes the first shot and health drops by 8 per shot.
- Put solid cover between player and guard; confirm no damage through it.
- Check that reload fills only missing rounds and reduces reserve by the same amount. With Starting Reserve set to zero before Play, an empty enemy must Hold.
- Pause mid-warning and mid-reload. Resume and confirm the remaining delay is preserved. Repeat using Tab for overhead inspection.
- Defeat the enemy; confirm shots stop. Restart and check full health, six rounds and 24 reserve with default settings.
- Let the round end; confirm combat stops. Check the ordinary Substation and Rail Depot scenes still use their existing baseline.

## Squad level

Two-level Utility AI. `SquadTactician` (one per scene, `Scripts/UtilityAI/SquadTactician.cs`) re-scores every 0.5 s and picks a play with the pure `SquadTactics` (`SquadTactics.cs`); it gives each guard a `TacticRole` and optional order point. Each guard's `UtilityScorer` still decides every action, so emergencies override the plan.

| Play | Available when | Score |
|---|---|---|
| CoveredRetreat | a member is under 35% health and another is alive | 0.70 + 0.30 x (1 - lowest/0.35) |
| PinAndFlank | someone sees the player, 2+ healthy (>= 35%) members, a healthy non-pinner has a flank point | 0.55 + 0.25 x avg healthy health |
| BoundingAdvance | nobody sees the player, target position known (squad blackboard), 2+ alive | 0.50 + 0.30 x avg alive health |
| Freestyle | always | 0.30 |

Play selection keeps the current play 2 s while it is available, then switches only for a lead above 0.08; an unavailable play is dropped at once.

| Role | Effect on the individual scorer |
|---|---|
| Pinner (sees, most ammo), Coverer | Fire + 0.30 |
| Flanker (nearest healthy with flank point), Mover (bounding, 4 m step) | Chase blocked; Advance 0.55 + 0.30 = 0.85 only with an order point and a teammate Covering; while the gate is closed Hold = 0.10 + 0.45 |
| Overwatch | Hold 0.10 + 0.50; Chase blocked; Fire (0.90) still wins when the player is visible |
| Retreater (lowest health) | Retreat + 0.30, capped at 0.99; the order point counts as cover |
| None (Freestyle) | no change from the single-guard rules above |

Emergencies: empty magazine Reload 1.00 beats every role score (Fire is unavailable at 0 ammo, Advance 0.85, Retreat capped at 0.99, Hold <= 0.60). A pinner's Fire (up to 1.20) outranks a half-empty visible-player Reload (max 1.15) by design.

Signals (derived from the chosen action): Fire or Overwatch Hold -> Covering; Advance/Chase -> Moving; Retreat -> CoverMe; reloading -> Reloading; else Idle. `PartnerCovering` is true when another alive member signals Covering. The tactician keeps a log of play changes and signal changes for an overlay.

Status: compiled against Unity 2021.3.45f2 assemblies and checked with a Mono console harness plus `Check decision scenarios` (which now includes the role and play cases). Not run in Unity Play mode; the tactician and controller wiring in a live scene are untested. Squad knowledge is a local blackboard, not the R3 `SquadCoordinator`.

## Squad tactics demo

Create the scene with the Tactical Game > Utility AI > Create squad tactics demo menu command, press Play, then use the director keys: `0` map, `1`-`3` follow a guard, `4` intruder view, `F6`/`F7`/`F8` Exposed/Hide/Ambush scenarios, `P` freeze, `L` slow motion. The director adds the `SquadTactician` and `SquadTacticsOverlay` itself.

Overlay regions (all on a 1440x900 virtual canvas):
- Top centre: the active play (coloured), its score and how long it has been held, every play with a score bar or "blocked", and a one-line plain-English goal.
- Left "Squad radio": one line per guard (role, current action, signal dot) and the last 8 radio messages, newest on top.
- In the world: a tag and signal pill above each guard, a line from a guard to its order point (FLANK / BOUND / COVER), a dashed grey "waiting for cover" or green "covered by 01" line to a teammate, the shared target (INTRUDER seen / LAST KNOWN) and a thin red line from each firing guard to it. The selected guard shows its full head label instead of the tag.
- Right: the selected guard's Utility AI panel. Bottom: the director's key bar.

Do not press Tab: the built-in overview freezes AI decisions.

Status: compiled against Unity 2021.3.45f2 only; the overlay and tactician wiring have not been run in Play mode.

## Integration and verification limits

`EnemyObservationProvider.Observe(range)` returns visibility, an aim point and distance. `PerceptionObservation` backs it with the guard's `GuardPerception` (view cone, wall occlusion, memory): the player counts as visible only while perception sees them and is Alerted, so the live player position is used only when legitimately seen. The scorer receives only the resulting snapshot. Role scoring is deferred.

Keep Health and TargetDummy on the guard. The controller disables BasicGuard in Awake as a safeguard; the demo also saves it disabled. Do not run another attack controller on the same enemy.

Verified locally: all runtime and editor C# sources compile against Unity 2021.3.45f2 assemblies, with one existing unused-variable warning in ProjectBuilder. All ten deterministic decision scenarios pass using Mono. Unity play-mode checks and Unity 6000.6 compilation have not been run. The demo is created by the editor command, not included as a pre-generated scene. Existing gameplay smoke checks still test the original maps and baseline; they do not validate this demo.
