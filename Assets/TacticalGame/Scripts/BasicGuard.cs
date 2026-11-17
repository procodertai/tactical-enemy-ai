using UnityEngine;

namespace TacticalGame
{
    // Temporary combat baseline: stand, turn, warn, shoot on a fixed timer.
    // No navigation, tactical choices, viewing cone, memory, or squad messages.
    [RequireComponent(typeof(TargetDummy))]
    public sealed class BasicGuard : MonoBehaviour
    {
        [Min(1)] public float range = 19f;
        [Min(.1f)] public float warningSeconds = 1.2f;
        [Min(.2f)] public float shotInterval = 1.6f;
        [Min(1)] public float damage = 8f;
        public Light warningLight;
        public LineRenderer tracer;
        public bool CanShoot { get; private set; }
        public int ShotsFired { get; private set; }
        TargetDummy target;
        float shotAt;
        float flashUntil;

        void Awake()
        {
            target = GetComponent<TargetDummy>();
            tracer.enabled = false;
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (game == null) return;
            tracer.enabled = Time.time < flashUntil && game.State == RoundState.Playing;
            if (!target.health.IsAlive || !game.CanControl)
            {
                CanShoot = false;
                warningLight.enabled = false;
                return;
            }
            Vector3 origin = transform.position + Vector3.up * 1.45f;
            Vector3 direction = game.player.viewCamera.transform.position - origin;
            bool clear = direction.sqrMagnitude < range * range &&
                Physics.Raycast(origin, direction.normalized, out var hit, range, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<PlayerController>() == game.player;
            if (!clear)
            {
                CanShoot = false;
                warningLight.enabled = false;
                return;
            }
            Vector3 facing = direction; facing.y = 0f;
            if (facing.sqrMagnitude > .01f)
                target.body.rotation = Quaternion.Slerp(target.body.rotation, Quaternion.LookRotation(facing), Time.deltaTime * 6f);
            if (!CanShoot) { CanShoot = true; shotAt = Time.time + warningSeconds; }
            warningLight.enabled = true;
            warningLight.intensity = 1.3f + Mathf.Sin(Time.time * 14f) * .6f;
            if (Time.time < shotAt) return;
            shotAt = Time.time + shotInterval;
            ShotsFired++;
            // Recheck the actual first collision every shot. Walls and other guards stop bullets.
            if (Physics.Raycast(origin, direction.normalized, out var shot, range, ~0, QueryTriggerInteraction.Ignore) &&
                shot.collider.GetComponentInParent<PlayerController>() == game.player)
                game.player.health.TakeDamage(damage);
            tracer.SetPosition(0, origin + direction.normalized * .6f);
            tracer.SetPosition(1, origin + direction.normalized * direction.magnitude);
            flashUntil = Time.time + .07f;
            GameAudio.Instance.EnemyShot();
        }
    }
}
