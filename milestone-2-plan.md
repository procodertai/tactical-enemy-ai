# Last Light — Milestone 2 — Learned Cover Selection

**Week 2 of 3.** Train a model to help an existing guard choose which piece of cover to move to. Build a replay lab that can test each possible choice from the same starting state, then measure when the game should trust the model's ranking and when it should fall back to shortest path. The squad still assigns roles, shares sightings, and reserves destinations through the existing system.

## What the class will see

Two cover markers both hide the guard once it arrives. One is closer, but reaching it can mean crossing an exposed opening. The replay lab forces each choice from the same start so the class can see both outcomes. Then the class sees the live guard use the model in one encounter and fall back to the existing ranking in another. An overhead overlay shows the candidates, selected route, model scores, and selection reason.

The model ranks only the cover candidates already accepted by the positioning system. A* still moves the guard, and the squad still rejects an occupied marker and tries the next one. Limit the experiment to the existing **Take Cover** action and one area of Substation.

## Who does what

| Owner | Work this week | What they present |
| --- | --- | --- |
| **Thien — Train and deploy the model** | Train a shallow decision tree on cover attempt outcomes. Export its thresholds, scores, and training example count per leaf to JSON and evaluate it in C#. Compare the learned ranking with the current shortest-path ranking on held-out encounters. | Show an example training row, a tree split, the predicted scores for two markers, and the held-out results. |
| **Minh — Counterfactual replay and data generation** | Build a replay lab that restores the same guard state, starting positions, and scripted player timing, then forces each legal cover choice in a separate run. Add a small seeded set of encounter variations and export one labelled row per attempted route, including path length and estimated exposure. Group rows from the same encounter together before splitting the data. | Force route A and route B in the same scenario, show the two outcomes side by side, and trace them to the training rows Thien receives. |
| **Tai — When the game should trust the model** | Implement a policy that uses the learned ranking when the top score has enough training support and a clear lead over the runner-up. Otherwise use shortest path. Choose thresholds on validation encounters, then compare shortest path, always using the model, and using the model with fallback on the same held-out encounters. Keep the existing squad and reservation rules. | Show one model-led decision and one fallback, explain what triggered each, and present the tradeoff between safe arrivals and how often the model is used. |
| **Ngan — Optimization, graphics, and UI** | Profile the same replay before and after the change, targeting one measured bottleneck. Improve lighting and cover readability in the demo area. Add the overhead candidate markers, chosen A* path, compact scores, and the reason for using the model or falling back. | Show a profiler before-and-after and a clear visual comparison of the candidate paths and model scores. |

## The machine learning experiment

1. **Collect outcomes from matched trials.** When a guard seeks cover, run each reachable, unclaimed candidate from the same starting setup. Use a scripted player that can fire at visible guards with a fixed cadence and the normal hit rules. Log one row per candidate attempt with a scenario ID, decision-time features, arrival time, and damage taken. Vary positions and previously observed threats across setups.
2. **Define the label.** A successful attempt reaches the claimed cover in six simulated seconds without taking damage. Other outcomes get a failure label. Record path failures, timeouts, and damage separately so the team can explain the result.
3. **Train a shallow binary classifier.** Start with a depth-3 decision tree. Inputs are path length, estimated exposure along the route from the last known threat position, sighting age, and guard health. The model predicts whether a candidate attempt will succeed. Rank by the tree's estimated success score and break ties by shortest path. The leaf's training example count gives Tai a measure of how much data supports a score. [Decision tree documentation](https://scikit-learn.org/stable/modules/generated/sklearn.tree.DecisionTreeClassifier.html)
4. **Validate the fallback.** Keep all candidate rows from the same setup together. Choose the minimum leaf count and score gap on validation encounters. Set those values before the final test. Use the existing shortest-path ranking when the checks fail. This tests the idea of allowing a learned predictor to abstain on uncertain decisions. [Selective classification research](https://proceedings.neurips.cc/paper/2017/hash/4a8423d5e91fda00bb7e46540e2b0cf1-Abstract.html)
5. **Test on unseen encounters.** Compare shortest path, always choosing by the model, and the model with fallback. Report safe arrivals, damage, time to cover, and model coverage, which is the fraction of decisions where the model was used. Measure decision overhead separately. Report the results even if the fallback fails to improve on shortest path.

Only values available when the guard makes its choice go into the model. Actual arrival, later damage, and the hidden player's position are reserved for running or measuring the experiment.

## One-week sequence

- **Day 1:** Pick the Substation encounter, agree on the replay controls and feature columns, and separate training, validation, and test setups.
- **Days 2–3:** Make the same encounter replayable with forced choices. Confirm that attempts produce both successes and failures before generating more data.
- **Days 4–5:** Train and export the tree, validate the fallback thresholds, and profile the integrated comparison.
- **Day 6:** Lock the thresholds, run the held-out tests, and finish the lighting and overlay for the demo area.
- **Day 7:** Present a matched replay, a decision tree rule, a fallback example, and one results table.

## Demo order

1. **Minh** forces each of the two cover choices from the same starting state and shows how the outcomes become training labels.
2. **Thien** walks through one tree split and the scores it produces for the two routes.
3. **Tai** shows when the game accepts the model's pick and when it falls back. Use the held-out table to explain the effect of the fallback.
4. **Ngan** shows the paths and selection reason in the improved overlay, then shares one measured performance improvement.

A successful milestone delivers a trained model used by the game, a repeatable dataset and comparison, and a readable Windows demo. Both maps keep their normal squad gameplay. Substation is the scope of the experiment and visual pass this week.

[System map](TEAM_SYSTEM_MAP.md) · [Current verification](Docs/VERIFICATION.md)
