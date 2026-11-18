using UnityEngine;

namespace TacticalGame.Coordination
{
    // STEP 01: shared vocabulary and message data only. No coordination policies live here.
    public static class SquadContractVersion
    {
        public const string Current = "0.2";
    }

    public enum SquadRole
    {
        Unassigned = 0,
        Attacker = 1,
        Flanker = 2,
        Support = 3
    }

    public enum EnemyRegistrationResult
    {
        Registered = 0,
        AlreadyRegistered = 1,
        DuplicateId = 2,
        InvalidEnemy = 3
    }

    public enum DestinationRegistrationResult
    {
        Registered = 0,
        AlreadyRegistered = 1,
        DuplicateId = 2,
        InvalidDestination = 3
    }

    public enum DestinationReservationResult
    {
        Granted = 0,
        AlreadyOwned = 1,
        Occupied = 2,
        InvalidEnemy = 3,
        InvalidMarker = 4,
        CoordinationDisabled = 5
    }

    public enum DestinationReleaseReason
    {
        LeavingPosition = 0,
        ActionChanged = 1,
        EnemyDefeated = 2,
        MovementFailed = 3,
        RoundReset = 4,
        CoordinationDisabled = 5,
        ClaimExpired = 6,
        MarkerUnavailable = 7,
        Obstruction = 8,
        MemberUnavailable = 9
    }

    public enum MovementStatus
    {
        Arrived = 0,
        Failed = 1,
        Obstructed = 2
    }

    // These immutable messages are passed between C# components, not edited in the Inspector.
    public readonly struct SightingReport
    {
        public string ReporterId { get; }
        public Vector3 ObservedPosition { get; }
        public float ObservedAt { get; }

        /// <param name="observedAt">Time.time at observation, from this Unity instance.</param>
        public SightingReport(string reporterId, Vector3 observedPosition, float observedAt)
        {
            ReporterId = reporterId;
            ObservedPosition = observedPosition;
            ObservedAt = observedAt;
        }
    }

    public readonly struct SharedSighting
    {
        public SightingReport Report { get; }
        public float AgeSeconds { get; }

        // The coordinator supplies age using the same clock as Report.ObservedAt.
        public SharedSighting(SightingReport report, float ageSeconds)
        {
            Report = report;
            AgeSeconds = ageSeconds;
        }
    }

    public readonly struct DestinationClaim
    {
        public string EnemyId { get; }
        public int MarkerId { get; }
        public long ClaimId { get; }
        public int RoundVersion { get; }
        public float ClaimedAt { get; }
        public float ExpiresAt { get; }

        public DestinationClaim(string enemyId, int markerId)
            : this(enemyId, markerId, 0, 0, 0, 0) { }

        public DestinationClaim(string enemyId, int markerId, long claimId, int roundVersion, float claimedAt, float expiresAt)
        {
            EnemyId = enemyId; MarkerId = markerId; ClaimId = claimId;
            RoundVersion = roundVersion; ClaimedAt = claimedAt; ExpiresAt = expiresAt;
        }
    }

    public readonly struct ReachableDestination
    {
        public int MarkerId { get; }
        public Vector3 Position { get; }

        // The positioning provider must establish reachability before returning this candidate.
        public ReachableDestination(int markerId, Vector3 position)
        {
            MarkerId = markerId;
            Position = position;
        }
    }

    public readonly struct MovementRequest
    {
        public string EnemyId { get; }
        public int MarkerId { get; }
        public int RequestId { get; }
        public Vector3 Destination { get; }
        public long ClaimId { get; }
        public int RoundVersion { get; }

        // The executor issues a fresh request ID for each move; do not reuse it within a round.
        public MovementRequest(string enemyId, int markerId, int requestId, Vector3 destination)
            : this(enemyId, markerId, requestId, destination, 0, 0) { }

        public MovementRequest(string enemyId, int markerId, int requestId, Vector3 destination, long claimId, int roundVersion)
        {
            EnemyId = enemyId; MarkerId = markerId; RequestId = requestId;
            Destination = destination; ClaimId = claimId; RoundVersion = roundVersion;
        }
    }

    public readonly struct MovementReport
    {
        public MovementRequest Request { get; }
        public MovementStatus Status { get; }
        public string Reason { get; }

        // Returning the original request lets the executor reject results from an obsolete move.
        public MovementReport(MovementRequest request, MovementStatus status, string reason = "")
        {
            Request = request;
            Status = status;
            Reason = reason;
        }
    }

    public enum YieldAction { None, Proceed, Wait, Replan, CoordinationDisabled, InvalidRequest }

    public readonly struct YieldInstruction
    {
        public string EnemyId { get; }
        public string OtherEnemyId { get; }
        public YieldAction Action { get; }
        public float WaitUntil { get; }
        public YieldInstruction(string enemyId, string otherEnemyId, YieldAction action, float waitUntil)
        { EnemyId = enemyId; OtherEnemyId = otherEnemyId; Action = action; WaitUntil = waitUntil; }
    }

    public readonly struct SquadDebugEvent
    {
        public float Time { get; }
        public string Message { get; }
        public SquadDebugEvent(float time, string message) { Time = time; Message = message; }
    }

    public sealed class SquadMetrics
    {
        public int ReservationRequests { get; internal set; }
        public int ReservationConflicts { get; internal set; }
        public int ClaimsGranted { get; internal set; }
        public int ClaimsReleased { get; internal set; }
        public int ClaimsExpired { get; internal set; }
        public int SightingsAccepted { get; internal set; }
        public int SightingsRejected { get; internal set; }
        public int RoleChanges { get; internal set; }
        public int ObstructionReports { get; internal set; }
        public int ReplanRequests { get; internal set; }
        public int StaleMovementReports { get; internal set; }
        internal void Reset()
        {
            ReservationRequests = ReservationConflicts = ClaimsGranted = ClaimsReleased = ClaimsExpired = 0;
            SightingsAccepted = SightingsRejected = RoleChanges = ObstructionReports = ReplanRequests = StaleMovementReports = 0;
        }
    }
}
