using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace TacticalGame.Coordination
{
    public sealed partial class SquadCoordinator
    {
        [SerializeField, HideInInspector] bool coordinationEnabled = true;
        bool resetting;
        int roundVersion = 1;
        long nextClaimId;
        bool hasSighting;
        float newestObservation = -1f;
        SightingReport sharedReport;
        readonly Dictionary<string, SquadRole> roles = new Dictionary<string, SquadRole>(StringComparer.Ordinal);
        readonly Dictionary<int, ClaimRecord> claims = new Dictionary<int, ClaimRecord>();
        readonly Dictionary<string, int> memberClaims = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, MovementRequest> activeMoves = new Dictionary<string, MovementRequest>(StringComparer.Ordinal);
        readonly Dictionary<string, int> lastMoveIds = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<DestinationClaim> claimSnapshots = new List<DestinationClaim>();
        readonly List<DestinationClaim> claimsToRelease = new List<DestinationClaim>();
        readonly List<SquadDebugEvent> debugEvents = new List<SquadDebugEvent>();
        ReadOnlyCollection<DestinationClaim> claimView;
        ReadOnlyCollection<SquadDebugEvent> eventView;
        readonly SquadMetrics metrics = new SquadMetrics();

        sealed class ClaimRecord
        {
            public string Owner;
            public int Marker;
            public long Id;
            public float Created, Expires;
            public bool Occupied;
        }

        public bool CoordinationEnabled => coordinationEnabled && isActiveAndEnabled && !resetting;
        public int RoundVersion => roundVersion;
        public SquadMetrics Metrics => metrics;
        public IReadOnlyList<DestinationClaim> Claims => claimView ?? (claimView = claimSnapshots.AsReadOnly());
        public IReadOnlyList<SquadDebugEvent> DebugEvents => eventView ?? (eventView = debugEvents.AsReadOnly());
        public event Action<bool> CoordinationChanged;
        public event Action TacticalStateChanged;
        public event Action<DestinationClaim, DestinationReleaseReason> DestinationReleased;

        float SightingLifetime => Positive(settings != null ? settings.SharedSightingLifetimeSeconds : 5f, 5f);
        float LeaseDuration => Positive(settings != null ? settings.DestinationLeaseSeconds : 12f, 12f);
        float OccupancyRadius => Positive(settings != null ? settings.OccupiedMarkerRadius : 1.25f, 1.25f);
        internal float ClaimRenewalInterval => Mathf.Min(.5f, LeaseDuration * .5f);
        internal bool IsAtDestination(Vector3 position, Vector3 destination) => Vector3.Distance(position, destination) <= OccupancyRadius;
        internal bool IsCurrentMovement(MovementRequest request) => CoordinationEnabled && CurrentMove(request, out _);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static float Positive(float value, float fallback) => Finite(value) && value > 0 ? value : fallback;
        bool LiveMember(string id, out ISquadMember member) => TryGetEnemy(id, out member) && IsAvailable(member);

        public void SetCoordinationEnabled(bool enabled)
        {
            if (coordinationEnabled == enabled) return;
            coordinationEnabled = enabled;
            if (!enabled) ClearTacticalState(false);
            else RefreshRoles();
            Log("Coordination " + (enabled ? "ON" : "OFF"));
            CoordinationChanged?.Invoke(enabled);
            TacticalStateChanged?.Invoke();
        }

        public bool ReportSighting(SightingReport report)
        {
            float now = Time.time;
            if (!CoordinationEnabled || !LiveMember(report.ReporterId, out _) || !Finite(report.ObservedPosition) ||
                !Finite(report.ObservedAt) || report.ObservedAt < 0 || report.ObservedAt > now ||
                now - report.ObservedAt > SightingLifetime || report.ObservedAt <= newestObservation)
            {
                metrics.SightingsRejected++;
                return false;
            }
            sharedReport = report; newestObservation = report.ObservedAt; hasSighting = true;
            metrics.SightingsAccepted++;
            Log("Sighting from " + report.ReporterId + " at " + report.ObservedPosition);
            TacticalStateChanged?.Invoke();
            return true;
        }

        public bool TryGetSharedSighting(out SharedSighting sighting)
        {
            sighting = default;
            if (!CoordinationEnabled || !hasSighting) return false;
            float age = Time.time - sharedReport.ObservedAt;
            if (age < 0 || age > SightingLifetime) return false;
            sighting = new SharedSighting(sharedReport, age);
            return true;
        }

        public SquadRole GetRole(string enemyId)
        {
            return CoordinationEnabled && LiveMember(enemyId, out _) && roles.TryGetValue(enemyId, out var role)
                ? role : SquadRole.Unassigned;
        }

        void RefreshRoles()
        {
            if (!CoordinationEnabled) { roles.Clear(); return; }
            var priority = new[] { SquadRole.Attacker, SquadRole.Flanker, SquadRole.Support };
            int count = 0;
            foreach (var member in memberList) if (IsAvailable(member)) count++;
            int desiredCount = Math.Min(count, priority.Length);
            var next = new Dictionary<string, SquadRole>(StringComparer.Ordinal);
            var used = new HashSet<SquadRole>();
            // Keep surviving assignments when they still cover a needed role.
            foreach (var member in memberList)
            {
                if (!IsAvailable(member) || !roles.TryGetValue(member.EnemyId, out var role)) continue;
                if (Array.IndexOf(priority, role) < desiredCount && role != SquadRole.Unassigned && used.Add(role))
                    next.Add(member.EnemyId, role);
            }
            foreach (var member in memberList)
            {
                if (!IsAvailable(member) || next.ContainsKey(member.EnemyId)) continue;
                SquadRole role = SquadRole.Support;
                for (int i = 0; i < desiredCount; i++)
                    if (used.Add(priority[i])) { role = priority[i]; break; }
                next.Add(member.EnemyId, role);
            }
            bool changed = roles.Count != next.Count;
            foreach (var pair in next)
                if (!roles.TryGetValue(pair.Key, out var oldRole) || oldRole != pair.Value)
                { metrics.RoleChanges++; changed = true; Log(pair.Key + " role: " + pair.Value); }
            roles.Clear(); foreach (var pair in next) roles.Add(pair.Key, pair.Value);
            if (changed) TacticalStateChanged?.Invoke();
        }

        public DestinationReservationResult TryReserveDestination(string enemyId, int markerId)
        {
            metrics.ReservationRequests++;
            if (!CoordinationEnabled) return DestinationReservationResult.CoordinationDisabled;
            TickClaims();
            if (!LiveMember(enemyId, out _)) return DestinationReservationResult.InvalidEnemy;
            if (!TryGetDestination(markerId, out var destination) || !IsAvailable(destination) || destination.Kind == MarkerKind.EnemySpawn)
                return DestinationReservationResult.InvalidMarker;
            if (claims.TryGetValue(markerId, out var existing))
            {
                if (existing.Owner != enemyId)
                {
                    metrics.ReservationConflicts++; Log(enemyId + " rejected: marker " + markerId + " owned by " + existing.Owner);
                    return DestinationReservationResult.Occupied;
                }
                existing.Expires = Time.time + LeaseDuration;
                RefreshClaimSnapshots();
                return DestinationReservationResult.AlreadyOwned;
            }
            // An unsuccessful request keeps the previous claim; a successful switch replaces it.
            DestinationClaim? replaced = null;
            if (memberClaims.TryGetValue(enemyId, out int previousMarker))
            {
                replaced = Snapshot(claims[previousMarker]);
                RemoveClaim(previousMarker, DestinationReleaseReason.ActionChanged, false);
            }
            var record = new ClaimRecord { Owner = enemyId, Marker = markerId, Id = ++nextClaimId, Created = Time.time, Expires = Time.time + LeaseDuration };
            claims.Add(markerId, record); memberClaims[enemyId] = markerId;
            metrics.ClaimsGranted++; RefreshClaimSnapshots(); Log(enemyId + " claimed marker " + markerId);
            if (replaced.HasValue) DestinationReleased?.Invoke(replaced.Value, DestinationReleaseReason.ActionChanged);
            TacticalStateChanged?.Invoke();
            return DestinationReservationResult.Granted;
        }

        public bool TryGetDestinationClaim(string enemyId, out DestinationClaim claim)
        {
            claim = default;
            if (!CoordinationEnabled || enemyId == null) return false;
            TickClaims();
            if (!memberClaims.TryGetValue(enemyId, out int marker) || !claims.TryGetValue(marker, out var record)) return false;
            claim = Snapshot(record); return true;
        }

        public bool RenewDestinationClaim(DestinationClaim claim)
        {
            TickClaims();
            if (!CoordinationEnabled || !MatchingClaim(claim, out var record) || !LiveMember(claim.EnemyId, out _)) return false;
            record.Expires = Time.time + LeaseDuration; RefreshClaimSnapshots(); return true;
        }

        public bool ReleaseDestination(string enemyId, int markerId, DestinationReleaseReason reason)
        {
            if (!CoordinationEnabled || !Enum.IsDefined(typeof(DestinationReleaseReason), reason) ||
                !claims.TryGetValue(markerId, out var record) || record.Owner != enemyId) return false;
            RemoveClaim(markerId, reason); return true;
        }

        // Use the token overload for delayed actions, so an old release cannot remove a newer claim.
        public bool ReleaseDestination(DestinationClaim claim, DestinationReleaseReason reason)
        {
            if (!CoordinationEnabled || !Enum.IsDefined(typeof(DestinationReleaseReason), reason) || !MatchingClaim(claim, out _)) return false;
            RemoveClaim(claim.MarkerId, reason); return true;
        }

        bool MatchingClaim(DestinationClaim claim, out ClaimRecord record)
        {
            return claims.TryGetValue(claim.MarkerId, out record) && record.Owner == claim.EnemyId &&
                claim.ClaimId != 0 && record.Id == claim.ClaimId && claim.RoundVersion == roundVersion;
        }

        DestinationClaim Snapshot(ClaimRecord record) => new DestinationClaim(record.Owner, record.Marker, record.Id, roundVersion, record.Created, record.Expires);

        void RefreshClaimSnapshots()
        {
            claimSnapshots.Clear();
            foreach (var claim in claims.Values) claimSnapshots.Add(Snapshot(claim));
            claimSnapshots.Sort((a, b) => a.MarkerId.CompareTo(b.MarkerId));
        }

        void RemoveClaim(int markerId, DestinationReleaseReason reason, bool notify = true)
        {
            if (!claims.TryGetValue(markerId, out var record)) return;
            var snapshot = Snapshot(record);
            claims.Remove(markerId); memberClaims.Remove(record.Owner);
            if (activeMoves.TryGetValue(record.Owner, out var move) && move.ClaimId == record.Id) activeMoves.Remove(record.Owner);
            metrics.ClaimsReleased++;
            if (reason == DestinationReleaseReason.ClaimExpired) metrics.ClaimsExpired++;
            RefreshClaimSnapshots(); Log(record.Owner + " released " + markerId + ": " + reason);
            if (notify) { DestinationReleased?.Invoke(snapshot, reason); TacticalStateChanged?.Invoke(); }
        }

        public bool TryBeginMovement(string enemyId, int requestId, out MovementRequest request)
        {
            request = default;
            if (requestId <= 0 || !TryGetDestinationClaim(enemyId, out var claim) ||
                !TryGetDestination(claim.MarkerId, out var destination) ||
                (lastMoveIds.TryGetValue(enemyId, out int previous) && requestId <= previous)) return false;
            request = new MovementRequest(enemyId, claim.MarkerId, requestId, destination.Position, claim.ClaimId, roundVersion);
            lastMoveIds[enemyId] = requestId; activeMoves[enemyId] = request;
            claims[claim.MarkerId].Occupied = false;
            RenewDestinationClaim(claim);
            return true; // Caller sends the returned request to IEnemyNavigation.MoveTo.
        }

        public bool HandleMovementReport(MovementReport report)
        {
            if (!CoordinationEnabled || !Enum.IsDefined(typeof(MovementStatus), report.Status) || !CurrentMove(report.Request, out var record))
            { metrics.StaleMovementReports++; return false; }
            if (report.Status == MovementStatus.Failed)
                RemoveClaim(record.Marker, DestinationReleaseReason.MovementFailed);
            else if (report.Status == MovementStatus.Arrived)
            {
                if (!LiveMember(record.Owner, out var member) || !TryGetDestination(record.Marker, out var destination) ||
                    !IsAtDestination(member.Position, destination.Position)) return false;
                record.Occupied = true; record.Expires = Time.time + LeaseDuration;
                activeMoves.Remove(record.Owner); RefreshClaimSnapshots(); Log(record.Owner + " arrived at " + record.Marker);
            }
            else Log(record.Owner + " reports obstruction; blocker identity is required for a yield decision.");
            TacticalStateChanged?.Invoke(); return true;
        }

        bool CurrentMove(MovementRequest request, out ClaimRecord record)
        {
            record = null;
            if (request.EnemyId == null || !LiveMember(request.EnemyId, out _) ||
                !activeMoves.TryGetValue(request.EnemyId, out var current) || request.RequestId != current.RequestId ||
                request.MarkerId != current.MarkerId || request.ClaimId != current.ClaimId ||
                request.RoundVersion != roundVersion || request.RoundVersion != current.RoundVersion ||
                !Finite(request.Destination) || Vector3.SqrMagnitude(request.Destination - current.Destination) > .001f ||
                !claims.TryGetValue(request.MarkerId, out record) || record.Id != request.ClaimId || record.Owner != request.EnemyId) return false;
            if (Time.time >= record.Expires) { RemoveClaim(record.Marker, DestinationReleaseReason.ClaimExpired); return false; }
            return true;
        }

        public bool CancelMovement(MovementRequest request)
        {
            if (!CoordinationEnabled || !CurrentMove(request, out var record)) return false;
            RemoveClaim(record.Marker, DestinationReleaseReason.ActionChanged); return true;
        }

        void TickClaims()
        {
            if (!CoordinationEnabled) return;
            claimsToRelease.Clear();
            foreach (var pair in claims)
            {
                var record = pair.Value;
                if (!LiveMember(record.Owner, out _) || !TryGetDestination(pair.Key, out var destination) || !IsAvailable(destination))
                { claimsToRelease.Add(Snapshot(record)); continue; }
                if (record.Occupied && LiveMember(record.Owner, out var member))
                {
                    if (IsAtDestination(member.Position, destination.Position))
                        record.Expires = Time.time + LeaseDuration;
                    else claimsToRelease.Add(Snapshot(record));
                }
                else if (Time.time >= record.Expires) claimsToRelease.Add(Snapshot(record));
            }
            // Snapshot IDs before emitting callbacks: listeners may request/release other claims.
            var releaseIds = claimsToRelease.ToArray();
            foreach (var expired in releaseIds)
            {
                int id = expired.MarkerId;
                if (!claims.TryGetValue(id, out var record) || record.Id != expired.ClaimId) continue;
                var reason = !LiveMember(record.Owner, out _) ? DestinationReleaseReason.MemberUnavailable :
                    !TryGetDestination(id, out var destination) || !IsAvailable(destination) ? DestinationReleaseReason.MarkerUnavailable :
                    record.Occupied ? DestinationReleaseReason.LeavingPosition : DestinationReleaseReason.ClaimExpired;
                RemoveClaim(id, reason);
            }
            RefreshClaimSnapshots();
        }

        void ClearMemberState(string id)
        {
            if (memberClaims.TryGetValue(id, out int marker)) RemoveClaim(marker, DestinationReleaseReason.MemberUnavailable);
            roles.Remove(id); activeMoves.Remove(id); lastMoveIds.Remove(id);
            RemoveObstructionsFor(id);
        }

        void ClearDestinationState(int id) => RemoveClaim(id, DestinationReleaseReason.MarkerUnavailable);

        public void ResetCoordinationContext()
        {
            resetting = true;
            try { ClearTacticalState(true); }
            finally { resetting = false; }
            RefreshRoles(); TacticalStateChanged?.Invoke();
        }

        public void ResetMetrics() { metrics.Reset(); debugEvents.Clear(); }

        void ClearTacticalState(bool resetMetrics)
        {
            var released = claimSnapshots.ToArray();
            claims.Clear(); memberClaims.Clear(); claimSnapshots.Clear(); roles.Clear(); activeMoves.Clear(); lastMoveIds.Clear();
            obstructionPairs.Clear(); yieldInstructions.Clear(); hasSighting = false; newestObservation = -1;
            if (resetMetrics)
            { roundVersion = roundVersion == int.MaxValue ? 1 : roundVersion + 1; metrics.Reset(); debugEvents.Clear(); }
            else metrics.ClaimsReleased += released.Length;
            foreach (var claim in released) DestinationReleased?.Invoke(claim,
                resetMetrics ? DestinationReleaseReason.RoundReset : DestinationReleaseReason.CoordinationDisabled);
        }

        void TickCoordination()
        {
            if (!CoordinationEnabled) return;
            if (hasSighting && Time.time - sharedReport.ObservedAt > SightingLifetime)
            { hasSighting = false; Log("Shared sighting expired."); TacticalStateChanged?.Invoke(); }
            TickClaims(); TickObstructions();
        }

        void Log(string message)
        {
            if (debugEvents.Count == 16) debugEvents.RemoveAt(0);
            debugEvents.Add(new SquadDebugEvent(Time.time, message));
        }
    }
}
