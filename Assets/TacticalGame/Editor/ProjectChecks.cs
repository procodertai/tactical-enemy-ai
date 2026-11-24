using System;
using System.Collections.Generic;
using System.IO;
using TacticalGame.Coordination;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame.Editor
{
    public static class ProjectChecks
    {
        [MenuItem("Tactical Game/Validate both maps")]
        public static void ValidateMaps()
        {
            string original = SceneManager.GetActiveScene().path;
            foreach (string path in new[] { ProjectBuilder.SubstationPath, ProjectBuilder.DepotPath })
            {
                EditorSceneManager.OpenScene(path);
                var game = FindOne<GameSession>();
                Require(game != null && game.player != null, path + ": missing mission or player");
                Require(game.objectives.Length == 2 && game.targets.Length == 3, path + ": expected 2 terminals and 3 targets");
                Require(FindAll<ExitGate>().Length == 1 && game.exitGate != null, path + ": expected one wired exit");
                Require(game.player.rifle != null && game.player.viewCamera != null && game.overviewCamera != null, "Missing weapon/camera wiring");
                foreach (var objective in game.objectives)
                    Require(objective.screen != null && objective.statusText != null && objective.onlineMaterial != null, "Incomplete terminal wiring");
                foreach (var target in game.targets)
                    Require(target.health != null && target.body != null && target.chest != null, "Incomplete target wiring");
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script on " + transform.name);
                foreach (var renderer in FindAll<Renderer>())
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                        foreach (var material in renderer.sharedMaterials)
                            Require(material != null && material.shader != null, "Missing template material: " + renderer.name);
                Physics.SyncTransforms();
                CheckRoute(game, false);
                CheckRoute(game, true);
                Debug.Log("PASS " + path + ": component wiring, missing scripts and two walking routes");
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            Debug.Log("LAST LIGHT: map validation passed.");
        }

        static void CheckRoute(GameSession game, bool east)
        {
            float x = east ? 6f : -6f;
            var route = new[] { new Vector3(0, 0, -16), new Vector3(x, 0, -14), new Vector3(x, 0, -9), new Vector3(x, 0, 13), new Vector3(0, 0, 16) };
            // Check samples along the clear circulation lanes. Ignore the optional dummy bodies.
            for (int segment = 1; segment < route.Length; segment++)
            {
                int samples = Mathf.CeilToInt(Vector3.Distance(route[segment - 1], route[segment]) / .4f);
                for (int i = 0; i <= samples; i++)
                {
                    Vector3 position = Vector3.Lerp(route[segment - 1], route[segment], i / (float)samples);
                    foreach (var collider in Physics.OverlapCapsule(position + Vector3.up * .5f, position + Vector3.up * 1.45f, .32f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    {
                        if (collider.GetComponentInParent<TargetDummy>() != null) continue;
                        Require(false, game.missionName + " circulation blocked near " + position + " by " + collider.name);
                    }
                }
            }
        }

        [MenuItem("Tactical Game/Build Windows game")]
        public static void BuildWindows()
        {
            ValidateMaps();
            const string outputOption = "-lastLightBuildOutput";
            string output = "Builds/LastLight/LastLight.exe";
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, outputOption);
            if (index >= 0)
            {
                Require(index + 1 < args.Length, "Missing path after " + outputOption);
                output = Path.GetFullPath(args[index + 1]);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectBuilder.SubstationPath, ProjectBuilder.DepotPath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Require(report.summary.result == BuildResult.Succeeded, "Windows build failed: " + report.summary.result);
            Debug.Log("LAST LIGHT: Windows game built at " + output);
        }

        public static void GenerateAndBuild()
        {
            ProjectBuilder.BuildAll();
            BuildWindows();
        }

        public static void GenerateAndCheckGameplay()
        {
            ProjectBuilder.BuildAll();
            PlayChecks.Run();
        }

        public static void GenerateBuildAndCheck()
        {
            GenerateAndBuild();
            PlayChecks.Run();
        }

        internal static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        internal static T FindOne<T>() where T : UnityEngine.Object
        {
            #if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<T>();
            #else
            return UnityEngine.Object.FindObjectOfType<T>();
            #endif
        }

        internal static T[] FindAll<T>() where T : UnityEngine.Object
        {
            #if UNITY_6000_6_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>();
            #elif UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            #else
            return UnityEngine.Object.FindObjectsOfType<T>();
            #endif
        }
    }

    // Runs real game components in Play mode without the external Test Framework package.
    [InitializeOnLoad]
    public static class PlayChecks
    {
        static double started;
        static int stage;
        static int sceneIndex;
        static float waitUntil;
        static int initialReserve;
        static float pausedRemaining;
        static bool oldOptionsEnabled;
        static EnterPlayModeOptions oldOptions;
        static GameObject guardTestWall;
        static float guardTestHealth;
        static int guardTestShots;
        static Vector3 guardTestPosition, rememberedPlayerPosition;
        static float sharedObservationTime;
        static Vector3 tacticalTestSighting;
        static float tacticalTestDeadline;
        static int tacticalArrivals;
        static float detourDeadline;
        static bool sawDetourStride;
        static readonly Vector3 RetreatMarker = new Vector3(-6.5f, 0f, -8.5f);
        static readonly Vector3 RetreatThreat = new Vector3(-6.5f, 0f, -15.5f);
        static bool sawRetreatMove;
        static readonly List<string> Results = new List<string>();
        const string Running = "LastLight.PlayChecks";

        static PlayChecks()
        {
            if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Tactical Game/Run gameplay smoke checks")]
        public static void Run()
        {
            ProjectChecks.ValidateMaps();
            Results.Clear();
            stage = 0;
            sceneIndex = 0;
            started = EditorApplication.timeSinceStartup;
            waitUntil = 0;
            SessionState.SetBool(Running, true);
            oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(ProjectBuilder.SubstationPath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Play checks timed out");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                var game = GameSession.Instance;
                if (game == null || game.player == null || !game.player.rifle.enabled) return;
                var player = game.player;
                var rifle = player.rifle;
                if (Time.unscaledTime < waitUntil) return;
                switch (stage)
                {
                    case 0:
                        ProjectChecks.Require(game.State == RoundState.Briefing, "Round must start at briefing");
                        ProjectChecks.Require(player.health.Current == 100 && rifle.Ammo == 24 && rifle.Reserve == 120, "Initial health/ammo");
                        foreach (var guard in ProjectChecks.FindAll<BasicGuard>()) guard.enabled = false;
                        foreach (var chase in ProjectChecks.FindAll<AutoChase>()) chase.enabled = false;
                        ProjectChecks.Require(player.GetComponents<PlayerNoiseEmitter>().Length == 1, "One noise emitter on the player");
                        foreach (var target in game.targets)
                        {
                            var sensors = target.GetComponents<GuardPerception>();
                            ProjectChecks.Require(sensors.Length == 1 && !sensors[0].HasLastKnown, "One fresh sensor per guard");
                            ProjectChecks.Require(target.TryGetComponent<UtilityEnemyController>(out var utility) && utility.enabled &&
                                target.GetComponent<SquadMovementFeedback>().NavigationConnected, "Utility AI and live squad movement wired");
                            utility.enabled = false;
                        }
                        ProjectChecks.Require(game.targets[0].GetComponentInChildren<SkinnedMeshRenderer>() != null, "Template character mesh");
                        game.Begin();
                        stage++; waitUntil = Time.unscaledTime + .3f; break;
                    case 1:
                        var exit = ProjectChecks.FindOne<ExitGate>();
                        exit.Interact(player);
                        ProjectChecks.Require(game.State == RoundState.Playing && !game.ExitUnlocked, "Exit must stay locked");
                        // Aim at a dummy from an unobstructed position, then fire the real rifle.
                        Place(player, game.targets[0].transform.position + new Vector3(0, 0, -4));
                        player.viewCamera.transform.LookAt(game.targets[0].transform.position + Vector3.up * 1.35f);
                        Physics.SyncTransforms();
                        ProjectChecks.Require(rifle.TryFire(), "Rifle did not fire");
                        ProjectChecks.Require(game.targets[0].health.Current == 75 && rifle.Ammo == 23 && game.ShotsHit == 1, "Hitscan damage/ammunition");
                        initialReserve = rifle.Reserve;
                        rifle.TryReload();
                        stage++; waitUntil = Time.unscaledTime + 2f; break;
                    case 2:
                        ProjectChecks.Require(!rifle.IsReloading && rifle.Ammo == 24 && rifle.Reserve == initialReserve - 1, "Reload accounting");
                        // Put a physical wall on the exact ray that just damaged this dummy.
                        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        wall.transform.position = game.targets[0].transform.position + new Vector3(0, 1.4f, -2f);
                        wall.transform.localScale = new Vector3(3, 3, .3f);
                        Physics.SyncTransforms();
                        var hpBefore = game.targets[0].health.Current;
                        ProjectChecks.Require(rifle.TryFire(), "Second rifle shot");
                        ProjectChecks.Require(game.targets[0].health.Current == hpBefore && game.ShotsHit == 1, "Wall occlusion");
                        UnityEngine.Object.Destroy(wall);
                        game.targets[0].health.TakeDamage(1000);
                        ProjectChecks.Require(!game.targets[0].health.IsAlive, "Target defeat");
                        foreach (var collider in game.targets[0].GetComponentsInChildren<Collider>())
                            ProjectChecks.Require(!collider.enabled, "Defeated target colliders");
                        player.health.TakeDamage(40);
                        foreach (var supply in ProjectChecks.FindAll<SupplyCrate>()) if (supply.medical) supply.Interact(player);
                        ProjectChecks.Require(player.health.Current == 100, "Medical supplies");
                        int reserveBefore = rifle.Reserve;
                        foreach (var supply in ProjectChecks.FindAll<SupplyCrate>()) if (!supply.medical) { supply.Interact(player); break; }
                        ProjectChecks.Require(rifle.Reserve == reserveBefore + 48, "Ammo supplies");
                        game.Pause(); pausedRemaining = game.Remaining;
                        stage++; waitUntil = Time.unscaledTime + .3f; break;
                    case 3:
                        ProjectChecks.Require(game.Remaining == pausedRemaining && Time.timeScale == 0, "Pause must freeze clock");
                        game.Resume();
                        game.ToggleOverview();
                        ProjectChecks.Require(!game.CanControl && game.overviewCamera.enabled && !player.viewCamera.enabled, "Overview control lock");
                        game.ToggleOverview();
                        game.objectives[1].Interact(player);
                        game.objectives[1].Interact(player);
                        ProjectChecks.Require(game.Completed == 1 && !game.ExitUnlocked, "Objective idempotence");
                        game.objectives[0].Interact(player);
                        ProjectChecks.Require(game.ExitUnlocked, "Both objectives unlock exit");
                        ProjectChecks.FindOne<ExitGate>().Interact(player);
                        ProjectChecks.Require(game.State == RoundState.Won, "Extraction victory");
                        Results.Add("PASS " + game.missionName + ": briefing, locked exit, hitscan damage, reload, wall occlusion, medical/ammo supplies, pause, overview, objective order/idempotence, win");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 4:
                        ProjectChecks.Require(game.State == RoundState.Briefing && game.Completed == 0 && player.health.Current == 100 && rifle.Ammo == 24 && rifle.Reserve == 120, "Restart state reset");
                        game.Begin();
                        var hazard = ProjectChecks.FindOne<DamageZone>();
                        Place(player, hazard.transform.position);
                        stage++; waitUntil = Time.unscaledTime + .3f; break;
                    case 5:
                        ProjectChecks.Require(player.health.Current < 100, "Electrical hazard damage");
                        player.health.TakeDamage(1000);
                        ProjectChecks.Require(game.State == RoundState.Lost, "Health defeat");
                        Results.Add("PASS " + game.missionName + ": clean restart, electrical hazard and health defeat");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 6:
                        var firstGuard = game.targets[0];
                        var firstUtility = firstGuard.GetComponent<UtilityEnemyController>();
                        foreach (var utility in ProjectChecks.FindAll<UtilityEnemyController>()) utility.enabled = utility == firstUtility;
                        var firstSensor = firstGuard.GetComponent<GuardPerception>();
                        foreach (var sensor in ProjectChecks.FindAll<GuardPerception>()) sensor.enabled = sensor == firstSensor;
                        guardTestPosition = firstGuard.transform.position;
                        game.targets[0].body.rotation = Quaternion.LookRotation(Vector3.forward);
                        game.Begin();
                        Place(player, firstGuard.transform.position + new Vector3(0, 0, -4));
                        stage++; waitUntil = Time.unscaledTime + .65f; break;
                    case 7:
                        var unaware = game.targets[0].GetComponent<GuardPerception>();
                        ProjectChecks.Require(!unaware.CanSeeTarget && !unaware.HasLastKnown, "Player outside the cone stays undetected");
                        ProjectChecks.Require(!game.squadCoordinator.TryGetSharedSighting(out _), "Undetected player stays unknown to the squad");
                        ProjectChecks.Require(game.targets[0].transform.position == guardTestPosition, "Chase must not follow an undetected player");
                        SetFirstGuardNavigation(false);
                        game.targets[0].body.rotation = Quaternion.LookRotation(Vector3.back);
                        unaware.secondsToNotice = .1f;
                        stage++; waitUntil = Time.unscaledTime + .35f; break;
                    case 8:
                        var seeing = game.targets[0].GetComponent<GuardPerception>();
                        ProjectChecks.Require(seeing.CanSeeTarget && seeing.State == GuardPerception.AwarenessState.Alerted, "Player inside the cone is detected");
                        ProjectChecks.Require(game.squadCoordinator.TryGetSharedSighting(out var known) &&
                            known.Report.ReporterId == game.targets[0].EnemyId &&
                            (known.Report.ObservedPosition - seeing.LastKnownPosition).sqrMagnitude < .01f, "Confirmed sighting reaches the squad");
                        ProjectChecks.Require(player.health.Current == 100 && game.targets[0].GetComponent<EnemyWeapon>().ShotsFired == 0, "Guard warning delay");
                        stage++; waitUntil = Time.unscaledTime + 1.15f; break;
                    case 9:
                        ProjectChecks.Require(player.health.Current == 92, "Utility guard must actually damage player");
                        guardTestWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        guardTestWall.transform.position = game.targets[0].transform.position + new Vector3(0, 1.4f, -2);
                        guardTestWall.transform.localScale = new Vector3(3, 3, .3f);
                        Physics.SyncTransforms();
                        guardTestHealth = player.health.Current;
                        guardTestShots = game.targets[0].GetComponent<EnemyWeapon>().ShotsFired;
                        rememberedPlayerPosition = game.targets[0].GetComponent<GuardPerception>().LastKnownPosition;
                        ProjectChecks.Require(game.squadCoordinator.TryGetSharedSighting(out var lastReport), "Shared sighting before cover");
                        sharedObservationTime = lastReport.Report.ObservedAt;
                        Place(player, player.transform.position + Vector3.right * .5f);
                        stage++; waitUntil = Time.unscaledTime + 2f; break;
                    case 10:
                        ProjectChecks.Require(player.health.Current == guardTestHealth && game.targets[0].GetComponent<EnemyWeapon>().ShotsFired == guardTestShots, "Cover must block guard fire");
                        var behindCover = game.targets[0].GetComponent<GuardPerception>();
                        ProjectChecks.Require(!behindCover.CanSeeTarget && behindCover.HasLastKnown && behindCover.TimeSinceLastSeen > 1f &&
                            behindCover.LastKnownPosition == rememberedPlayerPosition, "Cover preserves the last seen position as it ages");
                        ProjectChecks.Require(game.squadCoordinator.TryGetSharedSighting(out var aged) && aged.Report.ObservedAt == sharedObservationTime,
                            "Cover prevents new squad sightings");
                        behindCover.memoryDuration = .1f;
                        behindCover.decayPerSecond = 10f;
                        stage++; waitUntil = Time.unscaledTime + .3f; break;
                    case 11:
                        ProjectChecks.Require(!game.targets[0].GetComponent<GuardPerception>().HasLastKnown, "Local memory expires without a new stimulus");
                        ProjectChecks.Require(rifle.TryFire(), "Fire behind cover to test hearing");
                        stage++; waitUntil = Time.unscaledTime + .08f; break;
                    case 12:
                        var hearing = game.targets[0].GetComponent<GuardPerception>();
                        ProjectChecks.Require(!hearing.CanSeeTarget && hearing.HasLastKnown && hearing.TimeSinceLastSeen > 1f &&
                            Vector3.Distance(hearing.LastKnownPosition, player.transform.position) <=
                                hearing.SoundErrorAt(Vector3.Distance(hearing.transform.position, player.transform.position), 3) + .01f &&
                            hearing.State == GuardPerception.AwarenessState.Suspicious, "Gunshot provides a sound position through cover");
                        ProjectChecks.Require(game.squadCoordinator.TryGetSharedSighting(out var retainedSighting) &&
                            retainedSighting.Report.ObservedAt == sharedObservationTime, "Hearing cannot refresh a visual sighting");
                        hearing.memoryDuration = 4f;
                        SetFirstGuardNavigation(true);
                        stage++; waitUntil = Time.unscaledTime + .5f; break;
                    case 13:
                        ProjectChecks.Require(Vector3.Distance(game.targets[0].transform.position, guardTestPosition) > .05f,
                            "Utility AI investigates the remembered sound via A*");
                        UnityEngine.Object.DestroyImmediate(guardTestWall);
                        game.targets[0].health.TakeDamage(1000);
                        stage++; waitUntil = Time.unscaledTime + 2f; break;
                    case 14:
                        ProjectChecks.Require(player.health.Current == guardTestHealth && game.targets[0].GetComponent<EnemyWeapon>().ShotsFired == guardTestShots, "Dead guard must stop firing");
                        Results.Add("PASS " + game.missionName + ": cone detection, shared sighting, guard warning and damage, occlusion, memory aging and expiry, hearing, death stops attacks");
                        Results.Add("PASS " + game.missionName + ": Utility movement requires knowledge and investigates a heard position through A*");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 15:
                        // Shorten only the test instance's clock, then let GameSession.Update expire it.
                        typeof(GameSession).GetProperty("Remaining").SetValue(game, .1f);
                        game.Begin();
                        stage++; waitUntil = Time.unscaledTime + .3f; break;
                    case 16:
                        ProjectChecks.Require(game.State == RoundState.Lost && game.Remaining == 0f, "Actual timer expiry");
                        Results.Add("PASS " + game.missionName + ": timer expiry");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 17:
                        ProjectChecks.Require(game.squadCoordinator.Claims.Count == 0 &&
                            !game.squadCoordinator.TryGetSharedSighting(out _), "Fresh round clears squad claims and knowledge");
                        foreach (var sensor in ProjectChecks.FindAll<GuardPerception>()) sensor.enabled = false;
                        var attacker = game.targets[0];
                        attacker.GetComponent<UtilityEnemyController>().enabled = false;
                        tacticalTestSighting = FindTacticalTestSighting(game);
                        tacticalArrivals = 0;
                        foreach (var target in game.targets)
                            target.GetComponent<PathMover>().MovementReported += report =>
                            {
                                if (report.Status == MovementStatus.Arrived) tacticalArrivals++;
                            };
                        game.Begin();
                        game.ToggleOverview();
                        ProjectChecks.Require(game.squadCoordinator.ReportSighting(new SightingReport(attacker.EnemyId, tacticalTestSighting, Time.time)),
                            "Tactical test supplies a shared position");
                        tacticalTestDeadline = Time.unscaledTime + 9f;
                        stage++; waitUntil = Time.unscaledTime + .45f; break;
                    case 18:
                        var flanker = game.targets[1];
                        var support = game.targets[2];
                        ProjectChecks.Require(flanker.GetComponent<UtilityEnemyController>().CurrentAction == EnemyAction.Flank &&
                            support.GetComponent<UtilityEnemyController>().CurrentAction == EnemyAction.Support, "Utility AI uses the squad roles");
                        var registrations = game.squadCoordinator;
                        ProjectChecks.Require(registrations.TryGetDestinationClaim(flanker.EnemyId, out var flank) &&
                            registrations.TryGetDestination(flank.MarkerId, out var flankMarker) && flankMarker.Kind == MarkerKind.Flank,
                            "Flanker reserves a reachable flank marker");
                        ProjectChecks.Require(registrations.TryGetDestinationClaim(support.EnemyId, out var cover) &&
                            registrations.TryGetDestination(cover.MarkerId, out var coverMarker) && coverMarker.Kind == MarkerKind.Cover,
                            "Support reserves a separate covered marker");
                        stage++; break;
                    case 19:
                        if (tacticalArrivals == 0 && Time.unscaledTime < tacticalTestDeadline)
                        {
                            game.squadCoordinator.ReportSighting(new SightingReport(game.targets[0].EnemyId, tacticalTestSighting, Time.time));
                            waitUntil = Time.unscaledTime + .45f;
                            break;
                        }
                        ProjectChecks.Require(tacticalArrivals > 0, "Live A* navigator reports arrival at a claimed destination");
                        var defeated = game.targets[1];
                        defeated.health.TakeDamage(1000);
                        ProjectChecks.Require(!game.squadCoordinator.TryGetDestinationClaim(defeated.EnemyId, out _),
                            "Defeat releases the guard's tactical claim");
                        Results.Add("PASS " + game.missionName + ": role-based actions, reachable flank and cover claims, live A* arrival, defeat cleanup and round reset");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 20:
                        foreach (var sensor in ProjectChecks.FindAll<GuardPerception>()) sensor.enabled = false;
                        var injured = game.targets[0];
                        foreach (var controller in ProjectChecks.FindAll<UtilityEnemyController>())
                            controller.enabled = controller.gameObject == injured.gameObject;
                        var markerIds = new List<int>();
                        foreach (var marker in game.squadCoordinator.Destinations) markerIds.Add(marker.MarkerId);
                        foreach (int id in markerIds) game.squadCoordinator.UnregisterDestination(id);
                        injured.health.TakeDamage(80f);
                        game.Begin();
                        game.ToggleOverview();
                        ProjectChecks.Require(game.squadCoordinator.ReportSighting(new SightingReport(injured.EnemyId, new Vector3(-6.5f, 0, -9.5f), Time.time)),
                            "Movement fallback test supplies known target");
                        stage++; waitUntil = Time.unscaledTime + 1.25f; break;
                    case 21:
                        var lead = game.targets[0];
                        var leadController = lead.GetComponent<UtilityEnemyController>();
                        ProjectChecks.Require(leadController.CurrentAction == EnemyAction.Investigate,
                            "A failed cover and retreat choice lets the injured guard investigate");
                        leadController.enabled = false;
                        var stationary = game.targets[2];
                        lead.transform.position = new Vector3(-6.5f, lead.transform.position.y, -14.5f);
                        stationary.transform.position = new Vector3(-6.5f, stationary.transform.position.y, -12.5f);
                        Physics.SyncTransforms();
                        ProjectChecks.Require(lead.GetComponent<PathMover>().MoveTo(new Vector3(-6.5f, 0, -8.5f)), "Stationary-blocker test starts with an A* route");
                        sawDetourStride = false;
                        detourDeadline = Time.time + 5f;
                        stage++; break;
                    case 22:
                        var legs = game.targets[0].GetComponent<GuardLocomotion>();
                        ProjectChecks.Require(legs.HasLegRig, "Locomotion finds the legs on the actual guard rig");
                        sawDetourStride |= legs.HasStride;
                        var detourMover = game.targets[0].GetComponent<PathMover>();
                        if (detourMover.IsMoving && Time.time < detourDeadline) break;
                        Vector3 position = game.targets[0].Position;
                        ProjectChecks.Require(Mathf.Abs(position.x + 6.5f) < .25f && Mathf.Abs(position.z + 8.5f) < .25f &&
                            game.squadCoordinator.Metrics.ObstructionReports > 0,
                            $"Priority guard detours around a stationary teammate. End position {position}, path failed {detourMover.PathFailed}, " +
                            $"obstruction reports {game.squadCoordinator.Metrics.ObstructionReports}");
                        ProjectChecks.Require(sawDetourStride, "The actual guard model animates its legs while moving");
                        Results.Add("PASS " + game.missionName + ": unavailable movement fallback, stationary-teammate detour and movement animation");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 23:
                        var retreating = game.targets[0];
                        foreach (var controller in ProjectChecks.FindAll<UtilityEnemyController>())
                            controller.enabled = controller.gameObject == retreating.gameObject;
                        foreach (var sensor in ProjectChecks.FindAll<GuardPerception>()) sensor.enabled = false;
                        var previousMarkers = new List<int>();
                        foreach (var marker in game.squadCoordinator.Destinations) previousMarkers.Add(marker.MarkerId);
                        foreach (int id in previousMarkers) game.squadCoordinator.UnregisterDestination(id);
                        var fallbackMarker = new GameObject("Regression fallback marker").AddComponent<TacticalMarker>();
                        fallbackMarker.kind = MarkerKind.Fallback;
                        fallbackMarker.id = 81;
                        fallbackMarker.transform.position = RetreatMarker;
                        game.squadCoordinator.RegisterDestination(fallbackMarker);
                        retreating.transform.position = new Vector3(-6.5f, retreating.Position.y, -11.5f);
                        retreating.health.TakeDamage(75f);
                        Physics.SyncTransforms();
                        game.Begin();
                        ProjectChecks.Require(game.squadCoordinator.ReportSighting(new SightingReport(retreating.EnemyId, RetreatThreat, Time.time)),
                            "Retreat recovery test supplies a known threat");
                        sawRetreatMove = false;
                        tacticalTestDeadline = Time.unscaledTime + 4f;
                        stage++; break;
                    case 24:
                        var recovering = game.targets[0];
                        var retreatNavigator = recovering.GetComponent<PathMover>();
                        sawRetreatMove |= recovering.GetComponent<UtilityEnemyController>().CurrentAction == EnemyAction.FallBack && retreatNavigator.IsMoving;
                        if ((!sawRetreatMove || retreatNavigator.IsMoving) && Time.unscaledTime < tacticalTestDeadline) break;
                        ProjectChecks.Require(sawRetreatMove && Vector2.Distance(new Vector2(recovering.Position.x, recovering.Position.z),
                            new Vector2(RetreatMarker.x, RetreatMarker.z)) < .25f, "A critically injured guard physically reaches the retreat marker");
                        Place(player, RetreatThreat);
                        var recoveringSensor = recovering.GetComponent<GuardPerception>();
                        recoveringSensor.secondsToNotice = .1f;
                        recoveringSensor.enabled = true;
                        tacticalTestDeadline = Time.unscaledTime + 3.5f;
                        stage++; break;
                    case 25:
                        var returnedFire = game.targets[0].GetComponent<EnemyWeapon>();
                        if (returnedFire.ShotsFired == 0 && Time.unscaledTime < tacticalTestDeadline) break;
                        ProjectChecks.Require(returnedFire.ShotsFired > 0 && player.health.Current < player.health.maximum,
                            "Guard resumes real fire after withdrawing and reacquiring the player");
                        guardTestShots = returnedFire.ShotsFired;
                        guardTestPosition = game.targets[0].Position;
                        guardTestWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        guardTestWall.transform.position = (guardTestPosition + player.transform.position) * .5f + Vector3.up * 1.4f;
                        guardTestWall.transform.localScale = new Vector3(3, 3, .3f);
                        Physics.SyncTransforms();
                        tacticalTestDeadline = Time.unscaledTime + 4f;
                        stage++; waitUntil = Time.unscaledTime + .8f; break;
                    case 26:
                        var resumed = game.targets[0];
                        // Give a partial magazine time to reload safely if the unsettled guard chose Reload.
                        if (resumed.GetComponent<EnemyWeapon>().IsReloading && Time.unscaledTime < tacticalTestDeadline)
                        {
                            waitUntil = Time.unscaledTime + .2f;
                            break;
                        }
                        ProjectChecks.Require(resumed.GetComponent<EnemyWeapon>().ShotsFired == guardTestShots,
                            "Returning to cover blocks fire from the recovered guard");
                        if (Vector3.Distance(resumed.Position, guardTestPosition) < .05f && Time.unscaledTime < tacticalTestDeadline)
                        {
                            waitUntil = Time.unscaledTime + .1f;
                            break;
                        }
                        ProjectChecks.Require(Vector3.Distance(resumed.Position, guardTestPosition) > .05f,
                            "Guard can leave the completed retreat position after losing sight");
                        UnityEngine.Object.DestroyImmediate(guardTestWall);
                        Results.Add("PASS " + game.missionName + ": withdrawal completes, guard returns fire and can move again");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 27:
                        var patrolling = game.targets[0];
                        var patrol = patrolling.GetComponent<GuardScout>();
                        ProjectChecks.Require(patrol != null, "Scene includes DepotNavigation patrol and search");
                        foreach (var controller in ProjectChecks.FindAll<UtilityEnemyController>())
                            controller.enabled = controller.gameObject == patrolling.gameObject;
                        foreach (var sensor in ProjectChecks.FindAll<GuardPerception>()) sensor.enabled = false;
                        patrolling.transform.position = new Vector3(-6.5f, patrolling.Position.y, -14.5f);
                        patrol.waitMin = patrol.waitMax = 0f;
                        patrol.patrolMin = patrol.patrolMax = 2f;
                        Physics.SyncTransforms();
                        guardTestPosition = patrolling.Position;
                        game.Begin();
                        tacticalTestDeadline = Time.time + 3f;
                        stage++; break;
                    case 28:
                        var walker = game.targets[0];
                        bool patrolMoved = Vector3.Distance(walker.Position, guardTestPosition) > .2f;
                        if (!patrolMoved && Time.time < tacticalTestDeadline) break;
                        ProjectChecks.Require(patrolMoved && walker.GetComponent<GuardScout>().IsUtilityScouting,
                            "Guard moves on an idle patrol before any observation");
                        var patrolSighting = new Vector3(-6.5f, 0, -8.5f);
                        ProjectChecks.Require(game.squadCoordinator.ReportSighting(new SightingReport(walker.EnemyId, patrolSighting, Time.time)),
                            "Coordinator accepts an observation while the guard patrols");
                        tacticalTestDeadline = Time.time + 4f;
                        stage++; break;
                    case 29:
                        var investigator = game.targets[0];
                        var searching = investigator.GetComponent<GuardScout>();
                        bool leftPatrol = !searching.IsUtilityScouting &&
                            investigator.GetComponent<UtilityEnemyController>().CurrentAction == EnemyAction.Investigate;
                        if (!leftPatrol && Time.time < tacticalTestDeadline) break;
                        ProjectChecks.Require(leftPatrol, "A tactical order interrupts idle patrol");
                        stage++; break;
                    case 30:
                        var searcher = game.targets[0].GetComponent<GuardScout>();
                        bool startedSearch = searcher.IsUtilityScouting && searcher.state == ScoutState.Search;
                        if (!startedSearch && Time.time < tacticalTestDeadline) break;
                        ProjectChecks.Require(startedSearch, "Guard searches around its last reported position after investigating it");
                        Results.Add("PASS " + game.missionName + ": patrol hands off to tactical investigation and then local search");
                        if (sceneIndex == 0)
                        {
                            sceneIndex = 1;
                            game.LoadMission("RailDepot");
                            stage = 0; waitUntil = Time.unscaledTime + .4f;
                        }
                        else Complete();
                        break;
                }

                void SetFirstGuardNavigation(bool enabled)
                {
                    var guard = game.targets[0];
                    guard.GetComponent<UtilityEnemyController>().SetNavigation(enabled ? guard.GetComponent<PathMover>() : null);
                }
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error);
                Debug.LogException(error);
                Complete(true);
            }
        }

        static Vector3 FindTacticalTestSighting(GameSession game)
        {
            var positioning = Positioning.Instance;
            for (int x = -16; x <= 16; x += 4)
                for (int z = -16; z <= 16; z += 4)
                {
                    var point = new Vector3(x, 0, z);
                    var sighting = new SightingReport(game.targets[0].EnemyId, point, Time.time);
                    if (positioning.GetReachableCandidates(game.targets[1].EnemyId, MarkerKind.Flank, sighting).Count > 0 &&
                        positioning.GetReachableCandidates(game.targets[2].EnemyId, MarkerKind.Cover, sighting).Count > 0)
                        return point;
                }
            throw new InvalidOperationException(game.missionName + " needs reachable flank and covered test positions.");
        }

        static void Place(PlayerController player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position + Vector3.up * .1f;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        static void Complete(bool failed = false)
        {
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Artifacts");
            File.WriteAllLines("Artifacts/gameplay-checks.txt", Results);
            Debug.Log(string.Join("\n", Results));
            EditorApplication.isPlaying = false;
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
