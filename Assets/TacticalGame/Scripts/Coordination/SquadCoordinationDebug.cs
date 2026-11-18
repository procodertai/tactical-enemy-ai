using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame.Coordination
{
    // Off by default. Manual example injects API data only; it never moves agents or observes the player.
    [RequireComponent(typeof(SquadCoordinator)), DisallowMultipleComponent]
    public sealed class SquadCoordinationDebug : MonoBehaviour
    {
        [SerializeField] bool visible;
        SquadCoordinator squad;
        GUIStyle textStyle;
        GUIStyle markerStyle;
        public bool Visible { get => visible; set => visible = value; }

        void Awake() => squad = GetComponent<SquadCoordinator>();
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) visible = !visible;
            if (!visible || squad == null) return;
            if (Input.GetKeyDown(KeyCode.F4)) RunManualExample();
            if (Input.GetKeyDown(KeyCode.F5)) squad.SetCoordinationEnabled(!squad.CoordinationEnabled);
        }

        public bool RunManualExample()
        {
            if (squad == null) squad = GetComponent<SquadCoordinator>();
            if (squad == null || squad.MemberCount == 0) return false;
            var candidates = new List<ITacticalDestination>();
            foreach (var destination in squad.Destinations)
                if (destination.Kind == MarkerKind.Cover) candidates.Add(destination);
            if (candidates.Count < squad.MemberCount) return false;
            squad.SetCoordinationEnabled(true);
            squad.ResetCoordinationContext();
            // This is an explicit test observation at a chosen marker, NOT a live player position.
            squad.ReportSighting(new SightingReport(squad.Members[0].EnemyId, candidates[0].Position, Time.time));
            squad.TryReserveDestination(squad.Members[0].EnemyId, candidates[0].MarkerId);
            for (int i = 1; i < squad.MemberCount; i++)
            {
                squad.TryReserveDestination(squad.Members[i].EnemyId, candidates[0].MarkerId);
                squad.TryReserveDestination(squad.Members[i].EnemyId, candidates[i].MarkerId);
            }
            return true;
        }

        void OnGUI()
        {
            if (!visible || squad == null) return;
            float scale = Mathf.Max(.25f, Mathf.Min(Screen.width / 1440f, Screen.height / 900f));
            Matrix4x4 previous = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.depth = -10;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            var oldColor = GUI.color;
            DrawOverview(scale);
            float panelHeight = Mathf.Min(Screen.height / scale - 235, 640);
            GUI.color = new Color(.02f, .04f, .06f, .96f);
            GUI.DrawTexture(new Rect(30, 205, 550, panelHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float y = 221;
            Row(ref y, "SQUAD / " + (squad.CoordinationEnabled ? "ON" : "OFF") + " / round " + squad.RoundVersion);
            Row(ref y, "F3 close  ·  F4 manual example  ·  F5 toggle", 15);
            Row(ref y, "Example supplies reports/requests only. Guard tactics are not connected.", 14, 43);
            foreach (var member in squad.Members)
            {
                string claim = squad.TryGetDestinationClaim(member.EnemyId, out var owned) ? "marker " + owned.MarkerId : "no claim";
                Row(ref y, member.EnemyId + " / " + squad.GetRole(member.EnemyId) + " / " + claim);
            }
            if (squad.TryGetSharedSighting(out var sighting))
                Row(ref y, "Reported " + sighting.Report.ObservedPosition + " / age " + sighting.AgeSeconds.ToString("0.0") + "s", 14, 38);
            else Row(ref y, "Shared sighting: none / expired", 14);
            Row(ref y, "Claims " + squad.Claims.Count + " / conflicts " + squad.Metrics.ReservationConflicts + " / expired " + squad.Metrics.ClaimsExpired, 14);
            Row(ref y, "Sightings accepted " + squad.Metrics.SightingsAccepted + " / replans " + squad.Metrics.ReplanRequests, 14);
            for (int i = squad.DebugEvents.Count - 1; i >= 0 && y < 205 + panelHeight - 40; i--)
            {
                var item = squad.DebugEvents[i];
                Row(ref y, item.Time.ToString("0.0") + "  " + item.Message, 13, 34);
            }
            GUI.color = oldColor; GUI.matrix = previous; GUI.depth = previousDepth;
        }

        void DrawOverview(float scale)
        {
            var game = GameSession.Instance;
            if (game == null || !game.Overview || game.squadCoordinator != squad || game.overviewCamera == null) return;
            var camera = game.overviewCamera;
            if (markerStyle == null)
                markerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            foreach (var claim in squad.Claims)
                if (squad.TryGetEnemy(claim.EnemyId, out var member) && squad.TryGetDestination(claim.MarkerId, out var destination) &&
                    Project(camera, member.Position, scale, out var from) && Project(camera, destination.Position, scale, out var to))
                    DrawLine(from, to, new Color(.3f, 1f, .65f, .8f));
            foreach (var destination in squad.Destinations)
            {
                if (!Project(camera, destination.Position, scale, out var point)) continue;
                string prefix = destination.Kind == MarkerKind.Cover ? "C" : destination.Kind == MarkerKind.Flank ? "F" : "R";
                var color = destination.Kind == MarkerKind.Cover ? Color.cyan : destination.Kind == MarkerKind.Flank ? new Color(.85f, .6f, 1f) : Color.yellow;
                MarkerLabel(point, prefix + destination.MarkerId, color);
            }
            foreach (var member in squad.Members)
                if (Project(camera, member.Position, scale, out var point))
                    MarkerLabel(point + Vector2.up * 25, "E" + member.EnemyId, Color.white);
        }

        static bool Project(Camera camera, Vector3 position, float scale, out Vector2 point)
        {
            Vector3 projected = camera.WorldToScreenPoint(position + Vector3.up * .3f);
            point = new Vector2(projected.x / scale, (Screen.height - projected.y) / scale);
            return projected.z > 0 && projected.x >= 0 && projected.x <= Screen.width && projected.y >= 0 && projected.y <= Screen.height;
        }

        void MarkerLabel(Vector2 point, string label, Color color)
        {
            var bounds = new Rect(point.x - 27, point.y - 12, 54, 24);
            GUI.color = new Color(.015f, .025f, .04f, .9f);
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Label(bounds, label, markerStyle);
        }

        static void DrawLine(Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            Matrix4x4 previous = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - 1, delta.magnitude, 2), Texture2D.whiteTexture);
            GUI.matrix = previous;
        }

        void Row(ref float y, string value, int size = 16, float height = 28)
        {
            textStyle.fontSize = size;
            GUI.Label(new Rect(46, y, 516, height), value, textStyle);
            y += height;
        }

        void OnDrawGizmos()
        {
            if (!visible || !Application.isPlaying) return;
            if (squad == null) squad = GetComponent<SquadCoordinator>();
            foreach (var claim in squad.Claims)
                if (squad.TryGetEnemy(claim.EnemyId, out var member) && squad.TryGetDestination(claim.MarkerId, out var destination))
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(member.Position + Vector3.up, destination.Position + Vector3.up * .2f);
                    Gizmos.DrawWireSphere(destination.Position + Vector3.up * .2f, .4f);
                }
        }
    }
}
