using UnityEngine;

namespace TacticalGame
{
    // Turns player actions into noise events for GuardPerception, without editing player/rifle code.
    // The round's shot count tracks fired rounds; sprinting makes footstep noise.
    [DisallowMultipleComponent]
    public sealed class PlayerNoiseEmitter : MonoBehaviour
    {
        public float gunshotRadius = 40f;
        public float gunshotBoost = 0.6f;
        public float sprintRadius = 14f;
        public float sprintBoost = 0.12f;
        public float sprintInterval = 0.4f;
        public KeyCode debugNoiseKey = KeyCode.N;
        public float debugNoiseRadius = 15f;

        PlayerController player;
        int lastShotsFired;
        float nextStep;

        void Awake() { player = GetComponent<PlayerController>(); }

        void Update()
        {
            var game = GameSession.Instance;
            if (player == null || player.rifle == null || game == null || !game.CanControl) return;

            if (game.ShotsFired > lastShotsFired)
                GuardPerception.BroadcastNoise(transform.position, gunshotRadius, gunshotBoost);
            lastShotsFired = game.ShotsFired;

            if (debugNoiseKey != KeyCode.None && Input.GetKeyDown(debugNoiseKey))
                GuardPerception.BroadcastNoise(transform.position, debugNoiseRadius);

            bool sprinting = player.IsMoving && Input.GetKey(KeyCode.LeftShift) && !player.rifle.IsAiming;
            if (sprinting && Time.time >= nextStep)
            {
                nextStep = Time.time + sprintInterval;
                GuardPerception.BroadcastNoise(transform.position, sprintRadius, sprintBoost);
            }
        }
    }
}
