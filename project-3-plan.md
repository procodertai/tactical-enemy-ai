# Project 3 — Last Train Out

Build a top-down 2D wartime shooter in **Unity with C#**. The game takes place in the fictional city of Veyra, where a ceasefire has collapsed and civilians are waiting for the last evacuation train. The player is a member of the evacuation detail sent to restore power to the rail line and reopen the route to the platform. Three opposing soldiers patrol each mission area.

The player moves, aims, fires a rifle, reloads, and interacts with mission objectives. Each map has two interactable objectives and an exit. Completing both objectives and reaching the exit wins the mission. A short text or radio briefing explains the stakes. Reuse the interaction mechanic across both maps so the story comes through the locations, objectives, and environmental details.

## 1. Original Project 3 — Core submission

### Game AI and team assignments

Implement the core AI by hand in **Unity/C#**. Perception reports what each enemy can see or remembers. Utility AI scores the available game actions. Squad coordination shares observations and resolves competing destination claims. A\* plans a route to the selected position, and the game code executes the action.

| Teammate | Core game AI responsibility | Game work |
| --- | --- | --- |
| **1 — Utility AI** | Write action scores using visibility, distance, health, ammo, and role. Show why an enemy chooses actions such as firing, reloading, or moving to cover. | Rifle fire, health, and reload. |
| **2 — A\* and positioning** | Build the navigation grid, implement A\*, and place reachable cover and side-route destinations. Show the chosen path in the debug view. | Substation tilemap. |
| **3 — Squad coordination** | Implement shared sightings, roles, and destination claims for three enemies. Have an enemy consider another destination when a teammate claims its first choice. | Rail depot tilemap using the shared art kit. |
| **4 — Perception and scenarios** | Implement vision range, wall occlusion, and last-seen memory. Set up an encounter that shows an enemy gain and lose sight of the player. | Player movement, objectives, exit, and round reset. |

Everyone playtests and explains their own AI component in the demo or report.

### Maps and visual direction

Build **two handcrafted maps** that look like parts of the same city and play differently. Use Unity Tilemaps and 2D colliders. Start with a fixed camera that shows the whole playable area. Build silhouettes and routes with plain tiles first, then dress those routes so every space has a recognizable purpose.

#### The substation

Start around **24 × 18 tiles**. A transformer yard sits between a street and a maintenance corridor, with a fenced courtyard connecting them. The player resets a breaker and restarts a power relay before leaving through the service gate. Leave enough room around the main obstacles for multiple agents to move, and give each objective at least two approaches. Make this the first complete mission and the main evaluation map.

Give the substation a visual change when the power returns. A row of lamps coming on or a signal changing color can communicate progress in play while tying the level to the evacuation story.

#### The rail depot

Reuse the same concrete, fences, doors, and utility props. Build a compact platform with a stationary train, a ticket hall, and a looping service passage. The player switches the rail signal and opens the platform gate, then reaches the boarding point. Where the substation is organized around a central yard, the depot has long sightlines broken up by platforms, pillars, and a train car. Make the route choices visible from the fixed camera.

Use a restrained palette of weathered concrete, brick, and steel, with warm windows and signal lights as accents. Give the locations a few signs of the people who fled, such as abandoned luggage, a handwritten evacuation notice, and a lit waiting room. Keep the characters, objectives, and effects readable over the scenery. Use a small set of consistent sounds for rifle fire, reloads, footsteps, and objective completion.

### Three-week build plan

| Week | Core milestone |
| --- | --- |
| **1 — Playable substation graybox** | A human can move, aim, fire, reload, complete both objectives, reach the exit, and restart. Use simple in-editor checks for paths around walls, loss of sight, and destination claims as the AI pieces come together. |
| **2 — Full encounter** | Integrate the four AI components. Play a complete substation round against three hand-written enemies. Tune the actions and map routes, and start the rail depot with the shared tiles and objective interactions. |
| **3 — Polish and demo** | Reserve time for the substation art and sound pass, then finish the rail depot as the second map target. Run the course comparisons and prepare the demo and short report. |

### Core demo and evaluation

- Play the substation mission from briefing to exit or defeat, with a quick restart.
- Show three enemies selecting Utility AI actions, following A\* paths, sharing sightings, and resolving competing destinations. Use the overlay to explain one decision and show what changes when the player disappears behind a wall.
- Compare the full hand-written squad with a simple chase-only mode on the same map and starting setup. Record player exits, how often enemies bunch up, and how quickly an enemy changes action after spotting the player. Simple in-editor runs are enough to get the comparison started.

## 2. Optional upgrade — Two-sided reinforcement learning

This is a proposal to discuss with the group and professor after the core scope is agreed. Start it once the substation graybox and its hand-written enemies can finish a round. Keep the experiment on the substation and reserve the art and sound time from the core schedule.

### Preferred setup — Unity/C# with live Python training

- **Unity/C#** runs the substation simulation and owns resets, observations, valid game actions, rewards, and round outcomes. Keep A\*, perception, and squad coordination in the game code.
- **Python** receives observations and rewards from Unity at each decision step, returns actions, and updates the hand-written tabular Q-learning policy. Start with the Unity ML-Agents low-level Python API as the connection and verify one complete round locally. Export each frozen Q-table with its observation and action IDs so Unity can load it for play or evaluation. [Unity ML-Agents Python API](https://unity-technologies.github.io/ml-agents/Python-LLAPI-Documentation/)
- **Google Colab is a candidate.** Try a Linux Unity training build and the Python trainer in the same Colab runtime. Before committing, verify that the build starts without graphics and can complete, reset, and checkpoint a round. Save checkpoints outside the temporary runtime. If Colab setup takes too long, run Unity and Python together locally. [Unity executable documentation](https://unity-technologies.github.io/ml-agents/Learning-Environment-Executable/) · [Colab runtime FAQ](https://research.google.com/colaboratory/faq.html)

In the playable upgrade, **a human controls the player**. A toggle selects either three learned enemies using one shared policy or the three hand-written Utility AI enemies. The player bot is available for training and evaluation.

### Optional responsibilities

| Teammate | Hands-on ML work |
| --- | --- |
| **1 — Player policy** | Write the Python player learner, train it against the fixed Utility AI squad, and freeze its checkpoint. |
| **2 — Simulation and observations** | Connect Unity's simulation to Python. Implement reset, observation encoding, valid-action checks, and transition logging. Check that a live round can complete locally and try the same setup in Colab. |
| **3 — Shared enemy policy** | Train and export the shared enemy Q-table. Integrate the in-game Utility AI / Learned toggle and check that all three enemies can load the same policy. |
| **4 — Rewards and evaluation** | Build scripted routes, log rewards, compare the effect of the damage bonus, and run the held-out route comparisons and human playtests. |

### How simulated rounds generate training data

Reset the substation with fresh health, ammo, objectives, enemy positions, and squad memory. Pick a training spawn and opponent using a logged seed. Advance the same game rules in fixed simulation steps with rendering and audio disabled during batch runs. At each decision tick, Unity sends observations and available actions to Python. Python returns a choice for each active training agent, then Unity resolves the actions and advances the round. Record observation, action, reward, next observation, and whether the round ended. Update only the policy currently being trained. Record the outcome and reward breakdown for each round. Save a checkpoint before switching stages.

### What each policy learns

| Policy | Observations | High-level actions | Starting rewards |
| --- | --- | --- | --- |
| **Player bot** | Own map zone and health band, which objectives are complete, whether a nearby enemy is visible, and which mission goals are reachable. | Go to the breaker, go to the relay, go to the exit, move to a nearby fallback point, or wait. The shared game code handles following a route and interacting on arrival. | **+1** for each objective completed for the first time, **+5** for reaching the unlocked exit, **−3** for defeat or timeout, and **−0.01** per decision tick. |
| **Shared enemy policy** | Own map zone, health and ammo bands, assigned role, whether the player is visible or recently reported, and which game actions are available. No access to the player's hidden exact position. | Use the same action set as Utility AI, such as fire when available, reload, move to a marked cover point, take a side route, or support a teammate. The existing coordination code resolves competing destinations. | Each active enemy receives the same squad reward. Start with **+0.5 × the fraction of the player's max health newly lost**, **+5** if the player is defeated, **+1** on timeout, **−5** if the player reaches the exit, and **−0.01** per decision tick. Keep terminal rewards larger than any single hit reward and retain discounted future rewards so a setup action can pay off later. |

Treat those values as starting points. Log defeat and timeout separately. Compare the starting enemy reward with a run that removes the damage bonus, and check whether damage rewards improve interception or just encourage more hits in rounds the player still wins.

### Training order

1. **Make the scripted match work.** A scripted player follows waypoint routes, completes objectives, and attempts the exit against three hand-written Utility AI enemies. Check resets, win and loss outcomes, and available actions in the editor. Run one live Unity–Python round before starting training batches.
2. **Train and freeze the player bot.** Train against fixed Utility AI enemies on the substation. Compare exit success with the scripted player on a few training scenarios. Save a checkpoint for enemy training and stop updating it.
3. **Train and freeze the shared enemy policy.** Sample rounds from a mix of the frozen player bot and scripted training routes, starting with an even split. All three enemies use and update the same policy during training, using their own observations and the common squad reward. Freeze the selected checkpoint for comparison runs and the playable build.
4. **Stretch goal — alternate once more.** Train a new player checkpoint against the frozen learned enemies, then train a new enemy checkpoint against a mix that includes this player. Save the first pair so the team can compare both iterations.

### Optional demo and evaluation

- **Training evidence.** Show exit rate or enemy stop rate over training, the opponent mix, the frozen checkpoints, and the number of defeats and timeouts.
- **Held-out routes.** Reserve several scripted routes and starting setups before training. Compare the learned squad with the full hand-written Utility AI using the same setups, seeds, time limit, and game rules. Keep those runs out of training and reward tuning. Report player exit rate first, then player damage, timeouts, and time to finish.
- **Human playtests.** Ask a few people to play both modes on the substation. Vary which mode they play first. Record completion, damage, and whether the enemies' reactions felt readable. Report the sample size and any cases where Utility AI performed better.
- **Live demo.** Play one human-controlled round with three learned enemies, then restart the same setup with the Utility AI squad. Show a selected enemy's observation, action, and path. Display Q-values for the learned policy and action scores for the hand-written one.
