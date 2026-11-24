using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame.Coordination
{
    // The manual report and reservations live in a separate coordinator for the overlay.
    [RequireComponent(typeof(SquadCoordinator)), DisallowMultipleComponent]
    public sealed class SquadCoordinationDebug : MonoBehaviour
    {
        [SerializeField] bool visible;
        SquadCoordinator squad;
        SquadCoordinator example;
        GUIStyle textStyle;
        GUIStyle markerStyle;
        public bool Visible { get => visible; set => visible = value; }
        public SquadCoordinator DisplayedSquad => example != null ? example : squad;

        void Awake() => squad = GetComponent<SquadCoordinator>();
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                visible = !visible;
                if (!visible) CloseExample();
            }
            if (!visible || squad == null) return;
            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (example == null) RunManualExample();
                else CloseExample();
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                CloseExample();
                if (GameSession.Instance == null || GameSession.Instance.AiMode == GuardAiMode.Squad)
                    squad.SetCoordinationEnabled(!squad.CoordinationEnabled);
            }
        }

        void CloseExample()
        {
            if (example == null) return;
            Destroy(example.gameObject);
            example = null;
        }

        sealed class ExampleMember : ISquadMember
        {
            readonly ISquadMember member;
            public ExampleMember(ISquadMember member) { this.member = member; }
            public string EnemyId => member.EnemyId;
            public bool IsAlive => member.IsAlive;
            public Vector3 Position => member.Position;
        }

        sealed class ExampleDestination : ITacticalDestination
        {
            readonly ITacticalDestination marker;
            public ExampleDestination(ITacticalDestination marker) { this.marker = marker; }
            public int MarkerId => marker.MarkerId;
            public MarkerKind Kind => marker.Kind;
            public Vector3 Position => marker.Position;
        }

        public bool RunManualExample()
        {
            if (!Application.isPlaying) return false;
            if (squad == null) squad = GetComponent<SquadCoordinator>();
            if (squad == null || squad.MemberCount == 0) return false;
            var candidates = new List<ITacticalDestination>();
            foreach (var destination in squad.Destinations)
                if (destination.Kind == MarkerKind.Cover) candidates.Add(destination);
            if (candidates.Count < squad.MemberCount) return false;
            CloseExample();
            var preview = new GameObject("Squad debug example");
            preview.transform.SetParent(transform, false);
            example = preview.AddComponent<SquadCoordinator>();
            example.Configure(null, null, squad.Settings);
            foreach (var member in squad.Members) example.RegisterEnemy(new ExampleMember(member));
            foreach (var marker in squad.Destinations) example.RegisterDestination(new ExampleDestination(marker));
            visible = true;
            example.ReportSighting(new SightingReport(example.Members[0].EnemyId, candidates[0].Position, Time.time));
            example.TryReserveDestination(example.Members[0].EnemyId, candidates[0].MarkerId);
            for (int i = 1; i < example.MemberCount; i++)
            {
                example.TryReserveDestination(example.Members[i].EnemyId, candidates[0].MarkerId);
                example.TryReserveDestination(example.Members[i].EnemyId, candidates[i].MarkerId);
            }
            return true;
        }

        void OnGUI()
        {
            var displayed = DisplayedSquad;
            if (!visible || displayed == null) return;
            float scale = Mathf.Max(.25f, Mathf.Min(Screen.width / 1440f, Screen.height / 900f));
            Matrix4x4 previous = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.depth = -10;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            var oldColor = GUI.color;
            DrawOverview(displayed, scale);
            float panelHeight = Mathf.Min(Screen.height / scale - 235, 640);
            GUI.color = new Color(.02f, .04f, .06f, .96f);
            GUI.DrawTexture(new Rect(30, 205, 550, panelHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float y = 221;
            Row(ref y, (example != null ? "SQUAD EXAMPLE / " : "SQUAD / ") + (displayed.CoordinationEnabled ? "ON" : "OFF") + " / round " + displayed.RoundVersion);
            Row(ref y, "F3 close  ·  F4 manual example  ·  F5 toggle", 15);
            if (example != null) Row(ref y, "Sample sightings and claims are isolated from this round.", 14, 30);
            foreach (var member in displayed.Members)
            {
                string claim = displayed.TryGetDestinationClaim(member.EnemyId, out var owned) ? "marker " + owned.MarkerId : "no claim";
                Row(ref y, member.EnemyId + " / " + displayed.GetRole(member.EnemyId) + LiveAction(member) + " / " + claim);
            }
            if (displayed.TryGetSharedSighting(out var sighting))
                Row(ref y, "Guard " + sighting.Report.ReporterId + " reported " + sighting.Report.ObservedPosition + " / age " + sighting.AgeSeconds.ToString("0.0") + "s", 14, 38);
            else Row(ref y, "Shared sighting: none / expired", 14);
            Row(ref y, "Claims " + displayed.Claims.Count + " / conflicts " + displayed.Metrics.ReservationConflicts + " / expired " + displayed.Metrics.ClaimsExpired, 14);
            Row(ref y, "Sightings accepted " + displayed.Metrics.SightingsAccepted + " / replans " + displayed.Metrics.ReplanRequests, 14);
            for (int i = displayed.DebugEvents.Count - 1; i >= 0 && y < 205 + panelHeight - 40; i--)
            {
                var item = displayed.DebugEvents[i];
                Row(ref y, item.Time.ToString("0.0") + "  " + item.Message, 13, 34);
            }
            GUI.color = oldColor; GUI.matrix = previous; GUI.depth = previousDepth;
        }

        void DrawOverview(SquadCoordinator displayed, float scale)
        {
            var game = GameSession.Instance;
            if (game == null || !game.Overview || game.squadCoordinator != squad || game.overviewCamera == null) return;
            var camera = game.overviewCamera;
            if (markerStyle == null)
                markerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            foreach (var claim in displayed.Claims)
                if (displayed.TryGetEnemy(claim.EnemyId, out var member) && displayed.TryGetDestination(claim.MarkerId, out var destination) &&
                    Project(camera, member.Position, scale, out var from) && Project(camera, destination.Position, scale, out var to))
                    DrawLine(from, to, new Color(.3f, 1f, .65f, .8f));
            foreach (var member in displayed.Members)
                if (member is TargetDummy guard && guard.TryGetComponent<PathMover>(out var mover))
                    DrawRoute(camera, scale, member.Position, mover);
            foreach (var destination in displayed.Destinations)
            {
                if (!Project(camera, destination.Position, scale, out var point)) continue;
                string prefix = destination.Kind == MarkerKind.Cover ? "C" : destination.Kind == MarkerKind.Flank ? "F" : "R";
                var color = destination.Kind == MarkerKind.Cover ? Color.cyan : destination.Kind == MarkerKind.Flank ? new Color(.85f, .6f, 1f) : Color.yellow;
                MarkerLabel(point, prefix + destination.MarkerId, color);
            }
            foreach (var member in displayed.Members)
                if (Project(camera, member.Position, scale, out var point))
                    MarkerLabel(point + Vector2.up * 25, "E" + member.EnemyId + LiveAction(member), Color.white);
            if (displayed.TryGetSharedSighting(out var sighting) && Project(camera, sighting.Report.ObservedPosition, scale, out var reported))
                MarkerLabel(reported + Vector2.down * 16, "Last seen · " + sighting.Report.ReporterId, new Color(1f, .75f, .3f));
        }

        static string LiveAction(ISquadMember member)
        {
            if (member is TargetDummy guard && guard.TryGetComponent<UtilityEnemyController>(out var utility) && utility.HasDecision)
                return " / " + utility.CurrentAction;
            return "";
        }

        static void DrawRoute(Camera camera, float scale, Vector3 from, PathMover mover)
        {
            var route = mover.ActiveRoute;
            if (route == null) return;
            for (int i = mover.NextWaypointIndex; i < route.Count; i++)
            {
                Vector3 to = route[i];
                if (Project(camera, from, scale, out var start) && Project(camera, to, scale, out var end))
                    DrawLine(start, end, new Color(.35f, .8f, 1f, .85f));
                from = to;
            }
        }

        static bool Project(Camera camera, Vector3 position, float scale, out Vector2 point)
        {
            Vector3 projected = camera.WorldToScreenPoint(position + Vector3.up * .3f);
            point = new Vector2(projected.x / scale, (Screen.height - projected.y) / scale);
            return projected.z > 0 && projected.x >= 0 && projected.x <= Screen.width && projected.y >= 0 && projected.y <= Screen.height;
        }

        void MarkerLabel(Vector2 point, string label, Color color)
        {
            float width = Mathf.Max(54, markerStyle.CalcSize(new GUIContent(label)).x + 16);
            var bounds = new Rect(point.x - width * .5f, point.y - 12, width, 24);
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
