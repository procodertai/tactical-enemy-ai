using UnityEngine;

namespace TacticalGame
{
    [RequireComponent(typeof(Health))]
    public sealed class EnemyWeapon : MonoBehaviour
    {
        [Min(1)] public int magazineSize = 6;
        [Min(0)] public int startingReserve = 24;
        [Min(.1f)] public float reloadSeconds = 1.7f;
        public float range = 19f, damage = 8f, warningSeconds = 1.2f, shotInterval = 1.6f;
        public Light warningLight;
        public LineRenderer tracer;
        public int Ammo { get; private set; }
        public int Reserve { get; private set; }
        public int ShotsFired { get; private set; }
        public bool IsReloading { get; private set; }
        public bool Active => GameSession.Instance != null && GameSession.Instance.CanControl && health.IsAlive;
        Health health;
        float reloadLeft, shotLeft, warningLeft, flashLeft;
        bool aiming;

        void Awake()
        {
            health = GetComponent<Health>();
            Ammo = magazineSize; Reserve = startingReserve;
            StopAiming();
        }

        // Driven by the controller so pause/overview freeze every combat timer.
        public void Tick(float dt)
        {
            if (!Active) { HideEffects(); return; }
            shotLeft = Mathf.Max(0f, shotLeft - dt);
            flashLeft = Mathf.Max(0f, flashLeft - dt);
            if (tracer != null) tracer.enabled = flashLeft > 0f;
            if (!IsReloading) return;
            reloadLeft -= dt;
            if (reloadLeft > 0f) return;
            int loaded = Mathf.Min(magazineSize - Ammo, Reserve);
            Ammo += loaded; Reserve -= loaded; IsReloading = false;
        }

        public bool TryReload()
        {
            if (!Active || IsReloading || Ammo >= magazineSize || Reserve <= 0) return false;
            StopAiming(); IsReloading = true; reloadLeft = reloadSeconds;
            return true;
        }

        public bool TryFire(Vector3 point, float dt)
        {
            if (!Active || IsReloading || Ammo <= 0) return false;
            Vector3 origin = transform.position + Vector3.up * 1.45f;
            Vector3 direction = point - origin;
            // Never shoot through a wall, even if an observation is stale.
            if (direction.sqrMagnitude > range * range ||
                !Physics.Raycast(origin, direction.normalized, out var hit, range, ~0, QueryTriggerInteraction.Ignore) ||
                hit.collider.GetComponentInParent<PlayerController>() != GameSession.Instance.player)
            { StopAiming(); return false; }
            if (!aiming) { aiming = true; warningLeft = warningSeconds; }
            if (warningLight != null) { warningLight.enabled = true; warningLight.intensity = 1.3f + Mathf.Sin(Time.time * 14f) * .6f; }
            warningLeft -= dt;
            if (warningLeft > 0f || shotLeft > 0f) return false;
            Ammo--; ShotsFired++; shotLeft = shotInterval;
            GameSession.Instance.ReportPlayerHitFrom(origin);
            GameSession.Instance.player.health.TakeDamage(damage);
            if (tracer != null) { tracer.SetPosition(0, origin + direction.normalized * .6f); tracer.SetPosition(1, hit.point); tracer.enabled = true; }
            flashLeft = .07f;
            if (GameAudio.Instance != null) GameAudio.Instance.EnemyShot();
            return true;
        }

        public void StopAiming() { aiming = false; if (warningLight != null) warningLight.enabled = false; }
        public void HideEffects() { if (warningLight != null) warningLight.enabled = false; if (tracer != null) tracer.enabled = false; }
        void OnDisable() { StopAiming(); HideEffects(); }
    }
}
