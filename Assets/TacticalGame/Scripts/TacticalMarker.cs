using UnityEngine;
using TacticalGame.Coordination;

namespace TacticalGame
{
    public enum MarkerKind { Cover, Flank, Fallback, EnemySpawn }

    // Data for future lessons. No route search or destination selection is implemented.
    public sealed class TacticalMarker : MonoBehaviour, ITacticalDestination
    {
        public MarkerKind kind;
        public int id;
        [SerializeField] SquadCoordinator squadCoordinator;
        public int MarkerId => id;
        public MarkerKind Kind => kind;
        public Vector3 Position => transform.position;
        public SquadCoordinator SquadCoordinator => squadCoordinator;
        int? registeredId;

        void OnEnable() { if (Application.isPlaying) JoinSquad(); }
        void OnDisable() => LeaveSquad();

        public void AssignSquadCoordinator(SquadCoordinator coordinator)
        {
            if (squadCoordinator == coordinator) return;
            LeaveSquad();
            squadCoordinator = coordinator;
            if (Application.isPlaying) JoinSquad();
        }

        void JoinSquad()
        {
            if (!isActiveAndEnabled || squadCoordinator == null) return;
            var result = squadCoordinator.RegisterDestination(this);
            if (result == DestinationRegistrationResult.Registered || result == DestinationRegistrationResult.AlreadyRegistered)
                registeredId = MarkerId;
        }

        void LeaveSquad()
        {
            int key = registeredId ?? MarkerId;
            if (squadCoordinator != null && squadCoordinator.TryGetDestination(key, out var owner) && ReferenceEquals(owner, this))
                squadCoordinator.UnregisterDestination(key);
            registeredId = null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == MarkerKind.Cover ? Color.cyan : kind == MarkerKind.Flank ? Color.yellow : Color.magenta;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .25f, .3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
