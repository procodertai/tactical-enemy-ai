# Team system map — how the four roles work together

**Project:** Last Light / Tactical Enemy AI  
**Status snapshot:** 18 November 2026  
**Purpose:** Understand the responsibilities, interfaces, runtime flow, development order and evaluation of the four-person AI project.

The group is building **one integrated enemy AI system with four developers**. Each of the three enemies will use the work of all four teammates.

These diagrams follow the [3D project plan](project-3-3d-plan.md). **The behavior diagrams describe the intended system.** The shared interfaces exist, but most tactical behavior still needs implementation and teammate review.

The diagrams use Mermaid. Open this document in a Markdown viewer that supports Mermaid to see rendered charts; the diagram source remains readable in a text editor.

## Contents

1. [What the four roles actually mean](#1-what-the-four-roles-actually-mean)
2. [The main runtime loop](#2-the-main-runtime-loop-how-information-becomes-an-action)
3. [Inside each teammate's component](#3-what-happens-inside-each-teammates-component)
4. [The actual interface handoffs](#4-the-actual-handoffs-using-the-interfaces-already-in-the-project)
5. [Coordination responsibilities](#5-coordination-responsibilities-step-by-step)
6. [Failure handling](#6-failure-handling-where-the-components-must-cooperate)
7. [A concrete walkthrough](#7-a-concrete-walkthrough-across-all-four-roles)
8. [The development workflow](#8-the-development-workflow-who-builds-what-first)
9. [Cleanup and evaluation](#9-end-of-round-cleanup-and-evaluation)
10. [Optional machine-learning extension](#10-optional-later-extension-where-machine-learning-would-fit)

## 1. What the four roles actually mean

| Developer | Main question their component answers | Main responsibility |
|---|---|---|
| **R1 — Utility AI** | “What should this enemy do now?” | Score actions, choose one, manage its execution and cancellation |
| **R2 — A* and positioning** | “Which positions are suitable and reachable, and how should the enemy get there?” | Ground grid, candidate positions, paths, movement |
| **R3 — Coordination** | “What does the squad know, and who is using which position?” | Shared sightings, combat roles, destination ownership, conflict rules |
| **R4 — Perception and scenarios** | “What can this enemy legitimately know, and how do we test the system?” | Vision, occlusion, individual memory, scenarios, reset and evaluation |

There are also **combat roles** such as `Attacker`, `Flanker`, and `Support`. Those are assignments given to enemies by the squad system. They are different from the four **developer responsibilities**.

```mermaid
flowchart TD
    R1["Developer R1<br/>Utility AI and action execution"]
    R2["Developer R2<br/>A* and positioning"]
    R3["Developer R3<br/>Coordination"]
    R4["Developer R4<br/>Perception and scenarios"]

    R1 --> PREFAB["Reusable enemy setup<br/>Perception + decisions + navigation<br/>Health + weapon + animation"]
    R2 --> PREFAB
    R4 --> PREFAB

    PREFAB --> E1["Enemy 01"]
    PREFAB --> E2["Enemy 02"]
    PREFAB --> E3["Enemy 03"]

    R3 --> SQUAD["One shared squad service<br/>Members, reports, roles and claims"]

    E1 <-->|"Reports and requests"| SQUAD
    E2 <-->|"Reports and requests"| SQUAD
    E3 <-->|"Reports and requests"| SQUAD

    R2 --> GRID["Shared map navigation data<br/>Ground grid and destination markers"]
    GRID --> E1
    GRID --> E2
    GRID --> E3
```

An enemy therefore does **not** belong exclusively to one teammate. The coordination system serves all three enemies; their perception, decisions and movement operate individually.

## 2. The main runtime loop: how information becomes an action

This is the conceptual order of work. It does not mean every component must run every frame or that every enemy makes a new decision simultaneously.

```mermaid
flowchart TD
    WORLD["Current world<br/>Geometry, collisions and events"]

    WORLD --> PERCEPTION["1 — R4: Observe<br/>Range → viewing angle → wall occlusion"]

    PERCEPTION --> MEMORY["2 — R4: Update individual knowledge<br/>Visible now, last seen position and age"]

    MEMORY --> SHARE["3 — R3: Maintain squad context<br/>Accept legitimate reports<br/>Supply shared knowledge and combat roles"]

    MEMORY --> DECIDE
    SHARE --> DECIDE
    SELF["Enemy's own state<br/>Health, ammo and current action"] --> DECIDE

    DECIDE["4 — R1: Evaluate actions<br/>Check availability, score and select"]

    DECIDE --> NEEDMOVE{"Does the action<br/>need a destination?"}

    NEEDMOVE -->|"No"| WEAPON["R1: Execute local action<br/>Fire, reload or wait"]

    NEEDMOVE -->|"Yes"| CANDIDATES["5 — R2: Supply suitable<br/>reachable destination candidates"]

    CANDIDATES --> SELECT["6 — R1: Choose a candidate<br/>or reconsider the action"]

    SELECT --> RESERVE["7 — R3: Resolve reservation<br/>Is this destination available?"]

    RESERVE -->|"Granted or already owned"| MOVE["8 — R2: Find and follow the path"]
    RESERVE -->|"Occupied"| SELECT

    MOVE --> RESULT["9 — R1: Interpret movement result<br/>Match it to the active request"]

    RESULT --> CONTINUE["Continue, complete or cancel action<br/>Keep or release its destination claim"]
    WEAPON --> CONTINUE

    CONTINUE --> WORLD
```

The important boundaries are:

- **R4 establishes knowledge.** A hidden player's current position must not silently enter the tactical decision system.
- **R1 chooses behavior.** The coordinator supplies information and constraints; it does not replace Utility AI.
- **R2 proves positioning and navigation.** Reserving a marker does not prove that it is reachable or useful cover.
- **R3 manages shared ownership.** A valid path does not grant permission to occupy a teammate's reserved position.

## 3. What happens inside each teammate's component

| Owner | Inputs | Work performed | Output and consumer |
|---|---|---|---|
| **R4: perception** | Enemy eyes, player geometry, walls, observation settings | Check distance and viewing angle; raycast for occlusion | Current local observation → **R1**, legitimate sighting report → **R3** |
| **R4: individual memory** | Previous observation, current observation, time | Preserve the last observed position; calculate age; expire knowledge according to the agreed rule | Remembered information → **R1** |
| **R3: shared knowledge** | Reports from perception, member identities, observation times | Validate reporter, reject obsolete reports, calculate age and apply expiry | Usable shared report → **R1** |
| **R3: combat roles** | Registered living members and the later agreed assignment policy | Assign/update `Attacker`, `Flanker`, `Support`, or `Unassigned` | Role → **R1's scoring inputs** |
| **R1: action selection** | Own health/ammo, local/shared knowledge, role, current action | Check available actions, calculate scores, retain or change the active action | Chosen action → **action executor** |
| **R2: positioning** | Enemy ID, requested marker kind, known threat observation, map data | Check candidate suitability and establish reachability | `ReachableDestination` list → **R1** |
| **R3: reservations** | Enemy ID and selected marker ID | Check ownership and grant/reject the claim | Reservation result → **R1** |
| **R2: navigation** | Accepted movement request, grid and collision geometry | Find a path, follow waypoints, detect arrival/failure/obstruction | Movement report → **R1's executor** |
| **R1: weapon execution** | Active action, weapon state and current valid firing conditions | Aim/fire, apply cooldown, consume ammo, reload | Updated state and action result |
| **R3 + R2: obstruction handling** | Agents blocking one another and movement reports | **R3** decides the later yield rule; **R2** executes waiting/movement/replanning | Movement can continue without both agents repeatedly blocking each other |
| **R4 + everyone: evaluation** | Repeatable scenarios and component debug information | Run tests, collect results, identify failures | Evidence for tuning, demo and report |

**Choosing an action and executing it are separate jobs.** Selecting “reload” once should start a reload that takes time; the decision loop should not restart that reload every frame.

## 4. The actual handoffs, using the interfaces already in the project

This sequence follows the current contract definitions. The concrete services that answer these calls are later work.

```mermaid
sequenceDiagram
    participant P as R4 Perception
    participant S as R3 Coordination
    participant U as R1 Decision / executor
    participant N as R2 Positioning / navigation

    Note over P,N: Intended behavior — interfaces exist; services still need implementation

    P->>P: Check visibility and update local memory
    P->>S: ReportSighting(SightingReport)
    S-->>P: Accepted or rejected

    U->>S: GetRole(enemyId)
    S-->>U: SquadRole
    U->>S: TryGetSharedSighting(...)
    S-->>U: Report with age, or no usable report

    U->>U: Score actions using permitted knowledge
    U->>N: GetReachableCandidates(enemyId, kind, knownThreat)
    N-->>U: ReachableDestination list
    U->>U: Choose destination

    U->>S: TryReserveDestination(enemyId, markerId)

    alt Granted or AlreadyOwned
        S-->>U: Reservation succeeds
        U->>N: MoveTo(MovementRequest)
        N->>N: Search route and follow waypoints
        N-->>U: MovementReported(MovementReport)
        U->>U: Check the movement RequestId
    else Occupied
        S-->>U: Another enemy owns it
        U->>U: Try another candidate or action
    else CoordinationDisabled
        S-->>U: No managed reservation
        U->>U: Continue using the agreed independent fallback
    end
```

These are the main pieces of information being exchanged:

| Data | Essential contents | Why it matters |
|---|---|---|
| `ISquadMember` | Enemy ID, alive state, position | Identifies legitimate squad members |
| `ITacticalDestination` | Marker ID, kind, position | Identifies scene destinations |
| `SightingReport` | Reporter ID, **observed position**, observation time | Records an observation rather than a live player reference |
| `SharedSighting` | Original report and age | Lets decisions distinguish recent information from old information |
| `ReachableDestination` | Marker ID and position | Positioning promises that reachability has been established |
| `DestinationClaim` | Enemy ID and marker ID | Records exclusive destination ownership |
| `MovementRequest` | Enemy ID, marker ID, **request ID**, destination | Identifies one particular movement operation |
| `MovementReport` | Original request, status, reason | Lets the executor reject results from obsolete movements |

The exact definitions are in [SquadContracts.cs](Assets/TacticalGame/Scripts/Coordination/SquadContracts.cs) and [SquadInterfaces.cs](Assets/TacticalGame/Scripts/Coordination/SquadInterfaces.cs).

## 5. Coordination responsibilities, step by step

The coordination component provides the shared record and coordination rules that the other components use.

```mermaid
flowchart TD
    SETUP["R3: Create scene-local squad service"]
    SETUP --> REGISTER["Register enemies and destination markers<br/>Validate identities"]
    REGISTER --> STATE["Maintain squad state"]

    STATE --> KNOWLEDGE["Shared sightings<br/>Validate reporter and time<br/>Keep usable information"]
    STATE --> ROLES["Combat roles<br/>Apply the agreed role policy"]
    STATE --> CLAIMS["Destination claims<br/>One owner per marker<br/>One active claim per enemy"]

    KNOWLEDGE --> R1["R1 reads context<br/>and makes action decisions"]
    ROLES --> R1
    CLAIMS --> R1

    R1 --> REQUEST["Request or release a destination"]
    REQUEST --> CLAIMS

    LIFE["Death, leaving, cancellation,<br/>route failure or reset"] --> CLEANUP["R3: Remove obsolete claims<br/>and other relevant squad state"]
    CLEANUP --> STATE

    TOGGLE["Coordination switched off"] --> OFF["Clear reports and claims<br/>Return Unassigned roles<br/>Return CoordinationDisabled"]
    OFF --> FALLBACK["R1 uses independent behavior"]
```

The coordination implementation can progress through these stages:

| Stage | Main deliverable | What other teammates need from it |
|---|---|---|
| **Step 01 — current** | Review the implemented interfaces and resolve open agreements | Everyone accepts the same message fields and ownership rules |
| **Step 02 — next** | Scene-local squad record; register enemies and markers | Valid membership and destination lookup |
| **Step 03 — next** | Exclusive reservations and owner-checked release | R1 can safely commit to positions |
| **Later: shared sightings** | Report validation, age and expiry | R1 receives legitimate shared knowledge |
| **Later: combat roles** | Assignment/update policy | R1 can incorporate roles into scores |
| **Later: obstruction rules** | Decide which agent yields and when | R2 knows what movement response to execute |
| **Later: toggle and debug display** | Coordination on/off; visible roles, reports and claims | R4 can test and explain the coordination component's effect |

Only the first three step numbers are established by the existing coordination documents. The later ordering is a suggested progression.

## 6. Failure handling: where the components must cooperate

A successful “choose → move” path is only half the system. These cases prevent incorrect or stuck behavior.

```mermaid
flowchart TD
    CLAIM["R1 requests a marker<br/>R3 returns a reservation result"]

    CLAIM --> ACCEPT["Granted / AlreadyOwned"]
    ACCEPT --> MOVE["R1 issues a fresh movement request<br/>R2 executes it"]

    CLAIM --> OCCUPIED["Occupied"]
    OCCUPIED --> ALTERNATIVE["R1 selects another candidate<br/>or changes action"]

    CLAIM --> OFF["CoordinationDisabled"]
    OFF --> INDEPENDENT["R1 uses independent fallback"]

    CLAIM --> INVALID["InvalidEnemy / InvalidMarker"]
    INVALID --> FIX["Diagnose identity or setup problem<br/>Do not pretend permission was granted"]

    MOVE --> REPORT["R2 sends MovementReport"]
    REPORT --> MATCH{"R1: Is this the<br/>active RequestId?"}

    MATCH -->|"No"| IGNORE["Ignore obsolete result<br/>Do not cancel the newer move"]
    MATCH -->|"Yes"| STATUS{"Movement status"}

    STATUS -->|"Arrived"| HOLD["Keep claim while using position"]
    STATUS -->|"Failed"| RELEASE["R1 requests release<br/>R3 releases matching claim<br/>R1 decides again"]
    STATUS -->|"Obstructed"| YIELD["Report blockage<br/>R3 applies agreed conflict rule<br/>R2 executes the response"]
```

Three rules are especially important:

- **Arrival does not automatically release a claim.** The enemy still occupies or uses that position.
- **An old movement failure must not release a newer movement's destination.** That is why the executor checks `RequestId`.
- **A rejected reservation is not necessarily a bug.** `Occupied` can mean the coordinator correctly prevented two enemies from choosing the same position.

## 7. A concrete walkthrough across all four roles

This is an **illustrative paper scenario**, not a report of behavior already implemented.

| Step | Main owner | What happens | Information passed onward |
|---|---|---|---|
| 1 | **R4** | Enemy `01` sees the player at position `(5, 0, 8)` at simulation time `12` | A legitimate sighting |
| 2 | **R3** | Accept that report into shared squad knowledge | Reporter, observed position and observation time |
| 3 | **R1** | Enemy `02` asks for shared information at time `14` | Position `(5, 0, 8)`, age **2 seconds** |
| 4 | **R1** | Enemy `02` considers its health, ammo, knowledge and combat role; chooses a positioning action | Requested marker kind and known threat |
| 5 | **R2** | Find suitable reachable markers; return markers `3` and `5` | Candidate destinations |
| 6 | **R1** | Select marker `3` and request it | Enemy `02`, marker `3` |
| 7 | **R3** | Marker `3` is already owned by enemy `01`; return `Occupied` | Explicit rejection |
| 8 | **R1** | Try marker `5` | Another reservation request |
| 9 | **R3** | Grant marker `5` to enemy `02` | Exclusive ownership |
| 10 | **R1 → R2** | Issue movement request `17`; calculate and follow the route | Enemy ID, marker ID, request ID and destination |
| 11 | **R2 → R1** | Report arrival for request `17` | Matching movement result |
| 12 | **R1 + R3** | Continue the action at that position; retain the reservation | Marker `5` stays owned |
| 13 | **R4 + R3** | No new sighting occurs; the original report ages | Knowledge becomes stale, rather than tracking the hidden player |
| 14 | **R1 + R3** | When the enemy leaves the position, release its claim | Marker `5` becomes available |

The proposed shared-report lifetime is **5 seconds, pending team agreement**. Under that proposal, the time-12 report would be expired at time 18.

Enemy `02` knows **where the player was observed**. It does not automatically know where the player moved afterward.

## 8. The development workflow: who builds what first

The runtime loop above is how components cooperate **during execution**. This chart shows how the four humans should build and integrate them.

```mermaid
flowchart TD
    FOUNDATION["Existing foundation<br/>Playable environment and temporary guards"]

    FOUNDATION --> CONTRACTS["All four: finish interface review<br/>IDs, messages, ownership and failure meanings"]

    CONTRACTS --> P1["R1: Build action selection<br/>with isolated test inputs"]
    CONTRACTS --> P2["R2: Build grid, candidate queries<br/>path search and waypoint movement"]
    CONTRACTS --> P3["R3: Build membership<br/>reservations and shared records"]
    CONTRACTS --> P4["R4: Build visibility, memory<br/>and repeatable test scenarios"]

    P1 --> SINGLE["All four: integrate one enemy<br/>Observe → choose → reserve → move / act"]
    P2 --> SINGLE
    P3 --> SINGLE
    P4 --> SINGLE

    SINGLE --> REPLACE["Disable BasicGuard<br/>Use the integrated controller"]
    REPLACE --> THREE["Integrate all three enemies<br/>Shared reports, roles and claim conflicts"]
    THREE --> FAILURES["Verify cancellation, death,<br/>blocked movement and clean resets"]
    FAILURES --> COMPARE["R4 leads repeatable comparisons<br/>Each teammate supplies module evidence"]
    COMPARE --> DEMO["All four: tune, polish and explain<br/>Working demo and report"]
```

Each component can be developed with small test inputs before every other component is complete. Integration should still happen early, so disagreements about interfaces become visible while the system is small.

A useful completion test for each developer is:

| Role | First proof that their component works |
|---|---|
| **R1** | An enemy changes its selected action when health, ammo or visibility changes; the displayed scores explain why |
| **R2** | An enemy follows its computed route around an obstacle without cutting through a wall |
| **R3** | Two enemies request one marker; only one owns it, and ownership is correctly released later |
| **R4** | A wall stops new observations; last-seen memory ages correctly; the scenario resets cleanly |

## 9. End-of-round cleanup and evaluation

Reset belongs to the whole system. R4 leads the scenario/reset integration, while each owner clears their own component's state.

```mermaid
flowchart TD
    END["R4: Detect scenario or round completion"]
    END --> STOP["Stop further AI decisions and execution"]
    STOP --> RECORD["R4: Record outcome and measurements<br/>before clearing the evidence"]

    RECORD --> ONE["R1: Cancel actions<br/>Reset weapon and action state"]
    RECORD --> TWO["R2: Cancel movement<br/>Clear active paths and restore positions"]
    RECORD --> THREE["R3: Clear old members,<br/>reports, roles and claims"]
    RECORD --> FOUR["R4: Clear perception memory<br/>Reset scenario conditions and clock"]

    ONE --> READY["All components ready"]
    TWO --> READY
    THREE --> READY
    FOUR --> READY

    READY --> NEXT["Initialize the next round<br/>Register valid members after initialization<br/>Begin observations and decisions"]
```

The current game reloads its scene on restart. A future scene-local coordinator should therefore start empty. An in-place reset would need explicit cleanup calls such as `ResetRound()`.

For evidence, distinguish these two experiments:

| Comparison | What it measures |
|---|---|
| **Chase-only vs full tactical squad** | Whether the combined tactical system improves behavior over the simple decision baseline |
| **Same Utility AI, coordination on vs off** | Whether **the coordination component** adds value while the other systems remain the same |

Keep the map, starting setup, weapons, movement, perception limits and round cap consistent.

| Evidence | Main owner |
|---|---|
| Selected action, candidate scores and decision time | **R1** |
| Grid, path, reachability and route failures | **R2** |
| Shared report age, roles, claims, rejected conflicts and crowding | **R3** |
| Scenario setup, observation correctness, player completion, defeat/timeout and result collection | **R4** |

## 10. Optional later extension: where machine learning would fit

The core project above is hand-written AI. The optional learning experiment is a later track, with a different supporting work split.

```mermaid
flowchart TD
    CORE["Complete and verify the hand-written squad"]
    CORE --> BRIDGE["R2: Unity–Python connection<br/>Observations, valid actions, stepping and reset"]

    BRIDGE --> PLAYER["R1: Train a player policy<br/>against fixed hand-written enemies"]
    PLAYER --> FREEZEPLAYER["Freeze the selected player checkpoint"]

    FREEZEPLAYER --> ENEMY["R3: Train one shared enemy policy<br/>against frozen player and scripted routes"]
    ENEMY --> FREEZEENEMY["Freeze and export the enemy policy"]

    R4["R4: Rewards, scenario splits<br/>logging and experiments"] --> PLAYER
    R4 --> ENEMY
    R4 --> EVALUATE

    FREEZEENEMY --> EVALUATE["Compare on held-out scenarios<br/>and human playtests"]
    EVALUATE --> DEPLOY["Human-playable selector<br/>Utility AI or frozen learned policy"]

    KEEP["Retain perception, coordination,<br/>navigation and action execution"] --> DEPLOY
```

In this extension, learning chooses **high-level actions**. It does not replace collision handling, path following, weapon mechanics or the need for legitimate observations.

**Current project status — 18 November 2026:** the playable foundation and temporary `BasicGuard` exist; the shared contract types and identity adapters also exist. Team approval of those interfaces is pending. The next coordination work is to establish the squad record and registration, then exclusive destination reservations.
