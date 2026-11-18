using System;
using System.Collections.Generic;
using System.Linq;
using TacticalGame.Coordination;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame.Editor
{
    public static class SquadRegistrySetup
    {
        [MenuItem("Tactical Game/Coordination/Install coordination on both maps")]
        public static void InstallStarterScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before installing squad components.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your open scenes before installing the registry.");
            var settings = AssetDatabase.LoadAssetAtPath<SquadInterfaceSettings>(CoordinationInterfaceChecks.SettingsPath);
            ProjectChecks.Require(settings != null, "Create the Step 01 interface settings first.");
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var paths = new[] { ProjectBuilder.SubstationPath, ProjectBuilder.DepotPath };
            try
            {
                // Check both maps first so an invalid ID cannot leave only the first map upgraded.
                foreach (string path in paths)
                {
                    EditorSceneManager.OpenScene(path);
                    ValidateInputs(out _, out _);
                    ProjectChecks.Require(ProjectChecks.FindAll<SquadCoordinator>().Length <= 1, "More than one squad coordinator in " + path);
                }
                foreach (string path in paths)
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    var game = ValidateInputs(out var enemies, out var markers);
                    var squad = ProjectChecks.FindOne<SquadCoordinator>();
                    if (squad == null) squad = game.gameObject.AddComponent<SquadCoordinator>();
                    game.squadCoordinator = squad;
                    squad.Configure(enemies, markers, settings);
                    if (squad.GetComponent<SquadCoordinationDebug>() == null) squad.gameObject.AddComponent<SquadCoordinationDebug>();
                    foreach (var enemy in enemies) EditorUtility.SetDirty(enemy);
                    foreach (var enemy in enemies)
                        if (enemy.GetComponent<SquadMovementFeedback>() == null) enemy.gameObject.AddComponent<SquadMovementFeedback>();
                    foreach (var marker in markers) EditorUtility.SetDirty(marker);
                    EditorUtility.SetDirty(squad); EditorUtility.SetDirty(game);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("STEP 02 installed: " + path + " / " + enemies.Length + " enemies, " + markers.Length + " markers.");
                }
                foreach (string prefabPath in new[] { "Assets/TacticalGame/Prefabs/BasicGuard.prefab", "Assets/TacticalGame/Prefabs/StationaryTarget.prefab" })
                {
                    var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        if (prefab.GetComponent<SquadMovementFeedback>() == null) prefab.AddComponent<SquadMovementFeedback>();
                        PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }
            }
            finally
            {
                if (originalSetup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static GameSession ValidateInputs(out TargetDummy[] enemies, out TacticalMarker[] markers)
        {
            var game = ProjectChecks.FindOne<GameSession>();
            ProjectChecks.Require(game != null && game.targets != null, "Mission enemy references are missing.");
            enemies = game.targets;
            var enemyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var enemy in enemies)
                ProjectChecks.Require(enemy != null && !string.IsNullOrWhiteSpace(enemy.EnemyId) && enemyIds.Add(enemy.EnemyId), "Invalid or duplicate enemy ID.");
            markers = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TacticalMarker>(true)).OrderBy(marker => marker.id).ToArray();
            var markerIds = new HashSet<int>();
            foreach (var marker in markers)
                ProjectChecks.Require(marker.id > 0 && markerIds.Add(marker.id) && Enum.IsDefined(typeof(MarkerKind), marker.kind), "Invalid or duplicate marker ID.");
            return game;
        }
    }
}
