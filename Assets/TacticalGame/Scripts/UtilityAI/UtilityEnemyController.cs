using UnityEngine;

namespace TacticalGame
{
    [RequireComponent(typeof(EnemyWeapon), typeof(TargetDummy))]
    public sealed class UtilityEnemyController : MonoBehaviour
    {
        public EnemyObservationProvider observations;
        [Min(.01f)] public float decisionInterval = .2f;
        [Min(0)] public float minimumHold = .5f, switchMargin = .05f;
        public bool showOverlay = true;
        public EnemyAction CurrentAction { get; private set; }
        public string LastSwitch { get; private set; } = "Waiting for mission";
        public ActionScore[] Scores { get; private set; }

        // Squad level: written by SquadTactician, read by the scorer as inputs.
        public TacticRole Role;
        public Vector3 OrderPoint;
        public bool HasOrderPoint, PartnerCovering;
        public Vector3 SharedTargetPoint;
        public bool HasSharedTarget;
        public string Id => target != null ? target.targetId : name;
        public bool Alive => target != null && target.health.IsAlive;
        public float Health01 => target != null ? target.health.Current / target.health.maximum : 0f;
        public bool SeesPlayer => inputs.Visible;
        public int Ammo => weapon != null ? weapon.Ammo : 0;
        public int Capacity => weapon != null ? weapon.magazineSize : 0;
        public bool TryGetLastKnown(out Vector3 point)
        {
            var p = (observations as PerceptionObservation)?.Perception;
            point = p != null && p.HasLastKnown ? p.LastKnownPosition : default;
            return p != null && p.HasLastKnown;
        }
        // Broadcast to teammates; the tactician derives gates (e.g. "flanker moves only while someone covers") from it.
        public SquadSignal Signal => weapon == null || !weapon.Active ? SquadSignal.Idle
            : weapon.IsReloading ? SquadSignal.Reloading
            : CurrentAction == EnemyAction.Fire || (CurrentAction == EnemyAction.Hold && Role == TacticRole.Overwatch) ? SquadSignal.Covering
            : CurrentAction == EnemyAction.Advance || CurrentAction == EnemyAction.Chase ? SquadSignal.Moving
            : CurrentAction == EnemyAction.Retreat ? SquadSignal.CoverMe : SquadSignal.Idle;
        EnemyWeapon weapon;
        TargetDummy target;
        PathMover mover;
        readonly System.Collections.Generic.List<string> log = new System.Collections.Generic.List<string>();
        UtilityInputs inputs;
        float decisionLeft, held;
        bool initialized;
        float lastHitTime = -99f, coverLeft, repathLeft;
        Vector3 orderGoal; bool hasOrderGoal; // last destination sent for an Advance/Retreat order
        TacticalMarker cover, coverGoal; // cover: best marker found; coverGoal: the one we were last sent to

        void Awake()
        {
            weapon = GetComponent<EnemyWeapon>(); target = GetComponent<TargetDummy>(); mover = GetComponent<PathMover>();
            // Safety for manually attached controllers as well as the authored demo.
            var baseline = GetComponent<BasicGuard>();
            if (baseline != null) baseline.enabled = false;
            // Movement is now a Utility AI action, so the temporary chase must not fight for the PathMover.
            var autoChase = GetComponent<AutoChase>();
            if (autoChase != null) autoChase.enabled = false;
        }

        void OnEnable() { if (target != null) target.health.Damaged += OnDamaged; }
        void OnDamaged(float amount) { lastHitTime = Time.time; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) showOverlay = !showOverlay;
            if (!weapon.Active) { weapon.HideEffects(); if (mover != null) mover.Stop(); return; }
            if (observations == null) { weapon.StopAiming(); LastSwitch = "Missing observation provider"; return; }
            float dt = Time.deltaTime;
            weapon.Tick(dt);
            var observation = observations.Observe(weapon.range);
            held += dt; decisionLeft -= dt;
            bool knows = !float.IsInfinity(observation.Distance);
            if (!knows && HasSharedTarget) // squad knowledge stands in for own memory
            {
                knows = true; observation.AimPoint = SharedTargetPoint;
                observation.Distance = Vector3.Distance(transform.position + Vector3.up * 1.45f, SharedTargetPoint);
            }
            coverLeft -= dt;
            if (!knows) cover = null;
            else if (coverLeft <= 0f) // FindCover runs A* per marker, so at most once per second
            {
                coverLeft = 1f;
                cover = Positioning.Instance != null ? Positioning.Instance.FindCover(transform.position, observation.AimPoint, out _) : null;
            }
            inputs = new UtilityInputs { HealthFraction = target.health.Current / target.health.maximum,
                Ammo = weapon.Ammo, Capacity = weapon.magazineSize, Reserve = weapon.Reserve,
                Visible = observation.Visible, Distance = observation.Distance, Range = weapon.range,
                KnowsTarget = knows, CanMove = mover != null, HasCover = cover != null,
                UnderFire = Mathf.Clamp01(1f - (Time.time - lastHitTime) / 3f),
                Role = Role, HasOrderPoint = HasOrderPoint, PartnerCovering = PartnerCovering };
            // Availability changes bypass the normal decision interval and commitment.
            bool invalid = CurrentAction == EnemyAction.Fire && (!inputs.Visible || inputs.Distance > inputs.Range || inputs.Ammo <= 0)
                || (CurrentAction == EnemyAction.Chase && inputs.Visible && inputs.Distance <= inputs.Range)
                || (CurrentAction == EnemyAction.Advance && (!inputs.PartnerCovering || !inputs.HasOrderPoint));
            if (!weapon.IsReloading && (decisionLeft <= 0f || invalid || !initialized))
            {
                Scores = UtilityScorer.Evaluate(inputs);
                var next = UtilityScorer.Select(Scores, CurrentAction, initialized, held, minimumHold, switchMargin);
                if (!initialized || next != CurrentAction)
                {
                    LastSwitch = !initialized ? "Start -> " + next
                        : $"{CurrentAction} -> {next}   ({next} {ScoreText(next)} vs {CurrentAction} {ScoreText(CurrentAction)})";
                    CurrentAction = next; held = 0f; initialized = true;
                    log.Add($"{Time.timeSinceLevelLoad:F1}s  {LastSwitch}");
                    if (log.Count > 3) log.RemoveAt(0);
                }
                decisionLeft = decisionInterval;
            }
            bool moving = !weapon.IsReloading && (CurrentAction == EnemyAction.Chase || CurrentAction == EnemyAction.Retreat || CurrentAction == EnemyAction.Advance);
            if (!moving) { repathLeft = 0f; coverGoal = null; hasOrderGoal = false; if (mover != null) mover.Stop(); }
            if (weapon.IsReloading) return;
            if (moving)
            {
                weapon.StopAiming();
                if (CurrentAction == EnemyAction.Chase)
                {
                    repathLeft -= dt;
                    if (repathLeft <= 0f) { repathLeft = .5f; mover.MoveTo(observation.AimPoint); }
                }
                else if (CurrentAction == EnemyAction.Advance || (Role == TacticRole.Retreater && HasOrderPoint)) MoveToOrder(dt);
                else if (cover != null && cover != coverGoal) { coverGoal = cover; mover.MoveTo(cover.Position); }
                return;
            }
            if (CurrentAction == EnemyAction.Reload) { weapon.TryReload(); return; }
            if (CurrentAction != EnemyAction.Fire) { weapon.StopAiming(); return; }
            Vector3 facing = observation.AimPoint - transform.position; facing.y = 0f;
            if (facing.sqrMagnitude > .01f)
                target.body.rotation = Quaternion.Slerp(target.body.rotation, Quaternion.LookRotation(facing), dt * 6f);
            weapon.TryFire(observation.AimPoint, dt);
        }

        // Go to the squad's point: re-send when it moved > 1 m, or every 0.5 s while idle; stop within 1 m.
        void MoveToOrder(float dt)
        {
            Vector3 flat = OrderPoint - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude < 1f) { mover.Stop(); return; }
            repathLeft -= dt;
            bool changed = !hasOrderGoal || (OrderPoint - orderGoal).sqrMagnitude > 1f;
            if (changed || (!mover.IsMoving && repathLeft <= 0f)) { repathLeft = .5f; orderGoal = OrderPoint; hasOrderGoal = true; mover.MoveTo(OrderPoint); }
        }

        void OnDisable()
        {
            if (target != null) target.health.Damaged -= OnDamaged;
            if (weapon != null) { weapon.StopAiming(); weapon.HideEffects(); }
        }

        string ScoreText(EnemyAction action)
        {
            foreach (var score in Scores) if (score.Action == action) return score.Available ? score.Score.ToString("F2") : "unavailable";
            return "-";
        }

        // What the weapon is really doing, read back from EnemyWeapon, so it can be compared with the decision.
        string DoingText() => !weapon.Active ? "combat paused or guard down"
            : weapon.IsReloading ? "reloading magazine..." : CurrentAction == EnemyAction.Fire ? $"aiming + shooting (shots fired {weapon.ShotsFired})"
            : CurrentAction == EnemyAction.Reload ? "starting reload" : CurrentAction == EnemyAction.Chase ? "chasing along A* path"
            : CurrentAction == EnemyAction.Advance ? "advancing to squad point (teammate covering)"
            : CurrentAction == EnemyAction.Retreat ? $"running to cover marker {(cover != null ? cover.id.ToString() : "-")}" : "not shooting, holding";

        static string Colored(EnemyAction action) => $"<color={(action == EnemyAction.Fire ? "#ff6b6b" : action == EnemyAction.Reload ? "#ffd166" : action == EnemyAction.Chase ? "#6fd3ff" : action == EnemyAction.Retreat ? "#c39bff" : action == EnemyAction.Advance ? "#7cf0c8" : "#bbbbbb")}>{action.ToString().ToUpper()}</color>";

        GUIStyle text, small, panel;
        const string Muted = "#8d97a8";

        // Overlay is laid out on a virtual 1440x900 canvas but drawn at screen size (no GUI.matrix), so glyphs stay sharp.
        float scale, styledScale;
        Texture2D background;
        int Px(float v) => Mathf.RoundToInt(v * scale);
        int Font(float size) => Mathf.Max(1, Px(size));
        Rect Px(float x, float y, float w, float h) => new Rect(x * scale, y * scale, w * scale, h * scale);

        void Styles()
        {
            if (text != null && Mathf.Approximately(styledScale, scale)) return;
            styledScale = scale;
            if (background == null)
            {
                background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(.04f, .06f, .09f, .94f)); // opaque enough that the pause menu does not bleed through
                background.Apply();
            }
            text = new GUIStyle(GUI.skin.label) { fontSize = Font(14), richText = true, wordWrap = false,
                padding = new RectOffset(0, 0, Px(1), Px(1)), margin = new RectOffset(0, 0, 0, 0) };
            small = new GUIStyle(text) { fontSize = Font(12) };
            panel = new GUIStyle { padding = new RectOffset(Px(14), Px(14), Px(10), Px(12)) };
            panel.normal.background = background;
        }

        void Line(string value, GUIStyle style = null) => GUILayout.Label(value, style ?? text);

        void Section(string number, string title, string color, string note)
        {
            var rule = GUILayoutUtility.GetRect(1, Px(11));
            GUI.color = new Color(1, 1, 1, .12f); GUI.DrawTexture(new Rect(rule.x, rule.y + Px(5), rule.width, Mathf.Max(1, Px(1))), Texture2D.whiteTexture); GUI.color = Color.white;
            Line($"<b><color={color}>{number}  {title}</color></b>   <size={Font(12)}><color={Muted}>{note}</color></size>");
        }

        // Same virtual 1440x900 canvas as GameHud, so the overlay keeps its proportions on any Game view size.
        void OnGUI()
        {
            if (!showOverlay || weapon == null) return;
            scale = Mathf.Max(.25f, Mathf.Min(Screen.width / 1440f, Screen.height / 900f));
            Styles();
            GUI.depth = -20; // draw above the pause screen, so Esc freezes a decision for explaining
            if (weapon.Active) DrawHeadLabel(); // hidden while paused; the side panel is the one to explain from
            DrawPanel(Screen.width / scale);
        }

        // Shows the pipeline in order: 1 inputs -> 2 scores -> 3 selection -> 4 action.
        void DrawPanel(float width)
        {
            GUILayout.BeginArea(Px(width - 460, 160, 440, 700)); // right side: HUD and squad panel use the left
            GUILayout.BeginVertical(panel);
            Line($"<b>UTILITY AI  ·  Guard {target.targetId}</b>   <size={Font(12)}><color={Muted}>F2 hide</color></size>");

            var perception = (observations as PerceptionObservation)?.Perception;
            Section("1", "INPUT", "#7fb2ff", "perception (R4) + own state");
            Line($"Sees player  {(inputs.Visible ? "<color=#6fe08a>YES</color>" : "<color=#ff7a6b>NO</color>")}"
                + (perception != null ? $"  <color={Muted}>({perception.State})</color>" : $"  <color={Muted}>(perception not connected)</color>")
                + $"     Distance  {inputs.Distance:F1} / {inputs.Range:F0} m");
            Line($"Knows player pos  {(inputs.KnowsTarget ? "YES" : "NO")}     Cover marker  {(cover != null ? cover.id.ToString() : "none")}     Under fire  {inputs.UnderFire:F2}");
            Line($"Squad role  {Role}  <color={Muted}>order point {(HasOrderPoint ? "yes" : "no")} · teammate covering {(PartnerCovering ? "yes" : "no")} · signal {Signal}</color>");
            Line($"Health  {inputs.HealthFraction:P0}     Ammo  {weapon.Ammo}/{weapon.magazineSize}  <color={Muted}>(+{weapon.Reserve} reserve)</color>");

            Section("2", "SCORE", "#c39bff", "each action has its own formula");
            if (Scores != null) foreach (var score in Scores)
            {
                bool chosen = score.Action == CurrentAction;
                GUILayout.BeginHorizontal();
                GUILayout.Label(chosen ? $"<b>{score.Action}</b>" : score.Action.ToString(), text, GUILayout.Width(Px(70)));
                var bar = GUILayoutUtility.GetRect(Px(250), Px(18), GUILayout.Width(Px(250)));
                bar = new Rect(bar.x, bar.y + Px(5), bar.width, Px(9));
                GUI.color = new Color(1, 1, 1, .1f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                if (score.Available)
                {
                    GUI.color = chosen ? new Color(.35f, .88f, .5f) : new Color(.45f, .62f, 1f);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(score.Score), bar.height), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                GUILayout.Label(score.Available ? $"<b>{score.Score:F2}</b>" : $"<color={Muted}>blocked</color>", text);
                GUILayout.EndHorizontal();
                Line($"<color={Muted}>{(score.Available ? "= " : "")}{score.Reason}</color>", small);
                GUILayout.Space(Px(1));
            }

            Section("3", "SELECT", "#6fe08a", "highest available score wins");
            Line($"Chosen  <b>{Colored(CurrentAction)}</b>     <color={Muted}>kept {held:F1} s</color>");
            Line($"<color={Muted}>min hold {minimumHold} s · a rival must lead by {switchMargin}</color>", small);

            Section("4", "ACTION", "#ff9a6b", "what the weapon is doing");
            Line(DoingText());

            Section("", "LAST CHANGE", "#ffd166", "");
            Line($"<color=#ffd166>{LastSwitch}</color>", small);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // Compact live view that follows the guard: sees -> thinks -> does, plus the last decision changes.
        void DrawHeadLabel()
        {
            var game = GameSession.Instance;
            if (game == null || game.player == null || Scores == null) return;
            var camera = SpectatorCamera.Resolve(game);
            if (camera == null) return;
            Vector3 point = camera.WorldToScreenPoint(transform.position + Vector3.up * 2.4f);
            if (point.z <= 0f) return; // guard is behind the camera
            float rival = -1f; EnemyAction rivalAction = CurrentAction;
            foreach (var score in Scores)
                if (score.Available && score.Action != CurrentAction && score.Score > rival) { rival = score.Score; rivalAction = score.Action; }

            int rows = mover != null ? 4 : 3, logRows = Mathf.Min(2, log.Count);
            float width = 340, height = 14 + rows * 19 + logRows * 16;
            float x = Mathf.Clamp(point.x / scale - width / 2, 8, Screen.width / scale - width - 8);
            float y = Mathf.Max(8, (Screen.height - point.y) / scale - height);
            GUI.Box(Px(x, y, width, height), GUIContent.none, panel);
            float row = y + 7;
            void Row(string value, GUIStyle style, float step) { GUI.Label(Px(x + 10, row, width - 20, step), value, style); row += step; }
            Row($"<color=#7fb2ff>SEES</color>  player {(inputs.Visible ? "<color=#6fe08a>YES</color>" : "<color=#ff7a6b>NO</color>")} · {inputs.Distance:F0} m · hp {inputs.HealthFraction:P0} · ammo {weapon.Ammo}/{weapon.magazineSize} · hit {inputs.UnderFire:F1}", small, 19);
            Row($"<color=#c39bff>THINKS</color>  <b>{Colored(CurrentAction)}</b> {ScoreText(CurrentAction)}"
                + (rival >= 0f ? $"  >  {rivalAction} {rival:F2}" : "  (only choice)"), text, 19);
            Row($"<color=#ff9a6b>DOES</color>  {DoingText()}", small, 19);
            if (mover != null) Row($"<color={Muted}>MOVES  {(mover.IsMoving ? "walking A* path (decided by Utility AI)" : "standing")}</color>", small, 19);
            for (int i = log.Count - logRows; i < log.Count; i++) Row($"<color=#ffd166>{log[i]}</color>", small, 16);
        }
    }
}
