using UnityEngine;

namespace TacticalGame
{
    // Enemy health/visual response, separate from the replaceable BasicGuard behavior.
    public sealed class TargetDummy : MonoBehaviour
    {
        public string targetId = "01";
        public Health health;
        public Transform body;
        public TextMesh label;
        public Renderer chest;
        public Material hitMaterial;
        Material normalMaterial;
        float flashUntil;
        Quaternion standing;

        void Awake()
        {
            normalMaterial = chest.sharedMaterial;
            standing = body.localRotation;
            health.Damaged += OnDamaged;
            health.Died += OnDefeated;
        }

        void OnDamaged(float amount)
        {
            flashUntil = Time.time + .12f;
            chest.sharedMaterial = hitMaterial;
        }

        void OnDefeated()
        {
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            label.text = targetId + " / DOWN";
            label.color = new Color(.35f, 1f, .6f);
            GameSession.Instance.Notify("Guard " + targetId + " disabled.");
        }

        void Update()
        {
            if (chest.sharedMaterial == hitMaterial && Time.time > flashUntil) chest.sharedMaterial = normalMaterial;
            if (!health.IsAlive) body.localRotation = Quaternion.Slerp(body.localRotation, standing * Quaternion.Euler(78f, 0f, 0f), Time.deltaTime * 5f);
        }
    }
}
