using System;
using System.Collections.Generic;
using TacticalGame.Coordination;
using UnityEngine;

namespace TacticalGame
{
    // Basic guard perception: SIGHT (distance + angle + line of sight), HEARING (noise events)
    // and MEMORY (last known position). It only senses; it does not decide what the guard does.
    // Combat and movement read observations. Only confirmed visual sightings reach the squad.
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class GuardPerception : MonoBehaviour
    {
        public enum AwarenessState { Unaware, Suspicious, Alerted }

        [Header("Sight - central cone")]
        public float viewDistance = 28f;
        [Range(10f, 180f)] public float viewAngle = 90f;
        [Header("Sight - peripheral cone (shorter, wider)")]
        public float peripheralDistance = 9f;
        [Range(10f, 360f)] public float peripheralAngle = 160f;
        public float eyeHeight = 1.45f;

        [Header("Awareness (0..1)")]
        [Tooltip("Seconds to fully notice a fully visible player at close range")]
        public float secondsToNotice = 1f;
        public float suspiciousThreshold = 0.3f;
        public float decayPerSecond = 0.15f;

        [Header("Memory")]
        [Tooltip("After losing the player, awareness and last known position are held this long")]
        public float memoryDuration = 4f;

        [Header("Hearing")]
        public float hearingMultiplier = 1f;
        [Tooltip("Default awareness added by one noise")]
        public float noiseAwarenessBoost = 0.5f;

        [Header("Debug / demo")]
        public bool drawCones = true;
        public bool showOverlay = true;

        public float Awareness { get; private set; }
        public AwarenessState State { get; private set; } = AwarenessState.Unaware;
        public bool CanSeeTarget { get; private set; }
        public bool InPeripheralOnly { get; private set; }
        public bool HasLastKnown { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public float TimeSinceLastSeen => Time.time - lastSeenAt;
        public float LastStimulusAt => lastStimulusAt;
        public event Action<AwarenessState, AwarenessState> StateChanged;

        static readonly List<GuardPerception> All = new List<GuardPerception>();
        static Material lineMaterial;
        readonly RaycastHit[] hits = new RaycastHit[16];
        TargetDummy dummy;
        PlayerController player;
        CharacterController playerBody;
        float lastSeenAt = float.NegativeInfinity;
        float lastStimulusAt = float.NegativeInfinity;
        float visibleFraction;

        // Call from anywhere to make noise: GuardPerception.BroadcastNoise(pos, 25f);
        public static void BroadcastNoise(Vector3 position, float radius, float boost = -1f)
        {
            for (int i = 0; i < All.Count; i++) All[i].HearNoise(position, radius, boost);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable()
        {
            All.Remove(this);
            CanSeeTarget = false;
            InPeripheralOnly = false;
        }
        void Awake() { dummy = GetComponent<TargetDummy>(); }

        Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        Vector3 FlatForward()
        {
            Vector3 f = dummy != null && dummy.body != null ? dummy.body.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude < .0001f ? Vector3.forward : f.normalized;
        }

        bool Active()
        {
            var game = GameSession.Instance;
            if (game == null || game.State != RoundState.Playing) return false;
            if (dummy != null && dummy.health != null && !dummy.health.IsAlive) return false;
            if (player == null)
            {
                player = game.player;
                playerBody = player != null ? player.GetComponent<CharacterController>() : null;
            }
            return player != null;
        }

        void Update()
        {
            if (!Active()) { CanSeeTarget = false; InPeripheralOnly = false; return; }
            EvaluateSight();
            UpdateAwareness(Time.deltaTime);
            ShareSighting();
        }

        void ShareSighting()
        {
            if (!CanSeeTarget || State != AwarenessState.Alerted || dummy == null) return;
            var squad = dummy.SquadCoordinator;
            if (squad == null || !squad.CoordinationEnabled) return;
            if (squad.TryGetSharedSighting(out var previous) && previous.AgeSeconds < .25f) return;
            squad.ReportSighting(new SightingReport(dummy.EnemyId, LastKnownPosition, lastSeenAt));
        }

        // ---- SIGHT: distance -> angle -> raycast occlusion ----
        void EvaluateSight()
        {
            CanSeeTarget = false;
            InPeripheralOnly = false;
            visibleFraction = 0f;

            Vector3 eye = EyePosition;
            Vector3 toChest = ChestPoint() - eye;
            float dist = toChest.magnitude;
            if (dist < .01f) return;

            float angle = Vector3.Angle(FlatForward(), toChest);
            bool central = dist <= viewDistance && angle <= viewAngle * .5f;
            bool peripheral = !central && dist <= peripheralDistance && angle <= peripheralAngle * .5f;
            if (!central && !peripheral) return;

            // Test several body points so a partly hidden player is only partly visible.
            Vector3[] points = BodyPoints();
            int visible = 0;
            for (int i = 0; i < points.Length; i++)
                if (HasLineOfSight(eye, points[i])) visible++;
            if (visible == 0) return;

            visibleFraction = (float)visible / points.Length;
            CanSeeTarget = true;
            InPeripheralOnly = peripheral;
            LastKnownPosition = player.transform.position;
            HasLastKnown = true;
            lastSeenAt = lastStimulusAt = Time.time;
        }


        Vector3 ChestPoint()
        {
            if (playerBody != null) return playerBody.bounds.center;
            return player.transform.position + Vector3.up * .9f;
        }

        Vector3[] BodyPoints()
        {
            if (playerBody != null)
            {
                Bounds b = playerBody.bounds;
                return new[]
                {
                    new Vector3(b.center.x, b.min.y + .15f, b.center.z),
                    b.center,
                    new Vector3(b.center.x, b.max.y - .1f, b.center.z)
                };
            }
            Vector3 p = player.transform.position;
            return new[] { p + Vector3.up * .3f, p + Vector3.up * .9f, p + Vector3.up * 1.6f };
        }

        bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < .01f) return true;
            int n = Physics.RaycastNonAlloc(from, dir / dist, hits, dist, ~(1 << 2), QueryTriggerInteraction.Ignore);
            // A full nonalloc buffer can omit the closest hit.
            var sightHits = n < hits.Length ? hits : Physics.RaycastAll(from, dir / dist, dist, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (!ReferenceEquals(sightHits, hits)) n = sightHits.Length;
            float nearest = float.MaxValue;
            Collider first = null;
            for (int i = 0; i < n; i++)
            {
                Collider c = sightHits[i].collider;
                if (c.transform == transform || c.transform.IsChildOf(transform)) continue; // ignore self
                if (sightHits[i].distance < nearest) { nearest = sightHits[i].distance; first = c; }
            }
            // Visible when nothing blocks, or the first thing hit belongs to the player.
            return first == null || first.GetComponentInParent<PlayerController>() == player;
        }

        // ---- HEARING: event based, only gives a position to investigate ----
        public void HearNoise(Vector3 position, float radius, float boost = -1f)
        {
            var game = GameSession.Instance;
            if (game == null || game.State != RoundState.Playing) return;
            if (dummy != null && dummy.health != null && !dummy.health.IsAlive) return;
            if (Vector3.Distance(transform.position, position) > radius * hearingMultiplier) return;
            if (!CanSeeTarget)
            {
                LastKnownPosition = position;
                HasLastKnown = true;
            }
            lastStimulusAt = Time.time;
            Awareness = Mathf.Clamp01(Awareness + (boost >= 0f ? boost : noiseAwarenessBoost));
            RefreshState();
        }

        // ---- AWARENESS + MEMORY ----
        void UpdateAwareness(float dt)
        {
            if (CanSeeTarget)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                float closeness = Mathf.Clamp(1f - dist / Mathf.Max(viewDistance, .01f), .25f, 1f);
                float zone = InPeripheralOnly ? .4f : 1f;
                float rate = visibleFraction * closeness * zone / Mathf.Max(secondsToNotice, .05f);
                Awareness = Mathf.Clamp01(Awareness + rate * dt);
            }
            else if (Time.time - lastStimulusAt > memoryDuration)
            {
                Awareness = Mathf.Max(0f, Awareness - decayPerSecond * dt);
                if (Awareness <= 0f) HasLastKnown = false;
            }
            RefreshState();
        }

        void RefreshState()
        {
            AwarenessState next = Awareness >= 1f ? AwarenessState.Alerted
                : Awareness >= suspiciousThreshold ? AwarenessState.Suspicious
                : AwarenessState.Unaware;
            if (next == State) return;
            AwarenessState old = State;
            State = next;
            StateChanged?.Invoke(old, next);
        }

        // ---- VISUALISATION (works in the Game view, no Gizmos toggle needed) ----
        Color StateColor()
        {
            switch (State)
            {
                case AwarenessState.Alerted: return Color.red;
                case AwarenessState.Suspicious: return new Color(1f, .8f, 0f);
                default: return Color.green;
            }
        }

        static void ConeLines(Vector3 pos, Vector3 fwd, float range, float angle, Action<Vector3, Vector3> line)
        {
            float half = angle * .5f;
            Vector3 left = Quaternion.AngleAxis(-half, Vector3.up) * fwd;
            Vector3 right = Quaternion.AngleAxis(half, Vector3.up) * fwd;
            line(pos, pos + left * range);
            line(pos, pos + right * range);
            int steps = Mathf.Max(6, Mathf.CeilToInt(angle / 10f));
            Vector3 prev = pos + left * range;
            for (int i = 1; i <= steps; i++)
            {
                float a = Mathf.Lerp(-half, half, (float)i / steps);
                Vector3 p = pos + (Quaternion.AngleAxis(a, Vector3.up) * fwd) * range;
                line(prev, p);
                prev = p;
            }
        }

        void OnDrawGizmos()
        {
            if (!drawCones || Application.isPlaying) return;
            Vector3 pos = transform.position + Vector3.up * eyeHeight;
            Vector3 fwd = FlatForward();
            Gizmos.color = Color.green;
            ConeLines(pos, fwd, viewDistance, viewAngle, (a, b) => Gizmos.DrawLine(a, b));
            Gizmos.color = new Color(0f, 1f, 0f, .5f);
            ConeLines(pos, fwd, peripheralDistance, peripheralAngle, (a, b) => Gizmos.DrawLine(a, b));
        }

        void OnRenderObject()
        {
            if (!drawCones || !Application.isPlaying || !Active()) return;
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_ZWrite", 0);
                lineMaterial.SetInt("_Cull", 0);
            }
            lineMaterial.SetPass(0);
            Vector3 pos = EyePosition;
            Vector3 fwd = FlatForward();
            Color c = StateColor();

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            Action<Vector3, Vector3> line = (a, b) => { GL.Vertex(a); GL.Vertex(b); };
            GL.Color(c);
            ConeLines(pos, fwd, viewDistance, viewAngle, line);
            GL.Color(new Color(c.r, c.g, c.b, .6f));
            ConeLines(pos, fwd, peripheralDistance, peripheralAngle, line);
            if (CanSeeTarget)
            {
                GL.Color(Color.red);
                GL.Vertex(pos); GL.Vertex(ChestPoint());
            }
            if (HasLastKnown)
            {
                GL.Color(Color.cyan);
                GL.Vertex(LastKnownPosition); GL.Vertex(LastKnownPosition + Vector3.up * 2f);
                GL.Vertex(LastKnownPosition + Vector3.left * .5f); GL.Vertex(LastKnownPosition + Vector3.right * .5f);
                GL.Vertex(LastKnownPosition + Vector3.back * .5f); GL.Vertex(LastKnownPosition + Vector3.forward * .5f);
            }
            GL.End();
            GL.PopMatrix();
        }

        GUIStyle roleLabelStyle;

        void OnGUI()
        {
            if (!showOverlay || !Application.isPlaying || !Active()) return;
            var game = GameSession.Instance;
            Camera cam = game.Overview && game.overviewCamera != null ? game.overviewCamera : player.viewCamera;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * (eyeHeight + .9f));
            if (sp.z < 0f || sp.z > 60f) return;
            float x = sp.x - 40f, y = Screen.height - sp.y;
            var bar = new Rect(x, y, 80f, 8f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = StateColor();
            GUI.DrawTexture(new Rect(x + 1f, y + 1f, 78f * Awareness, 6f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x - 10f, y - 20f, 140f, 20f), State + (CanSeeTarget ? " (sees)" : ""));
            DrawRoleLabel(bar);
            GUI.color = old;
        }

        void DrawRoleLabel(Rect bar)
        {
            var squad = dummy != null ? dummy.SquadCoordinator : null;
            var role = squad != null ? squad.GetRole(dummy.EnemyId) : SquadRole.Unassigned;
            if (role == SquadRole.Unassigned) return;

            if (roleLabelStyle == null)
                roleLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            const float width = 68f, height = 18f, gap = 5f;
            float left = bar.xMax + gap;
            if (left + width > Screen.width) left = bar.xMin - gap - width;
            var label = new Rect(left, bar.center.y - height * .5f, width, height);
            GUI.color = new Color(0f, 0f, 0f, .7f);
            GUI.DrawTexture(label, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(label, role.ToString(), roleLabelStyle);
        }
    }
}
