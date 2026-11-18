using UnityEngine;
using TacticalGame.Coordination;

namespace TacticalGame
{
    // Enemy health/visual response, separate from the replaceable BasicGuard behavior.
    public sealed class TargetDummy : MonoBehaviour, ISquadMember
    {
        public string targetId = "01";
        public Health health;
        public Transform body;
        public TextMesh label;
        public Renderer chest;
        public Material hitMaterial;
        [SerializeField] SquadCoordinator squadCoordinator;
        public string EnemyId => targetId;
        public bool IsAlive => health != null && health.IsAlive;
        public Vector3 Position => transform.position;
        public SquadCoordinator SquadCoordinator => squadCoordinator;
        bool started;
        string registeredId;
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
            LeaveSquad();
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            label.text = targetId + " / DOWN";
            label.color = new Color(.35f, 1f, .6f);
            GameSession.Instance.Notify("Guard " + targetId + " disabled.");
        }

        void Start() { started = true; JoinSquad(); }
        void OnEnable() { if (Application.isPlaying && started) JoinSquad(); }
        void OnDisable() => LeaveSquad();

        public void AssignSquadCoordinator(SquadCoordinator coordinator)
        {
            if (squadCoordinator == coordinator) return;
            LeaveSquad();
            squadCoordinator = coordinator;
            if (Application.isPlaying && started) JoinSquad();
        }

        void JoinSquad()
        {
            if (!isActiveAndEnabled || squadCoordinator == null) return;
            var result = squadCoordinator.RegisterEnemy(this);
            if (result == EnemyRegistrationResult.Registered || result == EnemyRegistrationResult.AlreadyRegistered)
                registeredId = EnemyId;
        }

        void LeaveSquad()
        {
            // A rejected duplicate must never unregister the accepted owner of the same ID.
            string id = registeredId ?? EnemyId;
            if (squadCoordinator != null && squadCoordinator.TryGetEnemy(id, out var owner) && ReferenceEquals(owner, this))
                squadCoordinator.UnregisterEnemy(id);
            registeredId = null;
        }

        void OnDestroy()
        {
            LeaveSquad();
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDefeated;
        }

        void Update()
        {
            if (chest.sharedMaterial == hitMaterial && Time.time > flashUntil) chest.sharedMaterial = normalMaterial;
            if (!health.IsAlive) body.localRotation = Quaternion.Slerp(body.localRotation, standing * Quaternion.Euler(78f, 0f, 0f), Time.deltaTime * 5f);
        }
    }
}
