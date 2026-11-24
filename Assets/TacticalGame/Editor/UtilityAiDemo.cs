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
            ProjectChecks.FindOne<GameSession>().useUtilitySquad = false;
            foreach (var guard in guards)
            {
                guard.enabled = false;
                // Only guard 01 moves, so the audience watches one decision-maker.
                if (guard != first && guard.TryGetComponent<AutoChase>(out var chase)) chase.enabled = false;
            }
            var weapon = first.gameObject.AddComponent<EnemyWeapon>();
            weapon.warningLight = first.warningLight; weapon.tracer = first.tracer;
            weapon.range = first.range; weapon.damage = first.damage;
            weapon.warningSeconds = first.warningSeconds; weapon.shotInterval = first.shotInterval;
            var provider = first.gameObject.AddComponent<PerceptionObservation>();
            first.gameObject.AddComponent<UtilityEnemyController>().observations = provider;
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = first.gameObject;
            Debug.Log("Utility AI demo created. Press Play, Begin Mission, and F2 to toggle scores.");
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
            Debug.Log("PASS: Utility AI combat choices, health response, visibility, range, reserve, commitment, and interruption.");
        }

        static void Expect(UtilityInputs input, EnemyAction expected)
        {
            Require(UtilityScorer.Select(UtilityScorer.Evaluate(input), EnemyAction.Hold, false, 0f, .5f, .05f) == expected, "Expected " + expected);
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
