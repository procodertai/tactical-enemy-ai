using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame.Editor
{
    public static class SquadComparison
    {
        const string Output = "Artifacts/Comparison";
        static readonly string[] Maps = { "Substation", "RailDepot" };
        static readonly List<Result> Results = new List<Result>();
        static readonly List<Waypoint> Route = new List<Waypoint>();
        static readonly HashSet<int> ConvergedPairs = new HashSet<int>();
        static GameSession current;
        static PathMover[] movers;
        static int run, waypoint, lastFrame, convergences;
        static float lastProgressAt, previousCapture;
        static Vector3 lastProgressPosition;
        static int previousFrameRate;
        static double started;
        static bool waiting, running, previousOptionsEnabled;
        static EnterPlayModeOptions previousOptions;
        static GuardAiMode previousMode;
        static UnityEngine.Random.State previousRandomState;

        struct Waypoint
        {
            public Vector3 Position, Aim;
            public WorldInteractable Interact;
        }

        struct Result
        {
            public string Map, RouteName, Outcome;
            public GuardAiMode Mode;
            public float Elapsed;
            public int Decisions, Conflicts, Convergences;
            public double DecisionMilliseconds;
        }

        [MenuItem("Tactical Game/AI Evaluation/Run chase-only comparison")]
        public static void Run()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before running the comparison.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ProjectBuilder.SubstationPath);
            Results.Clear();
            current = null;
            run = 0;
            started = EditorApplication.timeSinceStartup;
            previousCapture = Time.captureDeltaTime;
            previousRandomState = UnityEngine.Random.state;
            previousFrameRate = Application.targetFrameRate;
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            waiting = running = true;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180)
                    throw new TimeoutException("The squad comparison exceeded its wall time limit.");
                var game = GameSession.Instance;
                if (!EditorApplication.isPlaying || game == null) return;
                if (waiting)
                {
                    if (game == current || game.State != RoundState.Briefing || game.squadCoordinator.MemberCount != game.targets.Length) return;
                    BeginRun(game);
                    return;
                }
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                CountConvergences();
                if (game.State == RoundState.Won || game.State == RoundState.Lost)
                {
                    RecordRun(game.State == RoundState.Won ? "Extracted" : game.Remaining <= 0f ? "Timeout" : "Defeated");
                    return;
                }
                WalkRoute();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Complete(true);
            }
        }

        static void BeginRun(GameSession game)
        {
            current = game;
            if (run == 0) previousMode = game.AiMode;
            bool east = (run / 2) % 2 == 1;
            UnityEngine.Random.InitState(700 + run / 2);
            game.SetAiMode(run % 2 == 0 ? GuardAiMode.ChaseOnly : GuardAiMode.Squad);
            game.player.enabled = false;
            movers = game.targets.Select(target => target.GetComponent<PathMover>()).ToArray();
            BuildRoute(game, east);
            waypoint = 0;
            convergences = 0;
            lastFrame = -1;
            ConvergedPairs.Clear();
            lastProgressAt = Time.time;
            lastProgressPosition = game.player.transform.position;
            Time.captureDeltaTime = .025f;
            Application.targetFrameRate = -1;
            waiting = false;
            game.Begin();
        }

        static void BuildRoute(GameSession game, bool east)
        {
            Route.Clear();
            Vector3 start = game.player.transform.position;
            AddRoute(AStarPathfinder.Instance.FindPath(start, new Vector3(east ? 6f : -6f, 0f, -9f)));
            Vector3 from = Route[Route.Count - 1].Position;
            foreach (var target in new WorldInteractable[] { game.objectives[east ? 1 : 0], game.objectives[east ? 0 : 1], game.exitGate })
                from = AddApproach(from, target, game.player);
        }

        static void AddRoute(List<Vector3> path)
        {
            if (path == null || path.Count == 0) throw new InvalidOperationException("The scripted route has an unreachable leg.");
            foreach (var point in path) Route.Add(new Waypoint { Position = point });
        }

        static Vector3 AddApproach(Vector3 from, WorldInteractable target, PlayerController player)
        {
            List<Vector3> best = null;
            Vector3 aim = default;
            float cost = float.PositiveInfinity;
            foreach (var collider in target.GetComponentsInChildren<Collider>())
            {
                Vector3 center = collider.bounds.center;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8f;
                    Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.8f;
                    var path = AStarPathfinder.Instance.FindPath(from, candidate);
                    if (path == null || path.Count == 0) continue;
                    Vector3 point = path[path.Count - 1];
                    Vector3 eyes = new Vector3(point.x, player.viewCamera.transform.position.y, point.z);
                    if (!CanReach(eyes, center, player, target, true)) continue;
                    float length = Vector3.Distance(from, path[0]);
                    for (int j = 1; j < path.Count; j++) length += Vector3.Distance(path[j - 1], path[j]);
                    if (length >= cost) continue;
                    best = path;
                    aim = center;
                    cost = length;
                }
            }
            if (best == null) throw new InvalidOperationException("No scripted interaction position for " + target.displayName);
            AddRoute(best);
            Vector3 end = best[best.Count - 1];
            Route.Add(new Waypoint { Position = end, Aim = aim, Interact = target });
            return end;
        }

        static bool CanReach(Vector3 eyes, Vector3 aim, PlayerController player, WorldInteractable target, bool ignoreGuards)
        {
            Vector3 direction = aim - eyes;
            float distance = player.interactionDistance;
            if (direction.sqrMagnitude > distance * distance) return false;
            var hits = Physics.RaycastAll(eyes, direction.normalized, distance, ~(1 << 2), QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (ignoreGuards && hit.collider.GetComponentInParent<TargetDummy>() != null) continue;
                return hit.collider.GetComponentInParent<WorldInteractable>() == target;
            }
            return false;
        }

        static void WalkRoute()
        {
            var player = current.player;
            var next = Route[waypoint];
            Vector3 offset = next.Position - player.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= .07f * .07f)
            {
                if (next.Interact != null)
                {
                    player.viewCamera.transform.LookAt(next.Aim);
                    if (!CanReach(player.viewCamera.transform.position, next.Aim, player, next.Interact, false))
                    {
                        if (Time.time - lastProgressAt > 4f) RecordRun("RouteBlocked");
                        return;
                    }
                    next.Interact.Interact(player);
                }
                waypoint++;
                lastProgressAt = Time.time;
                lastProgressPosition = player.transform.position;
                if (waypoint == Route.Count && current.State != RoundState.Won)
                    throw new InvalidOperationException("The scripted route reached the exit without finishing the round.");
                return;
            }

            var step = Vector3.ClampMagnitude(offset, player.walkSpeed * Time.deltaTime);
            player.transform.rotation = Quaternion.LookRotation(offset);
            player.viewCamera.transform.localRotation = Quaternion.identity;
            player.GetComponent<CharacterController>().Move(step + Vector3.down * (2f * Time.deltaTime));
            if ((player.transform.position - lastProgressPosition).sqrMagnitude >= .05f * .05f)
            {
                lastProgressAt = Time.time;
                lastProgressPosition = player.transform.position;
            }
            else if (Time.time - lastProgressAt > 4f) RecordRun("RouteBlocked");
        }

        static void CountConvergences()
        {
            for (int a = 0; a < movers.Length; a++)
                for (int b = a + 1; b < movers.Length; b++)
                {
                    int pair = a * movers.Length + b;
                    if (movers[a].TryGetRouteGoal(out var goalA) && movers[b].TryGetRouteGoal(out var goalB) &&
                        (goalA - goalB).sqrMagnitude < .01f)
                    {
                        if (ConvergedPairs.Add(pair)) convergences++;
                    }
                    else ConvergedPairs.Remove(pair);
                }
        }

        static void RecordRun(string outcome)
        {
            var controllers = current.targets.Select(target => target.GetComponent<UtilityEnemyController>()).ToArray();
            var metrics = current.squadCoordinator.Metrics;
            if (current.AiMode == GuardAiMode.ChaseOnly && (current.squadCoordinator.CoordinationEnabled || metrics.ReservationRequests != 0))
                throw new InvalidOperationException("Chase-only guards used squad coordination.");
            Results.Add(new Result
            {
                Map = Maps[run / 4], RouteName = (run / 2) % 2 == 0 ? "West-first" : "East-first",
                Mode = current.AiMode, Outcome = outcome, Elapsed = current.Elapsed,
                Decisions = controllers.Sum(controller => controller.DecisionCount),
                DecisionMilliseconds = controllers.Sum(controller => controller.TotalDecisionMilliseconds),
                Conflicts = metrics.ReservationConflicts, Convergences = convergences
            });
            Debug.Log($"COMPARISON {Results.Last().Map} {Results.Last().RouteName} {current.AiMode} {outcome}");
            if (++run == Maps.Length * 4) { Complete(false); return; }
            waiting = true;
            current.LoadMission(Maps[run / 4]);
        }

        static void Complete(bool failed)
        {
            EditorApplication.update -= Tick;
            running = false;
            Time.captureDeltaTime = previousCapture;
            UnityEngine.Random.state = previousRandomState;
            Application.targetFrameRate = previousFrameRate;
            GameSession.SetNextAiMode(previousMode);
            EditorApplication.isPlaying = false;
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            try
            {
                if (!failed) SaveResults();
            }
            catch (Exception error) { failed = true; Debug.LogException(error); }
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(failed ? 1 : 0);
        }

        static string Number(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);

        static void SaveResults()
        {
            Directory.CreateDirectory(Output);
            var runs = new List<string> { "map,route,mode,outcome,elapsed_seconds,decisions,mean_decision_ms,reservation_conflicts,convergence_episodes" };
            foreach (var row in Results)
                runs.Add($"{row.Map},{row.RouteName},{row.Mode},{row.Outcome},{Number(row.Elapsed)},{row.Decisions}," +
                    $"{Number(row.DecisionMilliseconds / Math.Max(1, row.Decisions))},{row.Conflicts},{row.Convergences}");
            File.WriteAllLines(Path.Combine(Output, "runs.csv"), runs);

            var summary = new List<string> { "mode,rounds,extractions,completion_pct,mean_decision_ms,reservation_conflicts,convergence_episodes" };
            foreach (var mode in Results.GroupBy(row => row.Mode))
            {
                int count = mode.Count(), success = mode.Count(row => row.Outcome == "Extracted");
                summary.Add($"{mode.Key},{count},{success},{Number(100.0 * success / count)}," +
                    $"{Number(mode.Sum(row => row.DecisionMilliseconds) / Math.Max(1, mode.Sum(row => row.Decisions)))}," +
                    $"{mode.Sum(row => row.Conflicts)},{mode.Sum(row => row.Convergences)}");
            }
            File.WriteAllLines(Path.Combine(Output, "summary.csv"), summary);
            Debug.Log("COMPARISON complete. Results in " + Output);
        }
    }
}
