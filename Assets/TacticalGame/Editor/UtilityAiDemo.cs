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
            var guards = UnityEngine.Object.FindObjectsOfType<BasicGuard>();
            Array.Sort(guards, (a, b) => string.CompareOrdinal(a.GetComponent<TargetDummy>().targetId, b.GetComponent<TargetDummy>().targetId));
            if (guards.Length == 0) throw new InvalidOperationException("Substation contains no guards.");
            var first = guards[0];
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
            // Movement actions: CanMove/KnowsTarget/HasCover are false above, so Chase and Retreat were unavailable.
            var move = new UtilityInputs { HealthFraction = 1f, Ammo = 6, Capacity = 6, Reserve = 24, Visible = true, Distance = 10f, Range = 19f,
                KnowsTarget = true, CanMove = true, HasCover = true, UnderFire = 1f };
            Expect(move, EnemyAction.Fire); // full health being shot: Fire 0.90 beats Retreat 0.30
            move.HealthFraction = .25f; move.Ammo = 3; Expect(move, EnemyAction.Retreat); // Retreat 0.81 > Reload 0.79 > Fire 0.71
            move.UnderFire = 0f; Expect(move, EnemyAction.Reload); // Retreat 0.51 loses to Reload 0.79
            move = new UtilityInputs { HealthFraction = 1f, Ammo = 6, Capacity = 6, Reserve = 24, Visible = false, Distance = 10f, Range = 19f,
                KnowsTarget = true, CanMove = true };
            Expect(move, EnemyAction.Chase); // Chase 0.80, hidden but last known position
            Require(!UtilityScorer.Evaluate(move)[(int)EnemyAction.Retreat].Available, "Retreat unavailable without cover");
            // Squad roles: individual level keeps emergencies, role adds bonuses and gates.
            var flank = new UtilityInputs { HealthFraction = 1f, Ammo = 6, Capacity = 6, Reserve = 24, Visible = false, Distance = 10f, Range = 19f,
                KnowsTarget = true, CanMove = true, Role = TacticRole.Flanker, HasOrderPoint = true };
            Expect(flank, EnemyAction.Hold); // gate closed: Hold 0.55, Chase blocked
            Require(!UtilityScorer.Evaluate(flank)[(int)EnemyAction.Chase].Available, "Flanker cannot chase");
            flank.PartnerCovering = true; Expect(flank, EnemyAction.Advance); // Advance 0.85
            flank.Ammo = 0; Expect(flank, EnemyAction.Reload); // empty magazine 1.00 beats Advance 0.85
            var pinner = new UtilityInputs { HealthFraction = 1f, Ammo = 6, Capacity = 6, Reserve = 24, Visible = true, Distance = 10f, Range = 19f, Role = TacticRole.Pinner };
            Expect(pinner, EnemyAction.Fire);
            pinner.Ammo = 0; Expect(pinner, EnemyAction.Reload);
            var retreater = new UtilityInputs { HealthFraction = .1f, Ammo = 0, Capacity = 6, Reserve = 24, Visible = true, Distance = 10f, Range = 19f,
                KnowsTarget = true, CanMove = true, UnderFire = 1f, Role = TacticRole.Retreater, HasOrderPoint = true };
            Expect(retreater, EnemyAction.Reload); // Retreat is capped at 0.99 so empty-magazine Reload still wins
            retreater.Ammo = 6; Expect(retreater, EnemyAction.Retreat);

            // Squad plays.
            MemberSnapshot M(string id, float hp, bool sees, bool flankPt) => new MemberSnapshot { Id = id, Alive = true, Health = hp, Ammo = 6, Capacity = 6,
                SeesPlayer = sees, HasFlankPoint = flankPt, FlankPoint = new Vector3(5, 0, 0), HasCoverPoint = true };
            SquadPlay Pick(SquadSnapshot s) => SquadTactics.Select(SquadTactics.Evaluate(s), SquadPlay.Freestyle, SquadTactics.MinHold);
            Require(Pick(new SquadSnapshot { Members = new[] { M("01", .2f, false, false), M("02", 1f, false, false) } }) == SquadPlay.CoveredRetreat, "Hurt member -> CoveredRetreat");
            Require(Pick(new SquadSnapshot { KnowsTarget = true, Members = new[] { M("01", 1f, true, false), M("02", 1f, false, true) } }) == SquadPlay.PinAndFlank, "Seen + flank point -> PinAndFlank");
            Require(Pick(new SquadSnapshot { KnowsTarget = true, Members = new[] { M("01", 1f, false, false), M("02", 1f, false, false) } }) == SquadPlay.BoundingAdvance, "Known, unseen -> BoundingAdvance");
            Require(Pick(new SquadSnapshot { Members = new[] { M("01", 1f, false, false), M("02", 1f, false, false) } }) == SquadPlay.Freestyle, "Nothing known -> Freestyle");
            var held = SquadTactics.Evaluate(new SquadSnapshot { KnowsTarget = true, Members = new[] { M("01", 1f, false, false), M("02", 1f, false, false) } });
            Require(SquadTactics.Select(held, SquadPlay.Freestyle, 1f) == SquadPlay.Freestyle, "Hysteresis keeps current play under 2 s");
            Require(SquadTactics.Select(held, SquadPlay.Freestyle, 2.5f) == SquadPlay.BoundingAdvance, "Play switches after 2 s");
            Debug.Log("PASS: Utility AI combat choices, health response, visibility, range, reserve, commitment, interruption, chase and retreat.");
        }

        static void Expect(UtilityInputs input, EnemyAction expected)
        {
            Require(UtilityScorer.Select(UtilityScorer.Evaluate(input), EnemyAction.Hold, false, 0f, .5f, .05f) == expected, "Expected " + expected);
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
