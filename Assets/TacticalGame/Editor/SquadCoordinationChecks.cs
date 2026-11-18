using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalGame.Coordination;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticalGame.Editor
{
    // Controlled interface inputs exercise coordination without implementing anyone else's AI.
    [InitializeOnLoad]
    public static class SquadCoordinationChecks
    {
        const string Running = "LastLight.FullCoordinationChecks";
        static readonly List<string> Results = new List<string>();
        static bool oldOptionsEnabled;
        static EnterPlayModeOptions oldOptions;
        static int stage, map, assertions;
        static double started, resumeAt;
        static GameObject rigObject;
        static SquadCoordinator rig;
        static SquadInterfaceSettings tuning;
        static Actor a, b, c;
        static DestinationClaim oldClaim;
        static MovementRequest oldMove, currentMove;
        static NavStub nav;

        sealed class Actor : ISquadMember
        {
            public string EnemyId { get; set; }
            public bool IsAlive { get; set; } = true;
            public Vector3 Position { get; set; }
        }
        sealed class Destination : ITacticalDestination
        {
            public int MarkerId { get; set; }
            public MarkerKind Kind { get; set; } = MarkerKind.Cover;
            public Vector3 Position { get; set; }
        }
        sealed class NavStub : IEnemyNavigation
        {
            public event Action<MovementReport> MovementReported;
            public void MoveTo(MovementRequest request) { }
            public void CancelMove(string enemyId, int requestId) { }
            public void Emit(MovementReport report) => MovementReported?.Invoke(report);
        }

        static SquadCoordinationChecks()
        { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }

        public static void InstallBuildAndRun()
        {
            SquadRegistrySetup.InstallStarterScenes();
            ProjectChecks.BuildWindows();
            Run();
        }

        [MenuItem("Tactical Game/Coordination/Run full coordination checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                Check(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Save open scenes first.");
            Results.Clear(); stage = map = assertions = 0;
            started = EditorApplication.timeSinceStartup; resumeAt = started + .5;
            oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(ProjectBuilder.SubstationPath);
            SessionState.SetBool(Running, true);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 150) throw new Exception("Coordination checks timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < resumeAt) return;
                var game = GameSession.Instance;
                if (game == null) return;
                switch (stage)
                {
                    case 0:
                        Check(game.squadCoordinator is ISquadCoordination, "Full interface is implemented.");
                        var live = game.squadCoordinator;
                        Check(live.MemberCount == 3 && live.DestinationCount == 7, "Installed roster.");
                        Check(live.Members.Select(m => live.GetRole(m.EnemyId)).Distinct().Count() == 3, "Three distinct initial roles.");
                        Check(live.GetRole("01") == SquadRole.Attacker && live.GetRole("02") == SquadRole.Flanker && live.GetRole("03") == SquadRole.Support, "Configured startup role order is deterministic.");
                        Check(live.GetComponent<SquadCoordinationDebug>().RunManualExample(), "Manual example is wired.");
                        Check(live.Claims.Count == 3 && live.Metrics.ReservationConflicts == 2, "Example resolves competing requests.");
                        Check(live.TryGetSharedSighting(out var supplied), "Example supplies an explicit observation.");
                        Vector3 originalPlayer = game.player.transform.position;
                        game.player.transform.position += new Vector3(3, 0, 2);
                        Check(live.TryGetSharedSighting(out var unchanged) && unchanged.Report.ObservedPosition == supplied.Report.ObservedPosition, "Player movement must not update reported knowledge.");
                        game.player.transform.position = originalPlayer;
                        live.ResetCoordinationContext();
                        NewRig();
                        Check(rig.GetRole("01") == SquadRole.Attacker && rig.GetRole("02") == SquadRole.Flanker && rig.GetRole("03") == SquadRole.Support, "Initial role policy.");
                        Check(rig.GetRole("missing") == SquadRole.Unassigned, "Unknown role is neutral.");
                        Check(rig.ReportSighting(new SightingReport("01", new Vector3(4, 0, 7), Time.time)), "Valid sighting.");
                        Check(!rig.ReportSighting(new SightingReport("02", Vector3.one, Time.time - .01f)), "Older sighting rejected.");
                        Check(!rig.ReportSighting(new SightingReport("missing", Vector3.one, Time.time)), "Unknown observer rejected.");
                        Check(!rig.ReportSighting(new SightingReport("01", new Vector3(float.NaN, 0, 0), Time.time)), "Invalid position rejected.");
                        Check(!rig.ReportSighting(new SightingReport("01", Vector3.zero, Time.time + 10)), "Future observation rejected.");
                        rig.EnemyDefeated("01");
                        Check(rig.TryGetSharedSighting(out _), "Shared knowledge survives its reporter until expiry.");
                        rig.RegisterEnemy(a);
                        stage++; Wait(.45); break;
                    case 1:
                        Check(!rig.TryGetSharedSighting(out _), "Report expiry.");
                        Check(!rig.ReportSighting(new SightingReport("01", Vector3.zero, Time.time - 1)), "Expired observations cannot revive memory.");
                        Check(rig.TryReserveDestination("01", 1) == DestinationReservationResult.Granted, "First claim granted.");
                        Check(rig.TryReserveDestination("01", 1) == DestinationReservationResult.AlreadyOwned, "Repeated ownership is idempotent.");
                        Check(rig.TryReserveDestination("02", 1) == DestinationReservationResult.Occupied, "Conflict rejected.");
                        Check(rig.TryReserveDestination("02", 2) == DestinationReservationResult.Granted, "Alternate destination.");
                        Check(rig.TryReserveDestination("02", 1) == DestinationReservationResult.Occupied && rig.TryGetDestinationClaim("02", out var retained) && retained.MarkerId == 2, "Failed switch keeps previous claim.");
                        Check(rig.TryReserveDestination("dead", 1) == DestinationReservationResult.InvalidEnemy, "Invalid claimant.");
                        Check(rig.TryReserveDestination("03", 99) == DestinationReservationResult.InvalidMarker, "Spawn marker is not a tactical destination.");
                        Check(rig.TryReserveDestination("03", 999) == DestinationReservationResult.InvalidMarker, "Unknown marker rejected.");
                        Check(!rig.ReleaseDestination("02", 1, DestinationReleaseReason.LeavingPosition), "Foreign release rejected.");
                        Check(rig.TryGetDestinationClaim("01", out oldClaim), "Claim snapshot.");
                        Check(rig.ReleaseDestination(oldClaim, DestinationReleaseReason.ActionChanged), "Owner releases token.");
                        Check(!rig.ReleaseDestination(oldClaim, DestinationReleaseReason.ActionChanged), "Repeated release is safe.");
                        rig.TryReserveDestination("01", 1);
                        Check(!rig.ReleaseDestination(oldClaim, DestinationReleaseReason.ActionChanged), "Old token cannot release a reacquired destination.");
                        Check(rig.TryReserveDestination("01", 3) == DestinationReservationResult.Granted, "Atomic successful switch.");
                        Check(!rig.Claims.Any(claim => claim.EnemyId == "01" && claim.MarkerId == 1), "One active claim per enemy.");
                        rig.TryReserveDestination("03", 1);
                        Check(rig.Claims.Select(claim => claim.MarkerId).Distinct().Count() == 3, "One owner per destination.");
                        stage++; Wait(.65); break;
                    case 2:
                        Check(rig.Claims.Count == 0 && rig.Metrics.ClaimsExpired == 3, "Abandoned claims expire.");
                        rig.TryReserveDestination("01", 1); rig.TryGetDestinationClaim("01", out oldClaim);
                        stage++; Wait(.3); break;
                    case 3:
                        Check(rig.RenewDestinationClaim(oldClaim), "Heartbeat renews its matching lease.");
                        stage++; Wait(.3); break;
                    case 4:
                        Check(rig.TryGetDestinationClaim("01", out _), "Renewed lease survives its original expiry.");
                        Check(rig.TryBeginMovement("01", 1, out currentMove), "Create tokenized movement request.");
                        Check(!rig.TryBeginMovement("01", 1, out _), "Request IDs cannot be reused.");
                        Check(!rig.HandleMovementReport(new MovementReport(currentMove, MovementStatus.Arrived)), "Arrival requires proximity to the owned marker.");
                        a.Position = new Vector3(4, 0, 5);
                        Check(rig.HandleMovementReport(new MovementReport(currentMove, MovementStatus.Arrived)), "Matching arrival acknowledged.");
                        stage++; Wait(.65); break;
                    case 5:
                        Check(rig.TryGetDestinationClaim("01", out _), "Actual occupancy retains its claim beyond travel lease.");
                        a.Position = Vector3.zero;
                        Check(!rig.TryGetDestinationClaim("01", out _), "Leaving an occupied position releases it.");
                        rig.TryReserveDestination("01", 1); rig.TryBeginMovement("01", 2, out oldMove);
                        rig.TryReserveDestination("01", 2); rig.TryBeginMovement("01", 3, out currentMove);
                        Check(!rig.HandleMovementReport(new MovementReport(oldMove, MovementStatus.Failed)), "Old route failure ignored.");
                        Check(rig.TryGetDestinationClaim("01", out var newer) && newer.MarkerId == 2, "Stale failure leaves current claim intact.");
                        Check(!rig.CancelMovement(oldMove), "Old action cancellation ignored.");
                        Check(rig.HandleMovementReport(new MovementReport(currentMove, MovementStatus.Obstructed)) && rig.TryGetDestinationClaim("01", out _), "Informational obstruction keeps the claim.");
                        Check(rig.HandleMovementReport(new MovementReport(currentMove, MovementStatus.Failed)), "Current route failure accepted.");
                        Check(!rig.TryGetDestinationClaim("01", out _), "Current failed route releases the claim.");
                        rig.ResetCoordinationContext();
                        rig.TryReserveDestination("01", 1); rig.TryBeginMovement("01", 1, out oldMove);
                        rig.ResetCoordinationContext();
                        rig.TryReserveDestination("01", 1); rig.TryBeginMovement("01", 1, out currentMove);
                        Check(oldMove.RequestId == currentMove.RequestId && oldMove.MarkerId == currentMove.MarkerId && oldMove.RoundVersion != currentMove.RoundVersion, "Reuse the same request/marker across contexts for the stale-round check.");
                        Check(!rig.HandleMovementReport(new MovementReport(oldMove, MovementStatus.Failed)), "Previous round/context report rejected.");
                        Check(rig.TryGetDestinationClaim("01", out _), "Fresh context claim remains.");
                        Check(rig.CancelMovement(currentMove) && !rig.TryGetDestinationClaim("01", out _), "Current action cancellation releases claim.");
                        NewRig();
                        a.Position = Vector3.zero; b.Position = Vector3.right; c.Position = Vector3.right * 2;
                        rig.TryReserveDestination("02", 1);
                        Check(rig.ReportObstruction("02", "01").Action == YieldAction.Wait, "Higher ID yields.");
                        Check(rig.ReportObstruction("01", "02").Action == YieldAction.Proceed, "Lower ID proceeds.");
                        Check(rig.ReportObstruction("02", "03").Action == YieldAction.Wait, "A second pair must not override an existing yield to a lower ID.");
                        Check(rig.ReportObstruction("01", "01").Action == YieldAction.InvalidRequest, "Self-blocking rejected.");
                        stage++; Wait(.3); break;
                    case 6:
                        Check(rig.ReportObstruction("02", "01").Action == YieldAction.Replan, "Persistent blockage requests a new route/destination.");
                        Check(!rig.TryGetDestinationClaim("02", out _), "Replan releases yielding agent's claim.");
                        rig.ReportObstruction("02", "01");
                        Check(rig.Metrics.ReplanRequests == 1, "Repeated blockage reports do not spam replan requests.");
                        stage++; Wait(1.1); break;
                    case 7:
                        Check(!rig.TryGetYieldInstruction("02", out _), "Stale obstruction cleared.");
                        c.Position = new Vector3(100, 0, 0);
                        Check(rig.ReportObstruction("01", "03").Action == YieldAction.None, "Distant agent is not a blocker.");
                        rig.ReportSighting(new SightingReport("01", Vector3.one, Time.time));
                        rig.TryReserveDestination("01", 1);
                        rig.SetCoordinationEnabled(false);
                        Check(rig.MemberCount == 3 && rig.DestinationCount == 4, "Toggle preserves membership.");
                        Check(rig.Claims.Count == 0 && !rig.TryGetSharedSighting(out _), "Toggle clears shared state.");
                        Check(rig.Members.All(member => rig.GetRole(member.EnemyId) == SquadRole.Unassigned), "Coordination-off roles are neutral.");
                        Check(rig.TryReserveDestination("01", 1) == DestinationReservationResult.CoordinationDisabled, "Explicit independent-mode response.");
                        Check(!rig.ReportSighting(new SightingReport("01", Vector3.zero, Time.time)), "Off rejects shared sightings.");
                        rig.SetCoordinationEnabled(true);
                        Check(rig.Members.Select(member => rig.GetRole(member.EnemyId)).Distinct().Count() == 3, "On restores roles.");
                        rig.TryReserveDestination("01", 1);
                        rig.EnemyDefeated("01");
                        Check(rig.Claims.Count == 0 && rig.GetRole("01") == SquadRole.Unassigned, "Death releases claims and roles.");
                        Check(rig.Members.Count(member => rig.GetRole(member.EnemyId) == SquadRole.Attacker) == 1, "Living member fills attacker role.");
                        rig.RegisterEnemy(a); rig.TryReserveDestination("01", 1); rig.UnregisterDestination(1);
                        Check(!rig.TryGetDestinationClaim("01", out _), "Removing marker releases its claim.");
                        rig.ResetRound();
                        Check(rig.MemberCount == 0 && rig.DestinationCount == 0 && rig.Claims.Count == 0 && !rig.TryGetSharedSighting(out _), "Round reset clears all state.");
                        NewRig(); rig.TryReserveDestination("01", 1);
                        bool reacted = false;
                        Action<DestinationClaim, DestinationReleaseReason> react = (claim, reason) =>
                        {
                            if (reacted || reason != DestinationReleaseReason.ActionChanged) return;
                            reacted = true; rig.TryReserveDestination("01", 2);
                        };
                        rig.DestinationReleased += react;
                        rig.TryReserveDestination("01", 3);
                        rig.DestinationReleased -= react;
                        Check(reacted && rig.Claims.Count == 1 && rig.TryGetDestinationClaim("01", out var lastRequest) && lastRequest.MarkerId == 2, "Reentrant release listeners must preserve one-claim-per-member ownership.");
                        // Real component forwarding: callback subscription, claim release, and unsubscription.
                        var real = game.squadCoordinator; real.ResetCoordinationContext();
                        string id = game.targets[0].EnemyId;
                        int marker = real.Destinations[0].MarkerId;
                        real.TryReserveDestination(id, marker); real.TryBeginMovement(id, 1, out currentMove);
                        var feedback = game.targets[0].GetComponent<SquadMovementFeedback>();
                        nav = new NavStub(); feedback.BindNavigation(nav);
                        nav.Emit(new MovementReport(currentMove, MovementStatus.Failed));
                        Check(!real.TryGetDestinationClaim(id, out _), "Optional navigation event bridge releases the matching claim.");
                        feedback.enabled = false;
                        real.TryReserveDestination(id, marker); real.TryBeginMovement(id, 2, out currentMove);
                        nav.Emit(new MovementReport(currentMove, MovementStatus.Failed));
                        Check(real.TryGetDestinationClaim(id, out _), "Disabled bridge unsubscribes.");
                        feedback.enabled = true; real.ResetCoordinationContext();
                        Results.Add("PASS " + game.missionName + ": roles, supplied/stale/invalid sightings, exclusive claims, token safety, leases, occupancy, movement feedback, obstruction/replan, toggle, cleanup, reset and optional navigation bridge.");
                        DisposeRig();
                        if (map == 0)
                        {
                            map = 1; stage = 0; game.LoadMission("RailDepot"); Wait(.5);
                        }
                        else Finish(false);
                        break;
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); Finish(true); }
        }

        static void NewRig()
        {
            DisposeRig();
            tuning = ScriptableObject.CreateInstance<SquadInterfaceSettings>();
            var serialized = new SerializedObject(tuning);
            serialized.FindProperty("sharedSightingLifetimeSeconds").floatValue = .35f;
            serialized.FindProperty("destinationLeaseSeconds").floatValue = .5f;
            serialized.FindProperty("obstructionReplanSeconds").floatValue = .25f;
            serialized.FindProperty("obstructionReportLifetime").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            rigObject = new GameObject("Coordination test inputs"); rig = rigObject.AddComponent<SquadCoordinator>();
            rig.Configure(null, null, tuning);
            a = new Actor { EnemyId = "01" }; b = new Actor { EnemyId = "02" }; c = new Actor { EnemyId = "03" };
            rig.RegisterEnemy(a); rig.RegisterEnemy(b); rig.RegisterEnemy(c);
            for (int i = 1; i <= 3; i++) rig.RegisterDestination(new Destination { MarkerId = i, Position = new Vector3(i * 4, 0, 5) });
            rig.RegisterDestination(new Destination { MarkerId = 99, Kind = MarkerKind.EnemySpawn });
        }
        static void DisposeRig()
        {
            if (rigObject != null) UnityEngine.Object.DestroyImmediate(rigObject);
            if (tuning != null) UnityEngine.Object.DestroyImmediate(tuning);
        }
        static void Wait(double seconds) { resumeAt = EditorApplication.timeSinceStartup + seconds; }
        static void Check(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
        static void Finish(bool failed)
        {
            DisposeRig(); SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
            Results.Add("Assertions: " + assertions);
            Directory.CreateDirectory("Artifacts/Coordination"); File.WriteAllLines("Artifacts/Coordination/full-coordination-checks.txt", Results);
            Debug.Log(string.Join("\n", Results)); EditorApplication.isPlaying = false;
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled; EditorSettings.enterPlayModeOptions = oldOptions;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
