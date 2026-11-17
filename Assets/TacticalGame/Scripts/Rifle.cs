using UnityEngine;

namespace TacticalGame
{
    public sealed class Rifle : MonoBehaviour
    {
        [Header("Weapon rules")]
        public int magazineSize = 24;
        public int startingReserve = 120;
        public float damage = 25f;
        public float roundsPerSecond = 7f;
        public float reloadSeconds = 1.7f;
        public float range = 90f;
        public Camera viewCamera;
        public Transform weaponModel;
        public Light muzzleFlash;
        public LineRenderer tracer;
        public int Ammo { get; private set; }
        public int Reserve { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsAiming { get; private set; }
        public float ReloadProgress => IsReloading ? 1f - reloadLeft / reloadSeconds : 0f;

        float nextShot;
        float reloadLeft;
        float flashLeft;
        float kick;
        float bobTime;
        Vector3 rest;
        PlayerController player;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            Ammo = magazineSize;
            Reserve = startingReserve;
            rest = weaponModel.localPosition;
            muzzleFlash.enabled = false;
            tracer.enabled = false;
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (game == null) return;
            flashLeft -= Time.deltaTime;
            muzzleFlash.enabled = flashLeft > 0f;
            tracer.enabled = flashLeft > 0f;
            if (game.State != RoundState.Playing) return;

            if (IsReloading)
            {
                reloadLeft -= Time.deltaTime;
                if (reloadLeft <= 0f)
                {
                    int loaded = Mathf.Min(magazineSize - Ammo, Reserve);
                    Ammo += loaded;
                    Reserve -= loaded;
                    IsReloading = false;
                    GameAudio.Instance.PlayCue(430f, .07f);
                }
            }
            IsAiming = game.CanControl && Input.GetMouseButton(1) && !IsReloading;
            if (game.CanControl)
            {
                if (Input.GetKeyDown(KeyCode.R)) TryReload();
                if (Input.GetMouseButton(0)) TryFire();
            }
            float targetFov = IsAiming ? 49f : 73f;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, Time.deltaTime * 13f);
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 5f);
            bobTime += Time.deltaTime * (player.IsMoving ? 11f : 2f);
            Vector3 bob = new Vector3(Mathf.Sin(bobTime) * .006f, Mathf.Cos(bobTime * 2f) * .005f, 0f);
            Vector3 aim = IsAiming ? new Vector3(-rest.x, .055f, .1f) : Vector3.zero;
            Vector3 reload = IsReloading ? new Vector3(0f, -.20f, -.1f) : Vector3.zero;
            weaponModel.localPosition = Vector3.Lerp(weaponModel.localPosition, rest + aim + reload + bob + new Vector3(0, 0, -kick * .1f), Time.deltaTime * 15f);
            weaponModel.localRotation = Quaternion.Euler(kick * -7f + (IsReloading ? 22f : 0f), 0f, IsReloading ? -18f : 0f);
        }

        public bool TryFire()
        {
            if (GameSession.Instance == null || !GameSession.Instance.CanControl || IsReloading || Time.time < nextShot) return false;
            if (Ammo <= 0)
            {
                nextShot = Time.time + .22f;
                GameAudio.Instance.PlayCue(110f, .035f);
                if (Reserve > 0) TryReload();
                else GameSession.Instance.Notify("Out of ammo. Find an orange supply crate.");
                return false;
            }
            nextShot = Time.time + 1f / roundsPerSecond;
            Ammo--;
            kick = 1f;
            flashLeft = .045f;
            GameAudio.Instance.Gunshot();
            bool hitTarget = false;
            var origin = viewCamera.transform.position;
            var end = origin + viewCamera.transform.forward * range;
            if (Physics.Raycast(origin, viewCamera.transform.forward, out var hit, range, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var targetHealth = hit.collider.GetComponentInParent<Health>();
                if (targetHealth != null && targetHealth != player.health && targetHealth.IsAlive)
                {
                    targetHealth.TakeDamage(damage);
                    hitTarget = true;
                }
            }
            tracer.SetPosition(0, muzzleFlash.transform.position);
            tracer.SetPosition(1, end);
            GameSession.Instance.RegisterShot(hitTarget);
            return true;
        }

        public void TryReload()
        {
            if (IsReloading || Ammo == magazineSize || Reserve <= 0) return;
            IsReloading = true;
            reloadLeft = reloadSeconds;
            GameAudio.Instance.PlayCue(240f, .1f);
        }

        public void AddAmmo(int amount) => Reserve += Mathf.Max(0, amount);
    }
}
