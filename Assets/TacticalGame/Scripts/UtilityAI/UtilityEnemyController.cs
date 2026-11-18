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
        EnemyWeapon weapon;
        TargetDummy target;
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

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) showOverlay = !showOverlay;
            if (!weapon.Active) { weapon.HideEffects(); return; }
            if (observations == null) { weapon.StopAiming(); LastSwitch = "Missing observation provider"; return; }
            float dt = Time.deltaTime;
            weapon.Tick(dt);
            var observation = observations.Observe(weapon.range);
            held += dt; decisionLeft -= dt;
            inputs = new UtilityInputs { HealthFraction = target.health.Current / target.health.maximum,
                Ammo = weapon.Ammo, Capacity = weapon.magazineSize, Reserve = weapon.Reserve,
                Visible = observation.Visible, Distance = observation.Distance, Range = weapon.range };
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
            if (weapon.IsReloading) return;
            if (CurrentAction == EnemyAction.Reload) { weapon.TryReload(); return; }
            if (CurrentAction != EnemyAction.Fire) { weapon.StopAiming(); return; }
            Vector3 facing = observation.AimPoint - transform.position; facing.y = 0f;
            if (facing.sqrMagnitude > .01f)
                target.body.rotation = Quaternion.Slerp(target.body.rotation, Quaternion.LookRotation(facing), dt * 6f);
            weapon.TryFire(observation.AimPoint, dt);
        }

        void OnDisable() { if (weapon != null) { weapon.StopAiming(); weapon.HideEffects(); } }

        void OnGUI()
        {
            if (!showOverlay || weapon == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 470, 130, 450, 330), GUI.skin.box); // right side: HUD and squad panel use the left
            GUILayout.Label("UTILITY AI — Guard " + target.targetId + " [F2]");
            var perception = (observations as PerceptionObservation)?.Perception;
            GUILayout.Label(perception != null ? $"Perception (R4): {perception.State}{(perception.CanSeeTarget ? ", sees player" : "")}" : "Perception: not connected");
            GUILayout.Label($"Health {inputs.HealthFraction:P0} | Ammo {weapon.Ammo}/{weapon.magazineSize} + {weapon.Reserve}");
            GUILayout.Label($"Visible {inputs.Visible} | Distance {inputs.Distance:F1}m | Range {weapon.range:F1}m");
            GUILayout.Label("Action: " + CurrentAction + (weapon.IsReloading ? " (finishing reload)" : ""));
            if (!weapon.Active) GUILayout.Label("Combat suspended or enemy defeated");
            if (Scores != null) foreach (var score in Scores)
                GUILayout.Label($"{score.Action}: {(score.Available ? score.Score.ToString("F2") : "unavailable")} — {score.Reason}");
            GUILayout.Label("Last change: " + LastSwitch);
            GUILayout.EndArea();
        }
    }
}
