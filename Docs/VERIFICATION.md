# Verification record

## Unity 6000.6.0f1

Verified on 23 November 2026 with the integrated AI on Substation and Rail Depot.

- Map and gameplay checks passed on both maps, including extraction, timeout, restart and wall occlusion.
- Guard checks passed for confirmed shared sightings, role-based decisions, separate cover and flank claims, and A* arrival feedback.
- The movement regressions passed on both maps. Injured guards choose another action when retreat and cover markers are unavailable, a moving guard detours around a stationary teammate, and the template guard develops a procedural stride when it moves.
- On both maps, a critically wounded guard completed its retreat, reacquired the player and fired, then moved again after cover blocked the shot.
- Coordination passed 152 assertions. Utility AI decision checks passed, including the separate Chase Only policy.
- The one-command Windows build published both maps to `Builds/LastLight`. The packaged player initialized successfully in a headless startup check.

Run **Tactical Game → Run gameplay smoke checks**, **Coordination → Run full coordination checks**, or **Utility AI → Check decision scenarios** in the editor to repeat those checks. The Windows build command is documented in [the running guide](RUNNING.md). The older Unity 2021 preview under `Builds/Compatibility2021` records the earlier foundation stage.

## Scripted Squad versus Chase Only comparison

Run **Tactical Game → AI Evaluation → Run chase-only comparison** from Edit mode. This runs eight rounds using the west-first and east-first routes on both maps in both modes. Each round starts from a fresh scene and uses the same weapon, perception settings, movement speed, and round cap. The scripted player walks, interacts with both terminals, and attempts extraction.

| Mode | Extractions | Reservation conflicts | Route convergence episodes |
| --- | ---: | ---: | ---: |
| Chase Only | 4 / 4 | 0 | 2 |
| Squad | 3 / 4 | 0 | 0 |

Repeated runs of the comparison produced these same outcomes. The west-first Rail Depot route was blocked in Squad mode. The harness records a blocked route when its player fails to make progress or reach an interaction for four simulation seconds. It counts a convergence episode when two guards start heading for the same A* endpoint.

Open `Artifacts/Comparison/runs.csv` for per-round results and `Artifacts/Comparison/summary.csv` for totals and mean decision time. Decision time measures the CPU time spent scoring and selecting actions. Pathfinding and rendering run separately, and timings depend on the machine. The results describe scripted walking routes. Human playtesting is a separate part of the course evaluation.
