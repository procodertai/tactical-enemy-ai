using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticalGame.Editor
{
    public static class UtilityAiDemo
    {
        const string DemoPath = "Assets/TacticalGame/Scenes/UtilityAiDemo.unity";

        [MenuItem("Tactical Game/Utility AI/Create standalone demo")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (File.Exists(DemoPath)) throw new InvalidOperationException("Demo already exists. Open it to preserve your changes.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ProjectBuilder.SubstationPath);
            // Save a copy before touching any scene objects.
            if (!EditorSceneManager.SaveScene(scene, DemoPath)) throw new IOException("Could not create demo scene.");
            var guards = ProjectChecks.FindAll<BasicGuard>();
            Array.Sort(guards, (a, b) => string.CompareOrdinal(a.GetComponent<TargetDummy>().targetId, b.GetComponent<TargetDummy>().targetId));
            if (guards.Length == 0) throw new InvalidOperationException("Substation contains no guards.");
            var first = guards[0];
            var game = ProjectChecks.FindOne<GameSession>();
            game.useUtilitySquad = false;
            game.allowDemoInvulnerability = true;
            foreach (var guard in guards)
            {
                guard.enabled = false;
                // Only guard 01 moves, so the audience watches one decision-maker.
                if (guard != first && guard.TryGetComponent<AutoChase>(out var chase)) chase.enabled = false;
                if (guard != first && guard.TryGetComponent<GuardScout>(out var scout)) scout.enabled = false;
            }
            var weapon = first.gameObject.AddComponent<EnemyWeapon>();
            weapon.warningLight = first.warningLight; weapon.tracer = first.tracer;
            weapon.range = first.range; weapon.damage = first.damage;
            weapon.warningSeconds = first.warningSeconds; weapon.shotInterval = first.shotInterval;
            var provider = first.gameObject.AddComponent<PerceptionObservation>();
            first.gameObject.AddComponent<UtilityEnemyController>().observations = provider;
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = first.gameObject;
            Debug.Log("Utility AI demo created. Press Play, then Begin Mission. F2 toggles scores and G toggles invulnerability.");
        }

        [MenuItem("Tactical Game/Utility AI/Check decision scenarios")]
        public static void CheckDecisions()
        {
            var input = new UtilityInputs { HealthFraction = 1f, Ammo = 6, Capacity = 6, Reserve = 24, Visible = true, Distance = 10f, Range = 19f };
            Expect(input, EnemyAction.Fire);
            input.Ammo = 0; Expect(input, EnemyAction.Reload);
            input.Reserve = 0; Expect(input, EnemyAction.Hold);
            input.Reserve = 24; input.Ammo = 3; input.Visible = false; Expect(input, EnemyAction.Reload);
            input.Visible = true; Expect(input, EnemyAction.Fire);
            input.HealthFraction = .25f; Expect(input, EnemyAction.Reload);
            var scores = UtilityScorer.Evaluate(input);
            Require(UtilityScorer.Select(scores, EnemyAction.Fire, true, .2f, .5f, .05f) == EnemyAction.Fire, "Commitment");
            Require(UtilityScorer.Select(scores, EnemyAction.Fire, true, 1f, .5f, .05f) == EnemyAction.Reload, "Switch after commitment");
            input.Visible = false;
            Require(UtilityScorer.Select(UtilityScorer.Evaluate(input), EnemyAction.Fire, true, 0f, .5f, .05f) == EnemyAction.Reload, "Invalid fire interrupted");
            input.Ammo = 6; input.Distance = 30; input.Visible = true; Expect(input, EnemyAction.Hold);
            input.CanMove = input.HasKnownTarget = input.RetreatReady = true;
            input.Role = Coordination.SquadRole.Attacker;
            Expect(input, EnemyAction.FallBack);
            input.RetreatReady = false;
            Expect(input, EnemyAction.Investigate);
            input.RetreatReady = true;
            Require(UtilityScorer.SelectChaseOnly(input) == EnemyAction.Investigate, "Chase Only ignores retreat and role scores");
            input.Distance = 8f;
            input.SettlingAfterDefense = true;
            Expect(input, EnemyAction.Fire);
            input.ShotBlocked = true;
            Expect(input, EnemyAction.Hold);
            input.SettlingAfterDefense = false;
            input.RetreatReady = false;
            Expect(input, EnemyAction.Investigate);
            CheckExperimentalSquadPlays();
            Debug.Log("PASS: Utility AI combat choices, health response, visibility, range, reserve, commitment, and interruption.");
        }

        static void CheckExperimentalSquadPlays()
        {
            var members = new[]
            {
                new MemberSnapshot { Id = "01", Alive = true, Health = 1f, Ammo = 6 },
                new MemberSnapshot { Id = "02", Alive = true, Health = 1f, HasFlankPoint = true, FlankPoint = Vector3.right * 5f }
            };
            var snapshot = new SquadSnapshot { Members = members };
            SquadPlay Pick() => SquadTactics.Select(SquadTactics.Evaluate(snapshot), SquadPlay.Freestyle, SquadTactics.MinHold);

            Require(Pick() == SquadPlay.Freestyle, "Unknown target falls back to freestyle");
            snapshot.KnowsTarget = true;
            Require(Pick() == SquadPlay.BoundingAdvance, "Known but unseen target allows a bounding advance");
            members[0].SeesPlayer = true;
            var scores = SquadTactics.Evaluate(snapshot);
            Require(Pick() == SquadPlay.PinAndFlank &&
                SquadTactics.Select(scores, SquadPlay.Freestyle, 1f) == SquadPlay.Freestyle,
                "Visible targets allow pin and flank after the current play's hold");
            var orders = SquadTactics.Assign(SquadPlay.PinAndFlank, snapshot, 0);
            Require(orders[0].Role == TacticRole.Pinner && orders[1].Role == TacticRole.Flanker && orders[1].Point == members[1].FlankPoint,
                "Pin and flank assigns the pinner and flank destination");
            members[0].Health = .2f;
            Require(Pick() == SquadPlay.CoveredRetreat, "An injured member triggers a covered retreat");
        }

        static void Expect(UtilityInputs input, EnemyAction expected)
        {
            Require(UtilityScorer.Select(UtilityScorer.Evaluate(input), EnemyAction.Hold, false, 0f, .5f, .05f) == expected, "Expected " + expected);
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
