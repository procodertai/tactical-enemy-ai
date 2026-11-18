using UnityEngine;

namespace TacticalGame.Coordination
{
    /// <summary>Tuning for coordination; perception, navigation and action scoring remain separate.</summary>
    [CreateAssetMenu(fileName = "SquadInterfaceSettings", menuName = "Tactical Game/Coordination/Interface Settings")]
    public sealed class SquadInterfaceSettings : ScriptableObject
    {
        public string ContractVersion => SquadContractVersion.Current;

        [Tooltip("Shared report lifetime, in scaled Unity seconds.")]
        [SerializeField, Min(.1f)] float sharedSightingLifetimeSeconds = 5f;
        [SerializeField, Min(.2f)] float destinationLeaseSeconds = 12f;
        [SerializeField, Min(.1f)] float occupiedMarkerRadius = 1.25f;
        [SerializeField, Min(.1f)] float obstructionReplanSeconds = 2f;
        [SerializeField, Min(.1f)] float obstructionReportLifetime = .75f;
        [SerializeField, Min(.2f)] float obstructionDistance = 2.5f;

        public float SharedSightingLifetimeSeconds => sharedSightingLifetimeSeconds;
        public float DestinationLeaseSeconds => destinationLeaseSeconds;
        public float OccupiedMarkerRadius => occupiedMarkerRadius;
        public float ObstructionReplanSeconds => obstructionReplanSeconds;
        public float ObstructionReportLifetime => obstructionReportLifetime;
        public float ObstructionDistance => obstructionDistance;
    }
}
