using TacticalGame.Coordination;
using UnityEngine;

namespace TacticalGame
{
    [RequireComponent(typeof(EnemyWeapon), typeof(TargetDummy))]
    public sealed class UtilityEnemyController : MonoBehaviour
    {
        public EnemyObservationProvider observations;
        PathMover tacticalNavigation;
        [Min(.01f)] public float decisionInterval = .2f;
        [Min(0)] public float minimumHold = .5f, switchMargin = .05f;
        public bool showOverlay = true;
        public EnemyAction CurrentAction { get; private set; }
        public string LastSwitch { get; private set; } = "Waiting for mission";
        public ActionScore[] Scores { get; private set; }
        EnemyWeapon weapon;
        TargetDummy target;
        TacticalMovement movement;
        UtilityInputs inputs;
        float decisionLeft, held;
        bool initialized;

        void Awake()
        {
            weapon = GetComponent<EnemyWeapon>(); target = GetComponent<TargetDummy>();
            // Safety for manually attached controllers as well as the authored demo.
            var baseline = GetComponent<BasicGuard>();
            if (baseline != null) baseline.enabled = false;
        }

        public void SetNavigation(PathMover navigation)
        {
            if (tacticalNavigation == navigation) return;
            movement?.Stop();
            tacticalNavigation = navigation;
            movement = navigation != null ? new TacticalMovement(GetComponent<TargetDummy>(), navigation) : null;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2) && (tacticalNavigation == null || target.EnemyId == "01")) showOverlay = !showOverlay;
            var game = GameSession.Instance;
            if (game == null || game.State != RoundState.Playing || !target.IsAlive)
            {
                if (game == null || game.State != RoundState.Paused || !target.IsAlive) movement?.Stop();
                weapon.HideEffects();
                return;
            }
            if (observations == null) { movement?.Stop(); weapon.StopAiming(); LastSwitch = "Missing observation provider"; return; }
            float dt = Time.deltaTime;
            weapon.Tick(dt);
            var observation = observations.Observe(weapon.range);
            var squad = target.SquadCoordinator;
            var knowledge = observation.KnownTarget;
            if (!observation.Visible && squad != null && squad.TryGetSharedSighting(out var shared) &&
                (!knowledge.HasValue || shared.Report.ObservedAt > knowledge.Value.ObservedAt)) knowledge = shared.Report;
            held += dt; decisionLeft -= dt;
            inputs = new UtilityInputs { HealthFraction = target.health.Current / target.health.maximum,
                Ammo = weapon.Ammo, Capacity = weapon.magazineSize, Reserve = weapon.Reserve,
                Visible = observation.Visible, Distance = observation.Distance, Range = weapon.range,
                CanMove = movement != null, HasKnownTarget = knowledge.HasValue,
                AtTacticalPosition = movement != null && movement.AtMarker,
                Role = squad != null ? squad.GetRole(target.EnemyId) : SquadRole.Unassigned };
            // Availability changes bypass the normal decision interval and commitment.
            bool invalid = CurrentAction == EnemyAction.Fire && (!inputs.Visible || inputs.Distance > inputs.Range || inputs.Ammo <= 0);
            if (!weapon.IsReloading && (decisionLeft <= 0f || invalid || !initialized))
            {
                Scores = UtilityScorer.Evaluate(inputs);
                var next = UtilityScorer.Select(Scores, CurrentAction, initialized, held, minimumHold, switchMargin);
                if (!initialized || next != CurrentAction)
                {
                    LastSwitch = (initialized ? CurrentAction.ToString() : "Start") + " → " + next;
                    foreach (var score in Scores) if (score.Action == next) LastSwitch += ": " + score.Reason;
                    CurrentAction = next; held = 0f; initialized = true;
                }
                decisionLeft = decisionInterval;
            }
            movement?.Step(CurrentAction, knowledge);
            if (weapon.IsReloading) return;
            if (CurrentAction == EnemyAction.Reload) { weapon.TryReload(); return; }
            if (CurrentAction == EnemyAction.Fire)
            {
                Face(observation.AimPoint, dt);
                weapon.TryFire(observation.AimPoint, dt);
                return;
            }
            weapon.StopAiming();
            if (movement != null && !tacticalNavigation.IsMoving && knowledge.HasValue && CurrentAction != EnemyAction.FallBack)
                Face(knowledge.Value.ObservedPosition, dt);
        }

        void Face(Vector3 point, float deltaTime)
        {
            Vector3 facing = point - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > .01f)
                target.body.rotation = Quaternion.Slerp(target.body.rotation, Quaternion.LookRotation(facing), deltaTime * 6f);
        }

        void OnDisable()
        {
            movement?.Stop();
            if (weapon != null) { weapon.StopAiming(); weapon.HideEffects(); }
        }

        void OnGUI()
        {
            if (!showOverlay || weapon == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 470, 130, 450, 330), GUI.skin.box); // right side: HUD and squad panel use the left
            GUILayout.Label("UTILITY AI — Guard " + target.targetId + " [F2]");
            var perception = (observations as PerceptionObservation)?.Perception;
            GUILayout.Label(perception != null ? $"Perception (R4): {perception.State}{(perception.CanSeeTarget ? ", sees player" : "")}" : "Perception: not connected");
            GUILayout.Label($"Health {inputs.HealthFraction:P0} | Ammo {weapon.Ammo}/{weapon.magazineSize} + {weapon.Reserve}");
            GUILayout.Label($"Visible {inputs.Visible} | Distance {inputs.Distance:F1}m | Range {weapon.range:F1}m");
            GUILayout.Label(inputs.Role + " / " + CurrentAction + (weapon.IsReloading ? " (finishing reload)" : ""));
            if (!weapon.Active) GUILayout.Label("Combat suspended or enemy defeated");
            if (Scores != null) foreach (var score in Scores)
                GUILayout.Label($"{score.Action}: {(score.Available ? score.Score.ToString("F2") : "unavailable")} — {score.Reason}");
            GUILayout.Label("Last change: " + LastSwitch);
            GUILayout.EndArea();
        }
    }
}
