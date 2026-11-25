using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame
{
    // Presenter overlay for the squad level: play banner (top), radio panel (left), world tags and order lines.
    // IMGUI on a virtual 1440x900 canvas, drawn at screen size (no GUI.matrix scaling) so text stays sharp.
    public sealed class SquadTacticsOverlay : MonoBehaviour
    {
        public SquadTactician tactician;

        static readonly Color Red = C(0xff, 0x6b, 0x6b), Blue = C(0x6f, 0xd3, 0xff), Purple = C(0xc3, 0x9b, 0xff), Grey = C(0xbb, 0xbb, 0xbb),
            Green = C(0x6f, 0xe0, 0x8a), Yellow = C(0xff, 0xd1, 0x66), Orange = C(0xff, 0x9a, 0x6b), CoverRed = C(0xff, 0x7a, 0x6b),
            Muted = C(0x8d, 0x97, 0xa8), Dark = new Color(.03f, .05f, .08f, .88f);
        static Color C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        static readonly SquadPlay[] PlayOrder = { SquadPlay.PinAndFlank, SquadPlay.SplitSearch,
            SquadPlay.BoundingAdvance, SquadPlay.Contain, SquadPlay.CoveredRetreat, SquadPlay.Freestyle };
        sealed class RouteVisual
        {
            public Vector3? Destination;
            public List<Vector3> Preview;
            public float RefreshAt, OrderedAt, ArrivedAt = -10f;
            public bool Arrived;
        }

        readonly Dictionary<UtilityEnemyController, RouteVisual> routes = new Dictionary<UtilityEnemyController, RouteVisual>();
        float scale;
        GUIStyle label;
        SquadPlay shownPlay;
        float shownSince = -10f, contactPulse = -10f;
        int contactVersion;

        static Color PlayColor(SquadPlay p) => p == SquadPlay.PinAndFlank ? Orange : p == SquadPlay.BoundingAdvance ? Blue
            : p == SquadPlay.CoveredRetreat ? Purple : p == SquadPlay.SplitSearch ? Yellow : p == SquadPlay.Contain ? Green : Grey;
        static Color SignalColor(SquadSignal s) => s == SquadSignal.Covering || s == SquadSignal.Arrived ? Green
            : s == SquadSignal.Moving ? Blue : s == SquadSignal.CoverMe || s == SquadSignal.Blocked ? CoverRed
            : s == SquadSignal.Reloading ? Yellow : Muted;
        static string RoleText(TacticRole r) => r == TacticRole.None ? "Watch" : r.ToString();
        static Color RoleLineColor(TacticRole r) => r == TacticRole.Flanker ? Orange : r == TacticRole.Mover ? Blue
            : r == TacticRole.Searcher ? Yellow : r == TacticRole.Shelter ? Green : Purple;
        static string RoleMarker(TacticRole r) => r == TacticRole.Flanker ? "Side position" : r == TacticRole.Mover ? "Next position"
            : r == TacticRole.Searcher ? "Search point" : r == TacticRole.Scout ? "Scout sector" : r == TacticRole.Support ? "Support angle" : "Shelter";

        void OnGUI()
        {
            var game = GameSession.Instance;
            if (Event.current.type != EventType.Repaint || game == null || game.State != RoundState.Playing) return;
            if (tactician == null) return;
            scale = Mathf.Max(.35f, Mathf.Min(Screen.width / 1440f, Screen.height / 900f));
            if (label == null) label = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;
            int prevDepth = GUI.depth;
            GUI.color = Color.white;
            if (tactician.CurrentPlay != shownPlay) { shownPlay = tactician.CurrentPlay; shownSince = Time.time; }
            if (tactician.ContactVersion != contactVersion)
            {
                contactVersion = tactician.ContactVersion;
                contactPulse = Time.time;
            }
            GUI.depth = -10;
            var cam = SpectatorCamera.Resolve(game);
            if (cam != null) DrawWorld(cam);
            if (cam != null && cam == game.player.DemoMapCamera &&
                Project(cam, game.player.transform.position + Vector3.up * .3f, out var playerPoint))
            {
                Dot(playerPoint, Blue, 8f);
                Pill(playerPoint + Vector2.up * 20f * scale, "PLAYER", 13, Blue, Dark);
            }
            DrawBanner();
            DrawRadio();
            GUI.color = prevColor; GUI.matrix = prevMatrix; GUI.depth = prevDepth;
        }

        // ---- helpers (virtual coordinates in, screen pixels out) ----
        Rect R(float x, float y, float w, float h) => new Rect(x * scale, y * scale, w * scale, h * scale);

        void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }

        void Text(Rect r, string value, float size, Color c, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            label.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * scale));
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = align;
            label.normal.textColor = c;
            GUI.Label(r, value, label);
        }

        // Dark pill centred on a screen-pixel point; width estimated from the text length.
        void Pill(Vector2 center, string value, float size, Color text, Color bg, bool bold = true)
        {
            float w = value.Length * size * (bold ? .66f : .58f) + 18f, h = size + 10f;
            var r = new Rect(center.x - w * scale * .5f, center.y - h * scale * .5f, w * scale, h * scale);
            Fill(r, bg);
            Text(r, value, size, text, bold, TextAnchor.MiddleCenter);
        }

        bool Project(Camera cam, Vector3 world, out Vector2 px)
        {
            Vector3 p = cam.WorldToScreenPoint(world);
            px = new Vector2(p.x, Screen.height - p.y);
            return p.z > 0f;
        }

        // Straight (or dashed) segment in screen pixels; rotates GUI.matrix around a screen-pixel pivot and restores it.
        void Line(Vector2 a, Vector2 b, Color c, float thickness, bool dashed = false)
        {
            Vector2 d = b - a; float len = d.magnitude;
            if (len < 1f || len > 20000f) return;
            float th = Mathf.Max(1f, thickness * scale);
            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.color = c;
            if (!dashed) GUI.DrawTexture(new Rect(a.x, a.y - th * .5f, len, th), Texture2D.whiteTexture);
            else
            {
                float dash = 10f * scale, gap = 7f * scale; int n = 0;
                for (float t = 0f; t < len && n++ < 200; t += dash + gap)
                    GUI.DrawTexture(new Rect(a.x + t, a.y - th * .5f, Mathf.Min(dash, len - t), th), Texture2D.whiteTexture);
            }
            GUI.matrix = prev; GUI.color = Color.white;
        }

        // ---- top banner ----
        void DrawBanner()
        {
            var plan = tactician.CurrentPlay;
            float vw = Screen.width / scale, bx = (vw - 860f) * .5f, by = 10f;
            var tint = PlayColor(plan);
            Fill(R(bx, by, 860, 126), Dark);
            float flash = Mathf.Clamp01(1f - (Time.time - shownSince) / .7f);
            Fill(R(bx, by, 860, 4 + flash * 4), Color.Lerp(tint, Color.white, flash));
            var scores = tactician.PlayScores;
            Text(R(bx + 20, by + 9, 565, 36), SquadTactician.PlayName(plan), 29, tint, true);
            string contact = tactician.KnowsTarget ? (tactician.TargetVisible ? "Seen by " : tactician.HeardContact ? "Heard by " : "Last seen by ") + tactician.ContactSource : "Searching for a fresh report";
            Text(R(bx + 550, by + 9, 288, 36), contact, 17, Muted, false, TextAnchor.MiddleRight);
            for (int i = 0; i < PlayOrder.Length; i++)
            {
                var choice = PlayOrder[i];
                float x = bx + 20 + i * 139;
                bool ok = scores != null && scores[(int)choice].Available;
                Color color = ok ? PlayColor(choice) : Muted;
                Text(R(x, by + 49, 138, 22), SquadTactician.PlayName(choice), 14, color, choice == plan);
                Fill(R(x, by + 77, 112, 5), new Color(1f, 1f, 1f, .12f));
                if (ok) Fill(R(x, by + 77, 112 * scores[(int)choice].Score, 5), color);
            }
            if (scores != null) Text(R(bx + 20, by + 92, 820, 25), scores[(int)plan].Reason, 17, Color.white);
        }

        // ---- left radio panel ----
        void DrawRadio()
        {
            var members = tactician.Members;
            int rows = Mathf.Min(members.Length, 5), logRows = Mathf.Min(8, tactician.Log.Count);
            float h = 40 + rows * 26 + 34 + Mathf.Max(1, logRows) * 24 + 10;
            const float x = 20, y = 150, w = 363;
            Fill(R(x, y, w, h), Dark);
            Text(R(x + 14, y + 6, 300, 28), "SQUAD RADIO", 18, Color.white, true);
            for (int i = 0; i < rows; i++)
            {
                var m = members[i]; float ry = y + 40 + i * 26;
                if (m == null) continue;
                if (!m.Alive) { Text(R(x + 14, ry, w - 28, 24), m.Id + "  down", 16, Muted); continue; }
                var sig = m.Signal;
                Text(R(x + 14, ry, 40, 24), m.Id, 16, Color.white, true);
                Text(R(x + 58, ry, 110, 24), RoleText(m.Role), 16, Color.white);
                Text(R(x + 158, ry, 102, 24), $"{m.Health01:P0} · {m.Ammo}/{m.DemoCapacity}", 14, Muted);
                Fill(R(x + 261, ry + 6, 11, 11), SignalColor(sig));
                Text(R(x + 278, ry, 82, 24), sig.ToString(), 14, SignalColor(sig), true);
            }
            float ly = y + 40 + rows * 26 + 8;
            Fill(R(x + 14, ly, w - 28, 1), new Color(1, 1, 1, .15f));
            var log = tactician.Log;
            if (log.Count == 0) Text(R(x + 14, ly + 8, w - 28, 22), "no radio traffic yet", 14, Muted);
            for (int k = 0; k < logRows; k++) // newest first
            {
                string line = log[log.Count - 1 - k];
                bool play = Has(line, "PLAN —");
                Color c = play ? Color.white
                    : Has(line, "COVERME") || Has(line, "COVER ME") ? CoverRed
                    : Has(line, "COVERING") ? Green : Has(line, "MOVING") ? Blue : Has(line, "RELOADING") ? Yellow : Grey;
                Text(R(x + 14, ly + 8 + k * 24, w - 28, 22), line, 13, c, play);
            }
        }

        static bool Has(string s, string key) => s.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0;

        void DrawWorld(Camera cam)
        {
            foreach (var member in tactician.Members)
            {
                if (member == null || !member.Alive) continue;
                DrawRoute(cam, member);
                var partner = member.DemoCoverPartner;
                if (member.DemoCoverRequired && member.DemoDestination.HasValue && !member.DemoArrived && partner != null &&
                    Project(cam, member.transform.position + Vector3.up, out var from) &&
                    Project(cam, partner.transform.position + Vector3.up, out var to))
                {
                    bool ready = partner.Signal == SquadSignal.Covering;
                    Line(from, to, ready ? new Color(Green.r, Green.g, Green.b, .5f) : Muted, 1.5f, !ready);
                    if (ready) Dot(Vector2.Lerp(to, from, Mathf.Repeat(Time.time * 1.3f, 1f)), Green, 5f);
                }
            }

            if (tactician.KnowsTarget)
            {
                Vector3 point = tactician.SharedTarget + Vector3.up * .16f;
                Color color = tactician.TargetVisible ? Red : tactician.HeardContact ? Blue : Orange;
                if (!tactician.TargetVisible)
                    color.a = Mathf.Lerp(1f, .18f, Mathf.Clamp01((Time.time - tactician.ContactTime) / SquadTactician.MemorySeconds));
                if (Project(cam, point, out var target))
                {
                    Dot(target, color, 9f);
                    Pill(target + new Vector2(0, 19f * scale), tactician.TargetVisible ? "Spotted" :
                        tactician.HeardContact ? "Sound reported" : "Last seen here", 13, color, Dark);
                }
                float age = Time.time - contactPulse;
                if (age < 1f)
                {
                    color.a *= 1f - age;
                    Ring(cam, point, .4f + age * 3.5f, color);
                }
            }

            foreach (var member in tactician.Members)
            {
                if (member == null || !member.Alive || !Project(cam, member.transform.position + Vector3.up * 2.3f, out var p)) continue;
                var signal = member.Signal;
                string state = member.DemoCoverRequired && member.DemoDestination.HasValue && signal == SquadSignal.Idle
                    ? "Waiting for cover" : signal.ToString();
                Pill(p, member.Id + " · " + RoleText(member.Role), 14, Color.white, Dark);
                Pill(p + Vector2.down * 23f * scale, state, 12, SignalColor(signal), Dark);
            }
        }

        void DrawRoute(Camera cam, UtilityEnemyController member)
        {
            if (!routes.TryGetValue(member, out var visual))
                routes.Add(member, visual = new RouteVisual());
            var destination = member.DemoDestination;
            if (visual.Destination != destination)
            {
                visual.Destination = destination;
                visual.OrderedAt = Time.time;
                visual.RefreshAt = 0f;
                visual.Preview = null;
                visual.Arrived = false;
                visual.ArrivedAt = -10f;
            }
            if (!destination.HasValue) return;
            bool arrived = member.DemoArrived;
            if (arrived && !visual.Arrived) visual.ArrivedAt = Time.time;
            visual.Arrived = arrived;
            Color color = RoleLineColor(member.Role);
            Vector3 position = member.transform.position;
            Vector3 end = destination.Value;
            Vector3 lift = Vector3.up * .22f;
            if (!arrived && !member.DemoBlocked)
            {
                var navigator = member.DemoNavigation;
                var active = navigator.ActiveRoute;
                if (active == null && Time.time >= visual.RefreshAt)
                {
                    visual.RefreshAt = Time.time + .5f;
                    visual.Preview = AStarPathfinder.Instance?.FindPath(position, end);
                }
                DrawPath(cam, position, active ?? visual.Preview, active != null ? navigator.NextWaypointIndex : 0, lift, color, active != null);
            }
            if (Project(cam, end + lift, out var to))
            {
                Dot(to, color, arrived ? 9f : 7f);
                Pill(to + new Vector2(0, -17f * scale), member.DemoBlocked ? "Route blocked" : arrived ? "Arrived" :
                    member.DemoPeeking ? "Firing angle" : RoleMarker(member.Role),
                    12, color, Dark);
            }
            float age = Time.time - Mathf.Max(visual.OrderedAt, visual.ArrivedAt);
            if (age < .75f)
            {
                color.a *= 1f - age / .75f;
                Ring(cam, end + lift, .3f + age * 3f, color);
            }
        }

        void DrawPath(Camera cam, Vector3 from, IReadOnlyList<Vector3> points, int start, Vector3 lift, Color color, bool walking)
        {
            if (points == null) return;
            float travelled = 0f;
            float pulse = Mathf.Repeat(Time.time * 5f, 5f);
            for (int i = start; i < points.Count; i++)
            {
                Vector3 next = points[i];
                next.y = from.y;
                float length = Vector3.Distance(from, next);
                if (length <= .01f) continue;
                if (Project(cam, from + lift, out var a) && Project(cam, next + lift, out var b))
                {
                    Line(a, b, color, walking ? 3f : 1.6f, !walking);
                    if (walking)
                    {
                        float offset = Mathf.Repeat(pulse - travelled, 5f);
                        if (offset <= length) Dot(Vector2.Lerp(a, b, offset / length), Color.Lerp(color, Color.white, .7f), 5f);
                    }
                }
                travelled += length;
                from = next;
            }
        }

        void Dot(Vector2 point, Color color, float diameter)
        {
            float size = diameter * scale;
            Fill(new Rect(point.x - size * .5f, point.y - size * .5f, size, size), color);
        }

        void Ring(Camera cam, Vector3 center, float radius, Color color)
        {
            const int steps = 24;
            Vector3 from = center + Vector3.right * radius;
            for (int i = 1; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2f / steps;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (Project(cam, from, out var a) && Project(cam, next, out var b)) Line(a, b, color, 2f);
                from = next;
            }
        }
    }
}
