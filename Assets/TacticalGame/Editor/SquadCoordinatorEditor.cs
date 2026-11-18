using TacticalGame.Coordination;
using UnityEditor;
using UnityEngine;

namespace TacticalGame.Editor
{
    [CustomEditor(typeof(SquadCoordinator))]
    public sealed class SquadCoordinatorEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Coordination service. Perception supplies sightings; Utility AI consumes roles/claims; navigation supplies movement and obstruction reports. F3 shows in-game diagnostics.", MessageType.Info);
            var squad = (SquadCoordinator)target;
            bool enabled = EditorGUILayout.Toggle("Coordination enabled", squad.CoordinationEnabled);
            if (enabled != squad.CoordinationEnabled)
            {
                Undo.RecordObject(squad, "Toggle squad coordination");
                squad.SetCoordinationEnabled(enabled); EditorUtility.SetDirty(squad);
            }
            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Enter Play mode to see the live roster.");
                return;
            }
            EditorGUILayout.LabelField("Registered enemies: " + squad.MemberCount, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                foreach (var member in squad.Members)
                    EditorGUILayout.ObjectField(member.EnemyId + " / " + squad.GetRole(member.EnemyId), member as Object, typeof(Object), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Registered markers: " + squad.DestinationCount, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                foreach (var destination in squad.Destinations)
                    EditorGUILayout.ObjectField(destination.MarkerId + " / " + destination.Kind, destination as Object, typeof(Object), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active claims: " + squad.Claims.Count, EditorStyles.boldLabel);
            foreach (var claim in squad.Claims)
                EditorGUILayout.LabelField(claim.EnemyId + " → " + claim.MarkerId + " / token " + claim.ClaimId + " / " + Mathf.Max(0, claim.ExpiresAt - Time.time).ToString("0.0") + "s");
            if (squad.TryGetSharedSighting(out var sighting))
                EditorGUILayout.LabelField("Shared report", sighting.Report.ReporterId + " / " + sighting.Report.ObservedPosition + " / " + sighting.AgeSeconds.ToString("0.0") + "s");
            else EditorGUILayout.LabelField("Shared report", "None / expired");
            EditorGUILayout.LabelField("Reservation conflicts", squad.Metrics.ReservationConflicts.ToString());
            EditorGUILayout.LabelField("Replan requests", squad.Metrics.ReplanRequests.ToString());
            EditorGUILayout.LabelField("Rejected stale move reports", squad.Metrics.StaleMovementReports.ToString());
            if (GUILayout.Button("Run manual coordination example")) squad.GetComponent<SquadCoordinationDebug>()?.RunManualExample();
            if (GUILayout.Button("Clear tactical context (keep roster)")) squad.ResetCoordinationContext();
            if (GUILayout.Button("Reset metrics")) squad.ResetMetrics();
            if (GUILayout.Button("Rebuild runtime registry")) squad.RebuildRegistry();
            if (GUILayout.Button("Clear runtime registry")) squad.ResetRound();
        }
    }
}
