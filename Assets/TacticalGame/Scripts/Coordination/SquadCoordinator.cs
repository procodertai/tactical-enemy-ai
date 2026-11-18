using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace TacticalGame.Coordination
{
    // One scene-local squad service; all observations and movement feedback come from callers.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // Register the configured roster before individual members' Start calls.
    public sealed partial class SquadCoordinator : MonoBehaviour, ISquadRegistry, ISquadCoordination,
        ISquadDestinationLeases, ISquadMovementCoordination, ISquadObstructionCoordination
    {
        [SerializeField] SquadInterfaceSettings settings;
        [SerializeField] TargetDummy[] configuredEnemies = new TargetDummy[0];
        [SerializeField] TacticalMarker[] configuredDestinations = new TacticalMarker[0];

        readonly Dictionary<string, ISquadMember> members = new Dictionary<string, ISquadMember>(StringComparer.Ordinal);
        readonly Dictionary<int, ITacticalDestination> destinations = new Dictionary<int, ITacticalDestination>();
        readonly List<ISquadMember> memberList = new List<ISquadMember>();
        readonly List<ITacticalDestination> destinationList = new List<ITacticalDestination>();
        readonly List<string> staleMembers = new List<string>();
        readonly List<int> staleDestinations = new List<int>();
        ReadOnlyCollection<ISquadMember> memberView;
        ReadOnlyCollection<ITacticalDestination> destinationView;
        bool started;

        public int MemberCount => members.Count;
        public int DestinationCount => destinations.Count;
        public SquadInterfaceSettings Settings => settings;
        public IReadOnlyList<ISquadMember> Members => memberView ?? (memberView = memberList.AsReadOnly());
        public IReadOnlyList<ITacticalDestination> Destinations => destinationView ?? (destinationView = destinationList.AsReadOnly());
        public event Action RegistryChanged;

        void Start()
        {
            started = true; // Every enemy's Health.Awake has completed before Start.
            RegisterConfiguredObjects();
        }

        void OnEnable()
        {
            if (Application.isPlaying && started) RegisterConfiguredObjects();
        }

        void OnDisable() => ResetRound();

        // Explicit configuration keeps additive scenes and future squads separate.
        public void Configure(TargetDummy[] enemies, TacticalMarker[] markers, SquadInterfaceSettings interfaceSettings)
        {
            configuredEnemies = enemies == null ? new TargetDummy[0] : (TargetDummy[])enemies.Clone();
            configuredDestinations = markers == null ? new TacticalMarker[0] : (TacticalMarker[])markers.Clone();
            settings = interfaceSettings;
            foreach (var enemy in configuredEnemies)
                if (enemy != null && enemy.gameObject.scene == gameObject.scene) enemy.AssignSquadCoordinator(this);
            foreach (var marker in configuredDestinations)
                if (marker != null && marker.gameObject.scene == gameObject.scene) marker.AssignSquadCoordinator(this);
            if (Application.isPlaying && started && isActiveAndEnabled) RebuildRegistry();
        }

        public void RegisterConfiguredObjects()
        {
            if (!isActiveAndEnabled) return;
            foreach (var enemy in configuredEnemies)
            {
                if (enemy == null || enemy.gameObject.scene != gameObject.scene) continue;
                enemy.AssignSquadCoordinator(this);
                var result = RegisterEnemy(enemy);
                if (result == EnemyRegistrationResult.DuplicateId)
                    Debug.LogError("Duplicate squad enemy ID: " + enemy.EnemyId, enemy);
            }
            foreach (var marker in configuredDestinations)
            {
                if (marker == null || marker.gameObject.scene != gameObject.scene) continue;
                marker.AssignSquadCoordinator(this);
                var result = RegisterDestination(marker);
                if (result == DestinationRegistrationResult.DuplicateId)
                    Debug.LogError("Duplicate squad marker ID: " + marker.MarkerId, marker);
            }
        }

        public void RebuildRegistry()
        {
            ResetRound();
            RegisterConfiguredObjects();
        }

        public EnemyRegistrationResult RegisterEnemy(ISquadMember enemy)
        {
            if (!isActiveAndEnabled || !IsAvailable(enemy) || string.IsNullOrWhiteSpace(enemy.EnemyId))
                return EnemyRegistrationResult.InvalidEnemy;
            if (members.TryGetValue(enemy.EnemyId, out var existing))
                return ReferenceEquals(existing, enemy) ? EnemyRegistrationResult.AlreadyRegistered : EnemyRegistrationResult.DuplicateId;
            foreach (var registered in memberList)
                if (ReferenceEquals(registered, enemy)) return EnemyRegistrationResult.InvalidEnemy; // IDs must remain stable.
            members.Add(enemy.EnemyId, enemy);
            memberList.Add(enemy);
            memberList.Sort((a, b) => StringComparer.Ordinal.Compare(a.EnemyId, b.EnemyId));
            if (enemy is TargetDummy target && target.SquadCoordinator != this) target.AssignSquadCoordinator(this);
            RefreshRoles();
            RegistryChanged?.Invoke();
            return EnemyRegistrationResult.Registered;
        }

        public void UnregisterEnemy(string enemyId)
        {
            if (enemyId == null || !members.TryGetValue(enemyId, out var enemy)) return;
            members.Remove(enemyId);
            memberList.Remove(enemy);
            ClearMemberState(enemyId);
            RefreshRoles();
            RegistryChanged?.Invoke();
        }

        public bool TryGetEnemy(string enemyId, out ISquadMember enemy)
        {
            enemy = null;
            return enemyId != null && members.TryGetValue(enemyId, out enemy);
        }

        public DestinationRegistrationResult RegisterDestination(ITacticalDestination destination)
        {
            if (!isActiveAndEnabled || !IsAvailable(destination) || destination.MarkerId <= 0 || !Enum.IsDefined(typeof(MarkerKind), destination.Kind))
                return DestinationRegistrationResult.InvalidDestination;
            if (destinations.TryGetValue(destination.MarkerId, out var existing))
                return ReferenceEquals(existing, destination) ? DestinationRegistrationResult.AlreadyRegistered : DestinationRegistrationResult.DuplicateId;
            foreach (var registered in destinationList)
                if (ReferenceEquals(registered, destination)) return DestinationRegistrationResult.InvalidDestination;
            destinations.Add(destination.MarkerId, destination);
            destinationList.Add(destination);
            destinationList.Sort((a, b) => a.MarkerId.CompareTo(b.MarkerId));
            if (destination is TacticalMarker marker && marker.SquadCoordinator != this) marker.AssignSquadCoordinator(this);
            RegistryChanged?.Invoke();
            return DestinationRegistrationResult.Registered;
        }

        public void UnregisterDestination(int markerId)
        {
            if (!destinations.TryGetValue(markerId, out var destination)) return;
            destinations.Remove(markerId);
            destinationList.Remove(destination);
            ClearDestinationState(markerId);
            RegistryChanged?.Invoke();
        }

        public bool TryGetDestination(int markerId, out ITacticalDestination destination) => destinations.TryGetValue(markerId, out destination);
        public void EnemyDefeated(string enemyId) => UnregisterEnemy(enemyId);

        public void ResetRound()
        {
            bool changed = members.Count != 0 || destinations.Count != 0;
            resetting = true;
            try
            {
                members.Clear(); destinations.Clear(); memberList.Clear(); destinationList.Clear();
                ClearTacticalState(true);
            }
            finally { resetting = false; }
            if (changed) RegistryChanged?.Invoke();
        }

        void LateUpdate()
        {
            // Also supports future interface implementations without TargetDummy's lifecycle hooks.
            staleMembers.Clear(); staleDestinations.Clear();
            foreach (var pair in members)
                if (!IsAvailable(pair.Value) || pair.Value.EnemyId != pair.Key) staleMembers.Add(pair.Key);
            foreach (var pair in destinations)
                if (!IsAvailable(pair.Value) || pair.Value.MarkerId != pair.Key) staleDestinations.Add(pair.Key);
            foreach (string id in staleMembers) UnregisterEnemy(id);
            foreach (int id in staleDestinations) UnregisterDestination(id);
            TickCoordination();
        }

        bool IsAvailable(ISquadMember enemy) => IsAvailableObject(enemy) && enemy.IsAlive;
        bool IsAvailable(ITacticalDestination destination) => IsAvailableObject(destination);

        bool IsAvailableObject(object item)
        {
            if (item == null || (item is UnityEngine.Object obj && obj == null)) return false;
            if (item is Behaviour behaviour)
                return behaviour.isActiveAndEnabled && behaviour.gameObject.scene == gameObject.scene;
            if (item is Component component)
                return component.gameObject.activeInHierarchy && component.gameObject.scene == gameObject.scene;
            return true;
        }
    }
}
