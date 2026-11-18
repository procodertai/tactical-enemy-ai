using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TacticalGame.Coordination;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame.Editor
{
    public static class CoordinationInterfaceChecks
    {
        public const string SettingsPath = "Assets/TacticalGame/Settings/SquadInterfaceSettings.asset";
        public const string ReportPath = "Artifacts/Coordination/step-01-interface-checks.txt";

        [MenuItem("Tactical Game/Coordination/Step 01 - Prepare and validate interfaces")]
        public static void PrepareAndValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before validating coordination interfaces.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your open scenes before this check. It opens both maps without saving them.");

            var report = new StringBuilder();
            var settings = EnsureSettings();
            Require(settings.SharedSightingLifetimeSeconds > 0f, "Shared-sighting lifetime must be positive.");
            report.AppendLine("STEP 01 / Interface schema " + settings.ContractVersion);
            report.AppendLine("PASS settings asset: " + SettingsPath);
            report.AppendLine("Configured shared-sighting lifetime: " + settings.SharedSightingLifetimeSeconds + " seconds");

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { ProjectBuilder.SubstationPath, ProjectBuilder.DepotPath })
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ValidateScene(path, report);
                }
            }
            finally
            {
                bool hasActiveLoadedScene = false;
                foreach (var scene in originalSetup)
                    if (scene.isLoaded && scene.isActive) hasActiveLoadedScene = true;
                if (hasActiveLoadedScene) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            report.AppendLine("PASS enemy and marker adapters compile and resolve against the existing scene components.");
            report.AppendLine("This interface check does not execute tactical behavior. Use Run full coordination checks to exercise the implemented service.");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
        }

        static SquadInterfaceSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SquadInterfaceSettings>(SettingsPath);
            if (settings != null) return settings; // Preserve any values the user has edited.
            if (AssetDatabase.LoadMainAssetAtPath(SettingsPath) != null)
                throw new InvalidOperationException("The settings path already contains another asset type: " + SettingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            AssetDatabase.Refresh();
            settings = ScriptableObject.CreateInstance<SquadInterfaceSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        static void ValidateScene(string path, StringBuilder report)
        {
            var game = ProjectChecks.FindOne<GameSession>();
            Require(game != null && game.targets != null && game.targets.Length == 3, path + ": expected three enemy references.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var enemy in game.targets)
            {
                Require(enemy != null && enemy.health != null, path + ": enemy health reference is missing.");
                ISquadMember member = enemy;
                Require(!string.IsNullOrWhiteSpace(member.EnemyId), path + ": enemy ID is blank.");
                Require(ids.Add(member.EnemyId), path + ": duplicate enemy ID " + member.EnemyId);
                Require(member.Position == enemy.transform.position, path + ": member position does not match the scene.");
            }

            var markerIds = new HashSet<int>();
            foreach (var marker in ProjectChecks.FindAll<TacticalMarker>())
            {
                ITacticalDestination destination = marker;
                Require(destination.MarkerId > 0 && markerIds.Add(destination.MarkerId), path + ": invalid/duplicate marker ID " + destination.MarkerId);
                Require(Enum.IsDefined(typeof(MarkerKind), destination.Kind), path + ": marker kind is invalid.");
            }
            Require(markerIds.Count > 0, path + ": no tactical destinations are present.");
            var sortedIds = new List<string>(ids); sortedIds.Sort(StringComparer.Ordinal);
            report.AppendLine("PASS " + path + ": ISquadMember IDs [" + string.Join(", ", sortedIds) + "]; " + markerIds.Count + " unique ITacticalDestination markers.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
