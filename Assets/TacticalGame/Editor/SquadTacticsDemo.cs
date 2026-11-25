using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticalGame.Editor
{
    public static class SquadTacticsDemo
    {
        const string DemoPath = "Assets/TacticalGame/Scenes/SquadTacticsDemo.unity";

        [MenuItem("Tactical Game/Utility AI/Open squad tactics demo")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(DemoPath))
            {
                EditorSceneManager.OpenScene(DemoPath);
                return;
            }
            var scene = EditorSceneManager.OpenScene(ProjectBuilder.SubstationPath);
            // Save a copy before touching any scene objects.
            if (!EditorSceneManager.SaveScene(scene, DemoPath)) throw new IOException("Could not create demo scene.");
            new GameObject("Squad Demo Director").AddComponent<SquadDemoDirector>(); // everything else is set up at runtime
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Squad tactics demo ready. Press Play and Begin Mission.");
        }

        [MenuItem("Tactical Game/Utility AI/Check squad strategies")]
        public static void CheckStrategies()
        {
            var members = new[] {
                new MemberSnapshot { Alive = true, Health = 1f, Ammo = 6, CanCover = true, SeesPlayer = true, HasClearShot = true },
                new MemberSnapshot { Alive = true, Health = 1f, Ammo = 5, CanCover = true, Flank = Vector3.right * 5f, LeftSearch = Vector3.left * 4f },
                new MemberSnapshot { Alive = true, Health = 1f, Ammo = 5, CanCover = true, RightSearch = Vector3.right * 4f, Step = Vector3.forward * 4f }
            };
            var squad = new SquadSnapshot { Members = members, KnowsTarget = true };
            void Expect(SquadPlay current, float age, SquadPlay expected)
            {
                var result = SquadTactics.Select(SquadTactics.Evaluate(squad), current, age);
                if (result != expected) throw new InvalidOperationException($"Expected {expected}, got {result}.");
            }

            Expect(SquadPlay.Freestyle, 0f, SquadPlay.Freestyle);
            Expect(SquadPlay.Freestyle, 2f, SquadPlay.PinAndFlank);
            members[0].CanCover = false;
            Expect(SquadPlay.PinAndFlank, 0f, SquadPlay.Contain);
            members[0].CanCover = true;
            members[0].SeesPlayer = members[0].HasClearShot = false;
            Expect(SquadPlay.Contain, 0f, SquadPlay.SplitSearch);
            var orders = SquadTactics.Assign(SquadPlay.SplitSearch, squad);
            if (orders[1].Role != TacticRole.Searcher || orders[2].Role != TacticRole.Searcher ||
                orders[1].Destination != members[1].LeftSearch || orders[2].Destination != members[2].RightSearch)
                throw new InvalidOperationException("Searchers need distinct destinations on both sides.");
            members[1].LeftSearch = null;
            Expect(SquadPlay.SplitSearch, 0f, SquadPlay.BoundingAdvance);
            members[1].Health = .30f;
            members[1].Cover = Vector3.back * 4f;
            Expect(SquadPlay.BoundingAdvance, 0f, SquadPlay.CoveredRetreat);
            members[1].Cover = null;
            Expect(SquadPlay.CoveredRetreat, 0f, SquadPlay.BoundingAdvance);
            squad.KnowsTarget = false;
            Expect(SquadPlay.BoundingAdvance, 0f, SquadPlay.Freestyle);
            members[0].Patrol = Vector3.forward * 6f;
            members[2].Patrol = Vector3.right * 6f;
            orders = SquadTactics.Assign(SquadPlay.Freestyle, squad);
            if (orders[0].Role != TacticRole.Scout || orders[1].Role != TacticRole.Overwatch ||
                orders[2].Role != TacticRole.Support || !orders[2].HasPoint)
                throw new InvalidOperationException("Patrol needs a scout, a watcher and mobile support.");
            squad.KnowsTarget = true;
            members[0].SeesPlayer = true;
            members[0].HasClearShot = false;
            members[1].Cover = Vector3.left * 3f;
            members[1].Peek = Vector3.left * 5f;
            members[1].Health = 1f;
            Expect(SquadPlay.PinAndFlank, 0f, SquadPlay.Contain);
            orders = SquadTactics.Assign(SquadPlay.Contain, squad);
            if (orders[1].Role != TacticRole.Shelter || orders[1].PeekPoint != members[1].Peek ||
                orders[2].Role != TacticRole.Support || !orders[2].HasPoint)
                throw new InvalidOperationException("A blocked shot needs a shelter with a firing angle and a moving teammate.");
            Debug.Log("PASS: six squad strategies, patrol roles, blocked shots, cover and firing positions, commitment and interruption.");
        }
    }
}
