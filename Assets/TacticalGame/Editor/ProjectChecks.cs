using System;
using System.Collections.Generic;
using System.IO;
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
            Directory.CreateDirectory("Builds/LastLight");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectBuilder.SubstationPath, ProjectBuilder.DepotPath },
                locationPathName = "Builds/LastLight/LastLight.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Require(report.summary.result == BuildResult.Succeeded, "Windows build failed: " + report.summary.result);
            Debug.Log("LAST LIGHT: Windows game built at Builds/LastLight/LastLight.exe");
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
                        foreach (var guard in ProjectChecks.FindAll<BasicGuard>()) guard.enabled = false;
                        var firstGuard = game.targets[0].GetComponent<BasicGuard>();
                        firstGuard.enabled = true;
                        game.Begin();
                        Place(player, firstGuard.transform.position + new Vector3(0, 0, -4));
                        stage++; waitUntil = Time.unscaledTime + .3f; break;
                    case 7:
                        ProjectChecks.Require(player.health.Current == 100 && game.targets[0].GetComponent<BasicGuard>().ShotsFired == 0, "Guard warning delay");
                        stage++; waitUntil = Time.unscaledTime + 1.3f; break;
                    case 8:
                        ProjectChecks.Require(player.health.Current == 92, "Basic guard must actually damage player");
                        guardTestWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        guardTestWall.transform.position = game.targets[0].transform.position + new Vector3(0, 1.4f, -2);
                        guardTestWall.transform.localScale = new Vector3(3, 3, .3f);
                        Physics.SyncTransforms();
                        guardTestHealth = player.health.Current;
                        guardTestShots = game.targets[0].GetComponent<BasicGuard>().ShotsFired;
                        stage++; waitUntil = Time.unscaledTime + 2f; break;
                    case 9:
                        ProjectChecks.Require(player.health.Current == guardTestHealth && game.targets[0].GetComponent<BasicGuard>().ShotsFired == guardTestShots, "Cover must block guard fire");
                        UnityEngine.Object.DestroyImmediate(guardTestWall);
                        game.targets[0].health.TakeDamage(1000);
                        stage++; waitUntil = Time.unscaledTime + 2f; break;
                    case 10:
                        ProjectChecks.Require(player.health.Current == guardTestHealth && game.targets[0].GetComponent<BasicGuard>().ShotsFired == guardTestShots, "Dead guard must stop firing");
                        Results.Add("PASS " + game.missionName + ": template character, guard warning delay, real enemy damage, cover blocking enemy shots, death stops attacks");
                        game.Restart();
                        stage++; waitUntil = Time.unscaledTime + .4f; break;
                    case 11:
                        // Shorten only the test instance's clock, then let GameSession.Update expire it.
                        typeof(GameSession).GetProperty("Remaining").SetValue(game, .1f);
                        game.Begin();
                        stage++; waitUntil = Time.unscaledTime + .3f; break;
                    case 12:
                        ProjectChecks.Require(game.State == RoundState.Lost && game.Remaining == 0f, "Actual timer expiry");
                        Results.Add("PASS " + game.missionName + ": timer expiry");
                        if (sceneIndex == 0)
                        {
                            sceneIndex = 1;
                            game.LoadMission("RailDepot");
                            stage = 0; waitUntil = Time.unscaledTime + .4f;
                        }
                        else Complete();
                        break;
                }
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error);
                Debug.LogException(error);
                Complete(true);
            }
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
