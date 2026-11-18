# Utility AI: first combat milestone

One stationary guard chooses Fire, Reload, or Hold. Perception, navigation and squad coordination remain future integrations.

## 1. Understand the scorer

Read `Assets/TacticalGame/Scripts/UtilityAI/UtilityScorer.cs` first. `UtilityInputs` is the snapshot the decision needs; `Evaluate` produces availability, scores and explanations without scene access. An unavailable action cannot win. Scores use these rules:

| Action | Available when | Score |
| --- | --- | --- |
| Fire | Visible, in range, loaded | 0.65 + 0.25 × health fraction |
| Reload | Magazine not full and reserve remains | 1 if empty; 0.95 if target hidden; otherwise 0.2 + 0.5 × missing magazine fraction + 0.45 × lost health fraction |
| Hold | Always | 0.1 |

The health coefficient is 0.45 rather than the initially proposed 0.25: the original formula did not produce the promised health-dependent switch. With three of six rounds, full health yields Fire 0.90 versus Reload 0.45. At 25% health, Fire is 0.7125 versus Reload 0.7875.

Run **Tactical Game → Utility AI → Check decision scenarios**. Expect a PASS message in the Console. Inspect `Select`: a valid action stays for at least 0.5 seconds, then a challenger must win by more than 0.05. Invalid actions are interrupted immediately. Initial ties prefer Reload, then Fire, then Hold.

## 2. Understand execution

`EnemyWeapon` owns six loaded rounds, 24 reserve rounds, reload timing, warning timing, shots and feedback. Reloading transfers missing ammunition after 1.7 seconds. Each shot consumes one round and checks the first physical collision again. Defaults preserve 8 damage, 19-metre range, a 1.2-second warning, and 1.6 seconds between shots.

`UtilityEnemyController` supplies state, scores every 0.2 seconds, selects an action and asks the weapon to execute it. Reload is committed until complete. Pause and overhead inspection freeze its timers. Death and round end stop combat. Restart reloads the scene and initializes state again.

## 3. Create and play the demo

1. Open the project with its target Unity 6000.6 editor and let scripts import.
2. Stop Play mode. Choose **Tactical Game → Utility AI → Create standalone demo**. Save any scene changes when Unity asks.
3. The command creates and opens `Assets/TacticalGame/Scenes/UtilityAiDemo.unity`, selects guard 01, disables all baseline guards and equips only that guard with Utility AI. It refuses to overwrite an existing demo. Later, open that scene directly.
4. Press Play and Begin Mission. Approach guard 01. The overlay shows observations, ammunition, current action, scores and the last switch explanation. Press F3 to hide or show it.
5. Watch the magazine drain, Reload complete, and Fire resume. Duck behind a wall with a partial magazine: Fire becomes unavailable and Reload becomes attractive.
6. For a health demonstration, shoot the guard three times with the player's 25-damage rifle while its magazine is near half full. Watch health and candidate scores; avoid a fourth hit, which defeats it. The deterministic decision checks provide the exact half-ammunition comparison without timing a live fight.

## 4. Verify gameplay in Unity

- Confirm the warning precedes the first shot and health drops by 8 per shot.
- Put solid cover between player and guard; confirm no damage through it.
- Check that reload fills only missing rounds and reduces reserve by the same amount. With Starting Reserve set to zero before Play, an empty enemy must Hold.
- Pause mid-warning and mid-reload. Resume and confirm the remaining delay is preserved. Repeat using Tab for overhead inspection.
- Defeat the enemy; confirm shots stop. Restart and check full health, six rounds and 24 reserve with default settings.
- Let the round end; confirm combat stops. Check the ordinary Substation and Rail Depot scenes still use their existing baseline.

## Integration and verification limits

`EnemyObservationProvider.Observe(range)` returns visibility, an aim point and distance. A teammate can implement a subclass backed by perception and assign it to the controller. The supplied `TemporaryEnemyObservation` directly references the player for range/raycast checks; it has no viewing cone or memory. The scorer receives only the resulting snapshot. Movement and role scoring are deferred.

Keep Health and TargetDummy on the guard. The controller disables BasicGuard in Awake as a safeguard; the demo also saves it disabled. Do not run another attack controller on the same enemy.

Verified locally: all runtime and editor C# sources compile against Unity 2021.3.45f2 assemblies, with one existing unused-variable warning in ProjectBuilder. All ten deterministic decision scenarios pass using Mono. Unity play-mode checks and Unity 6000.6 compilation have not been run. The demo is created by the editor command, not included as a pre-generated scene. Existing gameplay smoke checks still test the original maps and baseline; they do not validate this demo.
