using System.Collections.Generic;
using TacticalGame.Coordination;
using UnityEngine;

namespace TacticalGame
{
    // Executes decisions that need a path and holds at most one destination claim.
    sealed class TacticalMovement
    {
        readonly TargetDummy guard;
        readonly PathMover navigator;
        readonly GuardScout scout;
        DestinationClaim? claim;
        Vector3 markerPosition;
        Vector3 investigationPoint;
        EnemyAction markerAction;
        int requestId;
        int failedMarker;
        float avoidMarkerUntil, nextAttempt, renewAt;
        float defendedHealth, settleUntil;
        bool arrived, completedDefense;
        readonly Dictionary<EnemyAction, float> retryAfter = new Dictionary<EnemyAction, float>();

        SquadCoordinator Squad => guard.SquadCoordinator;
        public bool AtMarker => claim.HasValue && Squad != null && Squad.CoordinationEnabled && !navigator.IsMoving &&
            Squad.IsAtDestination(guard.Position, markerPosition);
        public bool SettlingAfterDefense => AtMarker && Time.time < settleUntil;

        public bool CanSelect(EnemyAction action)
        {
            if (IsDefensive(action) && completedDefense && (guard.health.Current >= defendedHealth || Time.time < settleUntil))
                return false;
            return !retryAfter.TryGetValue(action, out var retryAt) || Time.time >= retryAt;
        }

        public void RefreshClaim()
        {
            if (claim.HasValue && Time.time >= renewAt) RenewClaim();
            if (arrived || !AtMarker) return;
            arrived = true;
            if (!IsDefensive(markerAction)) return;
            completedDefense = true;
            defendedHealth = guard.health.Current;
            settleUntil = Time.time + 1f;
        }

        public TacticalMovement(TargetDummy guard, PathMover navigator)
        {
            this.guard = guard;
            this.navigator = navigator;
            scout = guard.GetComponent<GuardScout>();
            if (scout != null) scout.UseUtilityControl();
        }

        public void Step(EnemyAction action, SightingReport? knowledge, bool allowScouting)
        {
            if (allowScouting && scout != null && scout.isActiveAndEnabled)
            {
                if (knowledge.HasValue) scout.RememberForUtility(knowledge.Value.ObservedPosition);
                bool scoutDuringHold = action == EnemyAction.Hold && !knowledge.HasValue;
                bool searchOnArrival = action == EnemyAction.Investigate && knowledge.HasValue &&
                    NearInvestigationPoint(knowledge.Value.ObservedPosition);
                if (scoutDuringHold || searchOnArrival)
                {
                    if (!scout.IsUtilityScouting) Stop();
                    scout.TickUtilityScouting();
                    return;
                }
            }
            if (scout != null) scout.StopUtilityScouting();

            if (!knowledge.HasValue)
            {
                Stop();
                return;
            }

            var target = knowledge.Value;
            if (MarkerFor(action, out var kind) && Squad != null && Squad.CoordinationEnabled)
            {
                if (claim.HasValue && markerAction == action && (navigator.IsMoving || AtMarker)) return;
                if (claim.HasValue && markerAction != action)
                {
                    ReleaseClaim();
                    nextAttempt = 0f;
                }
                if (Time.time < nextAttempt || !CanSelect(action)) return;
                ReleaseClaim();
                nextAttempt = Time.time + .65f;
                if (TryMoveToMarker(action, kind, target) ||
                    (kind == MarkerKind.Fallback && TryMoveToMarker(action, MarkerKind.Cover, target))) return;
                // Let the scorer choose a different action while occupied or unreachable markers cool down.
                retryAfter[action] = Time.time + 2f;
                navigator.Stop();
                return;
            }

            if (action == EnemyAction.Investigate)
            {
                ReleaseClaim();
                Investigate(target.ObservedPosition);
                return;
            }

            if (!AtMarker) Stop();
        }

        void RenewClaim()
        {
            var owned = claim.Value;
            if (Squad != null && Squad.RenewDestinationClaim(owned))
            {
                renewAt = Time.time + Squad.ClaimRenewalInterval;
                return;
            }
            failedMarker = owned.MarkerId;
            avoidMarkerUntil = Time.time + 2f;
            claim = null;
            navigator.Stop();
        }

        bool TryMoveToMarker(EnemyAction action, MarkerKind kind, SightingReport target)
        {
            IPositioningProvider positioning = Positioning.Instance;
            if (positioning == null) return false;
            var squad = Squad;
            foreach (var candidate in positioning.GetReachableCandidates(guard.EnemyId, kind, target))
            {
                if (candidate.MarkerId == failedMarker && Time.time < avoidMarkerUntil) continue;
                if (IsDefensive(action) && squad.IsAtDestination(guard.Position, candidate.Position)) continue;
                var result = squad.TryReserveDestination(guard.EnemyId, candidate.MarkerId);
                if (result != DestinationReservationResult.Granted && result != DestinationReservationResult.AlreadyOwned) continue;
                if (!squad.TryGetDestinationClaim(guard.EnemyId, out var reserved)) continue;
                if (!squad.TryBeginMovement(guard.EnemyId, ++requestId, out var request))
                {
                    squad.ReleaseDestination(reserved, DestinationReleaseReason.MovementFailed);
                    continue;
                }
                claim = reserved;
                arrived = false;
                markerAction = action;
                markerPosition = candidate.Position;
                renewAt = Time.time + squad.ClaimRenewalInterval;
                navigator.MoveTo(request);
                return true;
            }
            return false;
        }

        static bool IsDefensive(EnemyAction action) => action == EnemyAction.TakeCover || action == EnemyAction.FallBack;

        void Investigate(Vector3 destination)
        {
            if (NearInvestigationPoint(destination)) { navigator.Stop(); return; }
            if (Time.time < nextAttempt || (navigator.IsMoving && (destination - investigationPoint).sqrMagnitude < 1f)) return;
            nextAttempt = Time.time + .5f;
            StartInvestigation(destination);
        }

        bool NearInvestigationPoint(Vector3 destination)
        {
            Vector3 offset = destination - guard.Position;
            offset.y = 0;
            return offset.sqrMagnitude < 2.4f * 2.4f;
        }

        void StartInvestigation(Vector3 destination)
        {
            investigationPoint = destination;
            navigator.MoveTo(destination);
        }

        public void Stop()
        {
            if (scout != null) scout.StopUtilityScouting();
            navigator.Stop();
            ReleaseClaim();
        }

        void ReleaseClaim()
        {
            if (!claim.HasValue) return;
            var owned = claim.Value;
            claim = null;
            navigator.Stop();
            if (Squad != null) Squad.ReleaseDestination(owned, DestinationReleaseReason.ActionChanged);
        }

        static bool MarkerFor(EnemyAction action, out MarkerKind kind)
        {
            switch (action)
            {
                case EnemyAction.Flank: kind = MarkerKind.Flank; return true;
                case EnemyAction.FallBack: kind = MarkerKind.Fallback; return true;
                case EnemyAction.Support:
                case EnemyAction.TakeCover: kind = MarkerKind.Cover; return true;
                default: kind = default; return false;
            }
        }
    }
}
