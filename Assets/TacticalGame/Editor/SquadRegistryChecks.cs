using System;
using System.Collections.Generic;
using System.IO;
using TacticalGame.Coordination;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticalGame.Editor
{
    [InitializeOnLoad]
    public static class SquadRegistryChecks
    {
        const string Running = "LastLight.SquadRegistryChecks";
        static readonly List<string> Results = new List<string>();
        static bool oldOptionsEnabled;
        static EnterPlayModeOptions oldOptions;
        static int stage, map;
        static double started, resumeAt;
        static TestMember transient;
        static SquadCoordinator previous;
        static int removedMarkerId;

        sealed class TestMember : ISquadMember
        {
            public string EnemyId { get; set; }
            public bool IsAlive { get; set; } = true;
            public Vector3 Position => Vector3.zero;
        }

        sealed class TestDestination : ITacticalDestination
        {
            public int MarkerId { get; set; }
            public MarkerKind Kind => MarkerKind.Cover;
            public Vector3 Position => Vector3.zero;
        }

        static SquadRegistryChecks()
        {
            if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
        }

        public static void InstallAndRun()
        {
            SquadRegistrySetup.InstallStarterScenes();
            Run();
        }

        [MenuItem("Tactical Game/Coordination/Step 02 - Run roster lifecycle checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                ProjectChecks.Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Save open scenes first.");
            Results.Clear(); stage = 0; map = 0;
            started = EditorApplication.timeSinceStartup; resumeAt = started + .5f;
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
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Squad roster checks timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < resumeAt) return;
                var game = GameSession.Instance;
                if (game == null) return;
                var squad = game.squadCoordinator;
                ProjectChecks.Require(squad != null, "Install Step 02 on the starter maps before running checks.");
                switch (stage)
                {
                    case 0:
                        ProjectChecks.Require(squad.MemberCount == 3 && squad.DestinationCount == 7, "Expected the live three-member/seven-marker registry.");
                        foreach (var enemy in game.targets)
                            ProjectChecks.Require(enemy.SquadCoordinator == squad && squad.TryGetEnemy(enemy.EnemyId, out var found) && ReferenceEquals(enemy, found), "Enemy registration/wiring.");
                        var original = game.targets[0];
                        ProjectChecks.Require(squad.RegisterEnemy(original) == EnemyRegistrationResult.AlreadyRegistered, "Repeated registration must be idempotent.");
                        ProjectChecks.Require(squad.RegisterEnemy(new TestMember { EnemyId = original.EnemyId }) == EnemyRegistrationResult.DuplicateId, "Duplicate member ID.");
                        ProjectChecks.Require(squad.RegisterEnemy(null) == EnemyRegistrationResult.InvalidEnemy, "Null member rejection.");
                        ProjectChecks.Require(squad.RegisterEnemy(new TestMember { EnemyId = "dead", IsAlive = false }) == EnemyRegistrationResult.InvalidEnemy, "Dead member rejection.");
                        transient = new TestMember { EnemyId = "04" };
                        ProjectChecks.Require(squad.RegisterEnemy(transient) == EnemyRegistrationResult.Registered && squad.MemberCount == 4, "Dynamic interface member registration.");
                        transient.IsAlive = false;
                        var marker = (TacticalMarker)squad.Destinations[0];
                        ProjectChecks.Require(squad.RegisterDestination(marker) == DestinationRegistrationResult.AlreadyRegistered, "Repeated marker registration.");
                        ProjectChecks.Require(squad.RegisterDestination(new TestDestination { MarkerId = marker.MarkerId }) == DestinationRegistrationResult.DuplicateId, "Duplicate marker ID.");
                        ProjectChecks.Require(squad.RegisterDestination(new TestDestination { MarkerId = 0 }) == DestinationRegistrationResult.InvalidDestination, "Invalid marker rejection.");
                        stage++; resumeAt = EditorApplication.timeSinceStartup + .15f; break;
                    case 1:
                        ProjectChecks.Require(squad.MemberCount == 3 && !squad.TryGetEnemy("04", out _), "Prune an invalid external member.");
                        // A rejected duplicate's disable callback must not erase the real ID owner.
                        var duplicate = game.targets[2];
                        string ownId = duplicate.EnemyId;
                        duplicate.gameObject.SetActive(false);
                        duplicate.targetId = game.targets[0].EnemyId;
                        duplicate.gameObject.SetActive(true);
                        duplicate.gameObject.SetActive(false);
                        ProjectChecks.Require(squad.TryGetEnemy(game.targets[0].EnemyId, out var owner) && ReferenceEquals(owner, game.targets[0]), "Rejected duplicate erased real owner.");
                        duplicate.targetId = ownId; duplicate.gameObject.SetActive(true);
                        ProjectChecks.Require(squad.MemberCount == 3, "Re-enabled enemy should rejoin.");
                        var duplicateMarker = (TacticalMarker)squad.Destinations[1];
                        int ownMarkerId = duplicateMarker.id;
                        int occupiedId = squad.Destinations[0].MarkerId;
                        duplicateMarker.gameObject.SetActive(false); duplicateMarker.id = occupiedId;
                        duplicateMarker.gameObject.SetActive(true); duplicateMarker.gameObject.SetActive(false);
                        ProjectChecks.Require(squad.TryGetDestination(occupiedId, out var markerOwner) && !ReferenceEquals(markerOwner, duplicateMarker), "Rejected marker erased real owner.");
                        duplicateMarker.id = ownMarkerId; duplicateMarker.gameObject.SetActive(true);
                        ProjectChecks.Require(squad.DestinationCount == 7, "Re-enabled marker should rejoin.");
                        game.targets[1].health.TakeDamage(1000);
                        ProjectChecks.Require(squad.MemberCount == 2 && !squad.TryGetEnemy(game.targets[1].EnemyId, out _), "Death must immediately remove squad membership.");
                        removedMarkerId = squad.Destinations[0].MarkerId;
                        UnityEngine.Object.Destroy(((TacticalMarker)squad.Destinations[0]).gameObject);
                        UnityEngine.Object.Destroy(game.targets[0].gameObject);
                        stage++; resumeAt = EditorApplication.timeSinceStartup + .15f; break;
                    case 2:
                        ProjectChecks.Require(squad.MemberCount == 1 && squad.DestinationCount == 6 && !squad.TryGetDestination(removedMarkerId, out _), "Destroyed objects left stale registry entries.");
                        squad.ResetRound();
                        ProjectChecks.Require(squad.MemberCount == 0 && squad.DestinationCount == 0, "Reset must clear both registries.");
                        squad.RegisterConfiguredObjects();
                        ProjectChecks.Require(squad.MemberCount == 1 && squad.DestinationCount == 6, "Rebuild must exclude dead/destroyed members and markers.");
                        squad.enabled = false;
                        ProjectChecks.Require(squad.MemberCount == 0 && squad.DestinationCount == 0, "Disabled coordinator must clear its registry.");
                        squad.enabled = true;
                        ProjectChecks.Require(squad.MemberCount == 1 && squad.DestinationCount == 6, "Re-enabled coordinator must restore eligible configured objects.");
                        Results.Add("PASS " + game.missionName + ": startup IDs, duplicate/invalid rejection, idempotence, duplicate-owner safety, re-enable, death, destruction, reset, and rebuild.");
                        previous = squad;
                        game.Restart();
                        stage++; resumeAt = EditorApplication.timeSinceStartup + .4f; break;
                    case 3:
                        ProjectChecks.Require(previous == null && squad.MemberCount == 3 && squad.DestinationCount == 7, "Scene restart must create a fresh squad registry.");
                        Results.Add("PASS " + game.missionName + ": scene restart creates a fresh three-member/seven-marker registry.");
                        if (map == 0)
                        {
                            map = 1; stage = 0;
                            game.LoadMission("RailDepot");
                            resumeAt = EditorApplication.timeSinceStartup + .4f;
                        }
                        else Finish(false);
                        break;
                }
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); Debug.LogException(error); Finish(true);
            }
        }

        static void Finish(bool failed)
        {
            SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
            Directory.CreateDirectory("Artifacts/Coordination");
            File.WriteAllLines("Artifacts/Coordination/step-02-registry-checks.txt", Results);
            Debug.Log(string.Join("\n", Results));
            EditorApplication.isPlaying = false;
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
