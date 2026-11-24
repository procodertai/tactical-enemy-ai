using System;
using UnityEngine;

namespace TacticalGame
{
    // Shared by the player and target dummies. AI can call TakeDamage later.
    public sealed class Health : MonoBehaviour
    {
        [Min(1)] public float maximum = 100f;
        public bool invulnerable; // demo aid: hits still register (flash, events) but health never drops
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;
        public event Action<float> Damaged;
        public event Action Died;

        void Awake() => ResetHealth();

        public void ResetHealth() => Current = maximum;

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            if (!invulnerable) Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount);
            if (!IsAlive) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsAlive) Current = Mathf.Min(maximum, Current + Mathf.Max(0f, amount));
        }
    }
}
