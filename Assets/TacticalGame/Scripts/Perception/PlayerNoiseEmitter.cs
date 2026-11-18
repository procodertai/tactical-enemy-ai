using UnityEngine;

namespace TacticalGame
{
    // Turns player actions into noise events for GuardPerception, without editing player/rifle code.
    // Gunshots are detected by the rifle ammo count dropping; sprinting makes footstep noise.
    [DisallowMultipleComponent]
    public sealed class PlayerNoiseEmitter : MonoBehaviour
    {
        public float gunshotRadius = 40f;
        public float gunshotBoost = 0.6f;
        public float sprintRadius = 14f;
        public float sprintBoost = 0.12f;
        public float sprintInterval = 0.4f;

        PlayerController player;
        int lastAmmo;
        float nextStep;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            if (player != null && player.rifle != null) lastAmmo = player.rifle.Ammo;
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (player == null || player.rifle == null || game == null || !game.CanControl) return;

            int ammo = player.rifle.Ammo;
            if (ammo < lastAmmo)
                GuardPerception.BroadcastNoise(transform.position, gunshotRadius, gunshotBoost);
            lastAmmo = ammo;

            bool sprinting = player.IsMoving && Input.GetKey(KeyCode.LeftShift) && !player.rifle.IsAiming;
            if (sprinting && Time.time >= nextStep)
            {
                nextStep = Time.time + sprintInterval;
                GuardPerception.BroadcastNoise(transform.position, sprintRadius, sprintBoost);
            }
        }
    }
}
