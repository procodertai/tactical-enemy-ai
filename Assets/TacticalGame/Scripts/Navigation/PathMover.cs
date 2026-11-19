using System;
using System.Collections.Generic;
using TacticalGame.Coordination;
using UnityEngine;

namespace TacticalGame
{
    // Follows A* paths and reports outcomes for coordinated requests.
    public sealed class PathMover : MonoBehaviour, IEnemyNavigation
    {
        public static readonly List<PathMover> All = new List<PathMover>();

        public float speed = 3.5f;
        public float turnSpeed = 8f;
        public float arriveDistance = 0.15f;

        public bool IsMoving => path != null && index < path.Count;
        public bool PathFailed { get; private set; }
        public event Action<MovementReport> MovementReported;

        List<Vector3> path;
        int index;
        Health health;
        TargetDummy guard;
        MovementRequest? trackedMove;
        readonly RaycastHit[] nearbyHits = new RaycastHit[12];
        float nextObstructionReport;
        string lastBlockerId;

        void OnEnable()  { All.Add(this); }
        void OnDisable() { All.Remove(this); Stop(); }
        void Awake()
        {
            health = GetComponent<Health>();
            guard = GetComponent<TargetDummy>();
        }

        public bool MoveTo(Vector3 destination)
        {
            Stop();
            return SetPath(destination);
        }

        bool SetPath(Vector3 destination)
        {
            var pf = AStarPathfinder.Instance;
            path = pf != null ? pf.FindPath(transform.position, destination) : null;
            index = 0;
            PathFailed = path == null;
            return !PathFailed;
        }

        public void MoveTo(MovementRequest request)
        {
            var squad = guard != null ? guard.SquadCoordinator : null;
            if (squad == null || request.EnemyId != guard.EnemyId || !squad.IsCurrentMovement(request)) return;
            Stop();
            trackedMove = request;
            if (!SetPath(request.Destination) || !squad.IsAtDestination(EndPosition(transform.position, path), request.Destination))
                Finish(MovementStatus.Failed, "No reachable endpoint for the marker");
            else if (path.Count == 0) CompleteMove();
        }

        public void CancelMove(string enemyId, int requestId)
        {
            if (trackedMove.HasValue && trackedMove.Value.EnemyId == enemyId && trackedMove.Value.RequestId == requestId) Stop();
        }

        public void Stop()
        {
            path = null;
            trackedMove = null;
            lastBlockerId = null;
            nextObstructionReport = 0f;
        }

        internal static Vector3 EndPosition(Vector3 start, List<Vector3> route)
        {
            if (route.Count == 0) return start;
            Vector3 end = route[route.Count - 1];
            return new Vector3(end.x, start.y, end.z);
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (game != null && game.State != RoundState.Playing) return;
            if (health != null && !health.IsAlive) { Stop(); return; }
            if (!IsMoving) return;
            var squad = guard != null ? guard.SquadCoordinator : null;
            if (trackedMove.HasValue && (squad == null || !squad.IsCurrentMovement(trackedMove.Value))) { Stop(); return; }

            Vector3 flat = path[index] - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= arriveDistance)
            {
                if (++index == path.Count) CompleteMove();
                return;
            }

            Vector3 step = Vector3.ClampMagnitude(flat, speed * Time.deltaTime);
            if (step.sqrMagnitude == 0f) return;
            if (squad != null && WaitForGuard(squad, step)) return;
            if (HitsSolidObstacle(step)) { Finish(MovementStatus.Failed, "Path blocked by geometry"); return; }
            transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
        }

        bool WaitForGuard(SquadCoordinator squad, Vector3 step)
        {
            const float separation = .7f;
            foreach (var member in squad.Members)
            {
                if (!member.IsAlive || member.EnemyId == guard.EnemyId) continue;
                Vector3 offset = member.Position - transform.position;
                offset.y = 0;
                float after = (offset - step).sqrMagnitude;
                if (after >= separation * separation || after >= offset.sqrMagnitude) continue;
                if (squad.CoordinationEnabled && (member.EnemyId != lastBlockerId || Time.time >= nextObstructionReport))
                {
                    if (trackedMove.HasValue && member.EnemyId != lastBlockerId)
                        MovementReported?.Invoke(new MovementReport(trackedMove.Value, MovementStatus.Obstructed, member.EnemyId));
                    lastBlockerId = member.EnemyId;
                    nextObstructionReport = Time.time + .25f;
                    var instruction = squad.ReportObstruction(guard.EnemyId, member.EnemyId);
                    if (instruction.Action == YieldAction.Replan) Stop();
                }
                return true;
            }
            lastBlockerId = null;
            return squad.TryGetYieldInstruction(guard.EnemyId, out var activeInstruction) && activeInstruction.Action == YieldAction.Wait;
        }

        bool HitsSolidObstacle(Vector3 step)
        {
            int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * .9f, .3f, step.normalized,
                nearbyHits, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (nearbyHits[i].collider.GetComponentInParent<Health>() == null) return true;
            return false;
        }

        void CompleteMove()
        {
            if (!trackedMove.HasValue) { Stop(); return; }
            bool atMarker = guard.SquadCoordinator.IsAtDestination(transform.position, trackedMove.Value.Destination);
            Finish(atMarker ? MovementStatus.Arrived : MovementStatus.Failed, atMarker ? "" : "Stopped outside the marker radius");
        }

        void Finish(MovementStatus status, string reason)
        {
            var request = trackedMove;
            PathFailed = status == MovementStatus.Failed;
            Stop();
            if (request.HasValue) MovementReported?.Invoke(new MovementReport(request.Value, status, reason));
        }

        void OnDrawGizmos()
        {
            if (!IsMoving) return;
            Gizmos.color = Color.cyan;
            Vector3 prev = transform.position + Vector3.up * 0.3f;
            for (int i = index; i < path.Count; i++)
            {
                Vector3 p = path[i] + Vector3.up * 0.3f;
                Gizmos.DrawLine(prev, p);
                Gizmos.DrawSphere(p, 0.12f);
                prev = p;
            }
        }
    }
}
