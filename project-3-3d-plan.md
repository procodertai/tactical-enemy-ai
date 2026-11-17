# Project 3 — 3D FPS Game AI Plan

Build a **single-player 3D first-person shooter in Unity/C#**. A human player completes two objectives and reaches an exit while three AI enemies try to stop them. Target short, replayable rounds with a rifle, aiming, firing, reloading, health, objective interactions, and quick restart.

## 1. Core Project 3

### Starting point and tools

Use a single-player FPS controller and weapon foundation, with ordinary GameObjects and C# components for the custom game AI. Reuse suitable environment models, materials, textures, and effects from the Multiplayer FPS template to achieve the visual style the team likes. Match the source render pipeline and bring required asset dependencies with the visuals.

The Multiplayer FPS template itself uses DOTS, Netcode for Entities, and GhostBridge. Asset extraction and compatibility still need a trial in the actual project. The first setup session should produce one room with the reused visuals, working player controls, a gun, and a damageable target. If an asset has substantial dependencies, use a simpler compatible substitute to keep the game moving. Credit the source assets and template in the final submission. [Multiplayer FPS documentation](https://docs.unity3d.com/Packages/com.unity.template.multiplayer-fps@1.0/manual/index.html)

Implement the course algorithms yourselves. Use simple C# scripts, Inspector settings, scene markers, and an in-game debug overlay. Start verification with short in-editor checks. Share the Unity project through version control and assign each map scene an owner to reduce simultaneous scene edits.

### Playable loop

1. Spawn the player and three enemies at designated positions.
2. Let the player activate two objective terminals in either order.
3. Unlock the exit when both terminals are complete.
4. End on reaching the exit or losing all health. Give evaluation rounds a fixed time limit and record timeout separately.
5. Reset health, ammo, positions, objectives, and enemy memory for the next round.

Use one weapon type for the first complete match. Give the player and enemies readable hit feedback and consistent weapon rules. A raycast weapon is sufficient for the initial build. Add a health and ammunition display, objective status, and an interaction prompt.

### Hand-written game AI

| System | What the team implements | Evidence in the demo |
| --- | --- | --- |
| **Utility AI** | Score available actions such as fire, reload, seek cover, fall back, take a side route, and support a teammate. Use observed player information, own health and ammo, distance, and assigned role. Keep an action active long enough to execute, and allow interruption for urgent changes. | Show candidate scores and explain a change in the selected action. |
| **A\* navigation and positioning** | Search a grid on the ground's XZ plane. Mark cells blocked using 3D colliders and allow clearance for the character's width. Use the resulting waypoints to drive movement. Place a small set of cover and side-route markers, filter unreachable candidates, and handle failed routes. | An enemy routes around a wall to its selected position. Draw the grid and route from a debug camera. |
| **Perception** | Check viewing distance and angle, then raycast from eye height to check wall occlusion. Store the last seen position and its age. Make enemies choose from current observations and remembered information. | The player moves behind a wall and the enemy's knowledge and chosen action change. |
| **Squad coordination** | Share sightings, assign roles, and reserve destination markers across three enemies. Release claims when actions end or an enemy is defeated. Resolve two enemies requesting the same destination, and use a simple yield or replan rule when they obstruct each other. | The squad selects different positions and updates roles or claims as the encounter changes. |

For the initial maps, use one connected ground level with walkable routes around obstacles. Keep the playable surfaces compatible with the ground grid. Cover checks should use actual occlusion and reachability. Debug the paths and sightlines against the visible geometry.

### Four-person work split

Every teammate owns a core AI component and the game work needed to exercise it.

| Teammate | Main AI responsibility | Supporting game work | First milestone contribution |
| --- | --- | --- | --- |
| **1 — Utility AI** | Action scoring, priority tuning, and score overlay. | Adapt weapon fire, health, reload, and enemy attack execution. | One enemy changes action when ammo, health, or visibility changes. |
| **2 — A\* and positioning** | Ground grid, A\*, reachable destination selection, and path display. | Substation scene and enemy movement along waypoints. | One enemy follows its own computed path around a wall without clipping corners. |
| **3 — Coordination** | Shared sightings, roles, destination claims, and a coordination toggle. | Rail depot scene, enemy prefab setup, and basic movement animation. | Three enemies claim different destinations and release them correctly. |
| **4 — Perception and scenarios** | Vision, occlusion, memory, and repeatable comparison scenarios. | Adapt player controller, terminal interactions, HUD, exit, and reset. | An enemy loses sight behind a wall, and a complete round can restart. |

Choose the art kit together. Everyone contributes to gameplay tuning, visual polish, and the explanation of their AI work. Integrate the scripts into the same graybox early.

### Maps and visual quality

Build two handcrafted industrial maps with a shared modular asset kit. Start at roughly **35–45 metres across**, then adjust after checking player speed and encounter length. These dimensions are starting estimates. Build full-scale grayboxes and play them before dressing the spaces.

| Map | Layout | Objectives and visual identity |
| --- | --- | --- |
| **Substation — first complete map** | A central transformer yard, a small control room, and a maintenance passage that reconnects to the yard. Two approaches to each objective let players change routes. Use solid equipment housings and walls for clearly readable cover. | Activate the breaker and relay, then reach the service gate. Use concrete, metal equipment, cable runs, fences, and signal lights. Change indicator lights when an objective is complete. |
| **Rail depot — second map target** | A stationary train divides two ground-level routes, with a ticket hall and a connecting service passage. Break up long views with pillars and parked equipment. Keep objective and exit access on the same navigable level. | Activate a signal console and platform gate control, then reach the marked exit. Reuse structural materials and add platform markings, signage, benches, and train props. |

Aim for a coherent finished environment. Use consistent materials, deliberate lighting, convincing prop placement, and a few animated or illuminated details. Make interactive terminals stand out and keep enemy silhouettes visible at gameplay distances. Use rifle fire, reload, footsteps, hit feedback, and objective sounds to make the game responsive.

Use imported animation clips for basic enemy idle and movement. Keep weapon attachment and animation setup simple enough to get all three enemies into the first full encounter. Add a debug camera that gives an elevated view of the map for explaining paths and coordination.

### Three-week milestones

| Week | Deliverable |
| --- | --- |
| **1 — Playable substation graybox** | Complete the visual/controller trial first. Build the substation routes and a human-playable round with both terminals, three enemy instances, win/loss, and reset. Integrate the first version of all four AI systems. Check movement, hits through obstacles, loss of sight, destination claims, and resets in the editor. |
| **2 — Full AI encounter and second map** | Finish and tune the hand-written squad. Make the chase-only comparison mode work with the same movement and weapons. Playtest the substation and build the rail depot from the shared kit. Start the visual pass once routes are stable. |
| **3 — Art, sound, and demonstration** | Finish the substation's lighting, props, animation, sound, and HUD. Complete the rail depot as the second map target. Run repeatable comparisons, fix gameplay blockers, and prepare the demo and report. Reserve time for polish even if the optional experiment is attempted. |

### Core demo and evaluation

- A human can play a complete substation round with three enemies, complete objectives, reach the exit, and restart.
- Show Utility AI scores, A\* paths, perception memory, roles, and destination claims. Pause on one action change and explain its inputs.
- Compare a simple chase-only mode with the full hand-written squad on the same starting setups and scripted player routes. Use the same weapons, movement, perception limits, and round cap.
- Record player completion rate, enemy decision time, and how often enemies compete for or converge on the same destination. Include a few human playtests for readability and enjoyment.
- Explain the algorithms and acknowledge reused controller, weapon, art, and animation assets.

## 2. Optional upgrade — Two-sided reinforcement learning

Propose this upgrade to the group and professor. Fit the initial experiment on the substation after the hand-written match works. The rail depot remains the second map target. Another cycle of alternating training is a stretch goal.

### Unity/C# with live Python training

Unity runs the game, perception, A\*, coordination, round resets, and high-level action execution. Python implements tabular Q-learning and exchanges observations, actions, and rewards with the running simulation. Use the ML-Agents low-level Python API for communication, with compatible Unity and Python package versions. First verify one full round locally. [Unity ML-Agents Python API](https://unity-technologies.github.io/ml-agents/Python-LLAPI-Documentation/)

Google Colab is a candidate for training. Try running a Linux Unity build without graphics and the Python trainer together inside the Colab runtime. Check launch, step, reset, and checkpoint saving before scheduling longer runs. Save checkpoints outside the temporary runtime so sessions can resume. Local Unity and Python remain a practical training setup. [Unity executable documentation](https://unity-technologies.github.io/ml-agents/Learning-Environment-Executable/) · [Colab FAQ](https://research.google.com/colaboratory/faq.html)

Train using compact observations from the game state. Keep the learned choices at the same high level as the existing AI. The 3D environment executes the choices through the same movement and weapon rules. For the player bot, provide a simple scripted aiming, shooting, and reload routine while learning objective and movement choices.

### Optional responsibilities

| Teammate | Hands-on ML responsibility |
| --- | --- |
| **1 — Player policy** | Implement the Python player learner, train against fixed hand-written enemies, and freeze its checkpoint. |
| **2 — Simulation and observations** | Connect Unity to Python, encode discrete observations and valid actions, implement batch resets, and verify local and candidate Colab execution. |
| **3 — Enemy policy** | Train one shared enemy policy, export its Q-table and state/action encoding, and implement the playable Utility AI / Learned toggle. |
| **4 — Rewards and experiments** | Build scripted routes and reward logs, compare the effect of damage rewards, and run held-out evaluation and human playtests. |

### Observations, actions, and rewards

Discretize the observations into a small set of map zones and bands. Count the possible states before training and reduce observations if the table becomes too sparse. Use a fixed decision interval and log it with the round cap. Mask unavailable actions consistently during exploration, value updates, and deployment.

| Policy | Observations | Actions | Initial rewards |
| --- | --- | --- | --- |
| **Player bot** | Own map zone, health band, completed objectives, whether an enemy is visible, and reachable mission goals. | Move toward terminal A, terminal B, the exit, or a fallback marker, or wait. Code handles waypoint following, interactions, and the scripted weapon routine. | +1 for each newly completed objective, +5 for reaching the unlocked exit, −3 for defeat or timeout, and −0.01 per decision tick. |
| **Shared enemy policy** | Own map zone, health/ammo bands, role, visible or last-reported player zone and memory age, and available actions. Observations come through perception and shared sightings. | Choose from the hand-written enemy action set. Coordination resolves positions and A\* supplies routes. | Shared squad reward of +0.5 × newly inflicted player damage as a fraction of maximum health, +5 on player defeat, +1 on timeout, −5 on player escape, and −0.01 per decision tick. |

These reward values are starting estimates. Give each active enemy the same squad reward once per decision step, with one terminal update for its final transition. Log damage and terminal rewards separately. Compare the initial reward with a run that removes the damage bonus. Discount future rewards so movement and other setup actions can receive credit for later outcomes.

### Simulated rounds and staged training

1. **Scripted match.** Reset player and enemy positions, health, ammo, terminals, timers, and squad memory. Make scripted player routes complete rounds against the Utility AI squad. Fix simulation and reset issues with a few visible runs.
2. **Live data generation.** At each decision step, Unity sends observations, valid actions, and the previous interval's reward to Python. Python selects actions. Unity executes them and advances the simulation. Record observation, action, reward, next observation, and round outcome. Batch runs can disable graphics and audio while preserving gameplay rules.
3. **Player training.** Train the player policy against fixed Utility AI enemies. Save and freeze a checkpoint after checking mission completion. Select checkpoints using training or validation routes.
4. **Enemy training.** Train one shared policy used by all three enemies against a mix of the frozen player bot and scripted training routes, starting with an even split. Collect each enemy's transitions under its own observations and update the shared table. Keep the player checkpoint fixed and save the enemy checkpoint for evaluation.
5. **Playable deployment.** Import the frozen Q-table into Unity and run C# action selection. A human controls the player and the enemy mode toggle chooses Utility AI or the learned squad.
6. **Stretch goal.** Train a new player against frozen learned enemies, then a new enemy policy against a mix including that player. Retain the first policy pair for comparison.

Keep substation collision geometry, navigation, action meanings, and observation encoding fixed during a training experiment. Visual polish can proceed alongside training. Rerun the relevant experiment after gameplay changes that alter what the policies experience.

### Optional milestones and demonstration

Attempt the connection and a small player training run during week 2 once the core encounter works. Continue to the shared enemy policy when the player checkpoint is ready. Use week 3 for the frozen comparison and human playtests alongside the scheduled art and sound work.

Reserve several scripted routes and starting setups before training. Compare learned enemies with the full hand-written squad using the same held-out routes, seeds, game rules, and round cap. Keep these routes out of learning and reward tuning. Report player exit rate first, followed by damage, defeat/timeout counts, and time to finish.

Show training curves for both policies, the opponent mix, and the checkpoints used. Let a few people play both enemy modes in varied order, record the sample size, and report where each mode performs better. In the live demo, show a human playing against the three learned enemies and use the overlay to explain an observation, chosen action, and route.

