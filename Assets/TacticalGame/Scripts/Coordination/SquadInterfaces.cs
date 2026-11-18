using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame.Coordination
{
    /// <summary>Read-only identity supplied by an existing enemy component.</summary>
    public interface ISquadMember
    {
        string EnemyId { get; }
        bool IsAlive { get; }
        Vector3 Position { get; }
    }

    /// <summary>A scene marker; its existence alone does not prove cover or reachability.</summary>
    public interface ITacticalDestination
    {
        int MarkerId { get; }
        MarkerKind Kind { get; }
        Vector3 Position { get; }
    }

    /// <summary>Scene-local identities and lifecycle, also usable independently of tactical coordination.</summary>
    public interface ISquadRegistry
    {
        int MemberCount { get; }
        int DestinationCount { get; }
        IReadOnlyList<ISquadMember> Members { get; }
        IReadOnlyList<ITacticalDestination> Destinations { get; }
        event Action RegistryChanged;

        EnemyRegistrationResult RegisterEnemy(ISquadMember enemy);
        void UnregisterEnemy(string enemyId);
        bool TryGetEnemy(string enemyId, out ISquadMember enemy);
        DestinationRegistrationResult RegisterDestination(ITacticalDestination destination);
        void UnregisterDestination(int markerId);
        bool TryGetDestination(int markerId, out ITacticalDestination destination);
        void EnemyDefeated(string enemyId);
        void ResetRound();
    }

    /// <summary>
    /// Contract for the full tactical service implemented by SquadCoordinator.
    /// Utility AI/execution consumes this interface; perception submits observations to it.
    /// </summary>
    public interface ISquadCoordination
    {
        bool CoordinationEnabled { get; }
        event Action<bool> CoordinationChanged;

        EnemyRegistrationResult RegisterEnemy(ISquadMember enemy);
        void UnregisterEnemy(string enemyId);

        // True only if a submitted observation is accepted. No hidden-player transform updates.
        bool ReportSighting(SightingReport report);
        SquadRole GetRole(string enemyId);

        // False means no usable shared observation, including expiry or coordination being off.
        bool TryGetSharedSighting(out SharedSighting sighting);

        DestinationReservationResult TryReserveDestination(string enemyId, int markerId);
        bool TryGetDestinationClaim(string enemyId, out DestinationClaim claim);

        // True only when a matching owned claim is released. Repeated/foreign releases return false.
        bool ReleaseDestination(string enemyId, int markerId, DestinationReleaseReason reason);
        void EnemyDefeated(string enemyId);

        // Clears round-scoped membership, reports, roles, and claims.
        void ResetRound();

        // Off clears shared state, returns Unassigned roles, and uses CoordinationDisabled responses.
        // Turning back on requires re-requesting claims during the next decision cycle.
        void SetCoordinationEnabled(bool enabled);
    }

    /// <summary>Implemented by teammate 2. Utility maps its chosen action to a marker kind.</summary>
    public interface IPositioningProvider
    {
        // Return an empty list if no valid candidate exists; never null.
        // Optional knownThreat is an actual observation, not the player's hidden current position.
        IReadOnlyList<ReachableDestination> GetReachableCandidates(
            string enemyId, MarkerKind kind, SightingReport? knownThreat);
    }

    /// <summary>Implemented by teammate 2 and consumed by the action executor.</summary>
    public interface IEnemyNavigation
    {
        // Arrived preserves the claim; Failed permits cleanup after the executor checks RequestId.
        // Obstructed is informational: it does not automatically cancel movement or release a claim.
        event Action<MovementReport> MovementReported;

        void MoveTo(MovementRequest request);
        void CancelMove(string enemyId, int requestId);
    }

    public interface ISquadDestinationLeases
    {
        IReadOnlyList<DestinationClaim> Claims { get; }
        bool RenewDestinationClaim(DestinationClaim claim);
        bool ReleaseDestination(DestinationClaim claim, DestinationReleaseReason reason);
    }

    public interface ISquadMovementCoordination
    {
        int RoundVersion { get; }
        bool TryBeginMovement(string enemyId, int requestId, out MovementRequest request);
        bool HandleMovementReport(MovementReport report);
        bool CancelMovement(MovementRequest request);
    }

    public interface ISquadObstructionCoordination
    {
        YieldInstruction ReportObstruction(string enemyId, string blockingEnemyId);
        bool TryGetYieldInstruction(string enemyId, out YieldInstruction instruction);
    }
}
