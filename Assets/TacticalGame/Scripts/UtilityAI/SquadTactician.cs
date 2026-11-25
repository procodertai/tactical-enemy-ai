using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame
{
    public sealed class SquadTactician : MonoBehaviour
    {
        public const float ThinkInterval = .25f, MemorySeconds = 8f;
        const float PointInterval = .75f;
        public SquadPlay CurrentPlay { get; private set; }
        public PlayScore[] PlayScores { get; private set; }
        public UtilityEnemyController[] Members { get; private set; } = System.Array.Empty<UtilityEnemyController>();
        public MemberOrder[] Orders { get; private set; } = System.Array.Empty<MemberOrder>();
        public bool KnowsTarget => Time.time - ContactTime <= MemorySeconds;
        public bool TargetVisible { get; private set; }
        public bool HeardContact { get; private set; }
        public Vector3 SharedTarget { get; private set; }
        public string ContactSource { get; private set; }
        public float ContactTime { get; private set; } = float.NegativeInfinity;
        public int ContactVersion { get; private set; }
        public float Held { get; private set; }
        public DemoDifficulty Difficulty { get; private set; } = DemoDifficulty.Medium;
        public IReadOnlyList<string> Log => log;

        readonly List<string> log = new List<string>();
        MemberSnapshot[] snapshots;
        SquadSignal[] signals;
        Vector3?[] failedDestinations;
        float[] failedUntil;
        float thinkAt, pointsAt, stepSince, rebuildAt;
        bool wasKnown;
        int nextMover, patrolSector;
        Vector3 approachOrigin;
        Vector3 sampledTarget;

        public void Initialize(List<UtilityEnemyController> members)
        {
            Members = members.ToArray();
            snapshots = new MemberSnapshot[Members.Length];
            signals = new SquadSignal[Members.Length];
            failedDestinations = new Vector3?[Members.Length];
            failedUntil = new float[Members.Length];
            Orders = new MemberOrder[Members.Length];
            foreach (var member in Members) approachOrigin += member.transform.position;
            if (Members.Length > 0) approachOrigin /= Members.Length;
            SetDifficulty(Difficulty);
        }

        public void SetDifficulty(DemoDifficulty difficulty)
        {
            Difficulty = difficulty;
            thinkAt = pointsAt = 0f;
            foreach (var member in Members) member.SetDemoDifficulty(difficulty);
        }

        public void RefreshGeometry()
        {
            AStarPathfinder.Instance?.grid?.Build();
            System.Array.Clear(failedUntil, 0, failedUntil.Length);
            pointsAt = 0f;
        }

        void LateUpdate()
        {
            var game = GameSession.Instance;
            if (snapshots == null || game == null || !game.CanControl || Time.deltaTime <= 0f) return;
            Held += Time.deltaTime;
            if (Time.time < thinkAt) return;
            thinkAt = Time.time + (Difficulty == DemoDifficulty.Hard ? .15f : Difficulty == DemoDifficulty.Easy ? .35f : ThinkInterval);
            Think();
        }

        void Think()
        {
            var latest = default(EnemyObservation);
            string source = null;
            bool visible = false, noise = false, blocked = false;
            for (int i = 0; i < Members.Length; i++)
            {
                var member = Members[i];
                var observation = member.Alive ? member.ReadDemoObservation() : default;
                snapshots[i].Alive = member.Alive;
                snapshots[i].Position = member.transform.position;
                snapshots[i].Health = member.Health01;
                snapshots[i].SeesPlayer = member.Alive && observation.Visible;
                snapshots[i].HasClearShot = member.Alive && member.DemoHasClearShot(observation);
                snapshots[i].CanCover = member.DemoReadyToCover;
                snapshots[i].NeedsCover = member.DemoNeedsShelter;
                snapshots[i].Ammo = member.Ammo;
                if (member.Signal != signals[i])
                {
                    signals[i] = member.Signal;
                    AddLog(member.Id + " — " + member.Signal);
                    if (member.DemoBlocked && member.DemoDestination.HasValue)
                    {
                        failedDestinations[i] = member.DemoDestination;
                        failedUntil[i] = Time.time + 4f;
                        blocked = true;
                    }
                }
                if (!observation.KnownTarget.HasValue) continue;
                if (latest.KnownTarget.HasValue && (visible && !observation.Visible ||
                    visible == observation.Visible && latest.KnownTarget.Value.ObservedAt >= observation.KnownTarget.Value.ObservedAt)) continue;
                latest = observation;
                visible = observation.Visible;
                noise = !visible && (member.observations as PerceptionObservation)?.Perception?.LastContactWasNoise == true;
                source = member.Id;
            }

            bool changedContact = false;
            if (latest.KnownTarget.HasValue && (visible || latest.KnownTarget.Value.ObservedAt >= ContactTime))
            {
                var report = latest.KnownTarget.Value;
                bool newNoise = noise && report.ObservedAt > ContactTime;
                changedContact = !wasKnown || visible && !TargetVisible || newNoise ||
                    UtilityEnemyController.FlatDistance(SharedTarget, report.ObservedPosition) > 2f;
                ContactTime = report.ObservedAt;
                SharedTarget = report.ObservedPosition;
                ContactSource = source;
                HeardContact = noise;
                if (changedContact)
                {
                    ContactVersion++;
                    AddLog(source + (visible ? " — player spotted" : noise ? " — sound reported" : " — contact relayed"));
                }
            }
            if (TargetVisible && !visible) AddLog("Sight lost — using last report");
            if (wasKnown && !KnowsTarget) AddLog("Last report expired");
            TargetVisible = visible;
            wasKnown = KnowsTarget;

            if (blocked)
            {
                if (Time.time >= rebuildAt)
                {
                    rebuildAt = Time.time + .5f;
                    AStarPathfinder.Instance?.grid?.Build();
                }
                pointsAt = 0f;
                AddLog("Route blocked — checking for a detour");
            }
            if (KnowsTarget && (Time.time >= pointsAt || changedContact ||
                UtilityEnemyController.FlatDistance(sampledTarget, SharedTarget) >= 2f))
            {
                pointsAt = Time.time + (Difficulty == DemoDifficulty.Hard ? .45f : PointInterval);
                sampledTarget = SharedTarget;
                FindPoints();
            }
            FillPatrolPoints();

            if (CurrentPlay == SquadPlay.BoundingAdvance)
            {
                for (int i = 0; i < Orders.Length; i++)
                {
                    if (Orders[i].Role != TacticRole.Mover) continue;
                    if (Members[i].DemoArrived || Members[i].DemoBlocked || Time.time - stepSince >= 3.5f)
                    {
                        nextMover = (i + 1) % Members.Length;
                        stepSince = Time.time;
                        AddLog("Next guard takes the lead");
                    }
                    else if (Orders[i].HasPoint && !changedContact)
                        snapshots[i].Step = Orders[i].Point;
                }
            }

            var snapshot = new SquadSnapshot { Members = snapshots, KnowsTarget = KnowsTarget, NextMover = nextMover, Difficulty = Difficulty };
            PlayScores = SquadTactics.Evaluate(snapshot);
            float minHold = Difficulty == DemoDifficulty.Hard ? .45f : Difficulty == DemoDifficulty.Easy ? 1.6f : SquadTactics.MinHold;
            var chosen = SquadTactics.Select(PlayScores, CurrentPlay, Held, minHold);
            if (chosen != CurrentPlay)
            {
                AddLog("PLAN — " + PlayName(chosen));
                CurrentPlay = chosen;
                Held = 0f;
                stepSince = Time.time;
            }
            var orders = SquadTactics.Assign(CurrentPlay, snapshot);
            for (int i = 0; i < Members.Length; i++)
            {
                var member = Members[i];
                UtilityEnemyController partner = null;
                int priority = 0;
                for (int j = 0; j < Members.Length; j++)
                {
                    if (i == j || !snapshots[j].Alive) continue;
                    TacticRole role = orders[j].Role;
                    int candidate = role == TacticRole.Pinner || role == TacticRole.Coverer ? 2 : role == TacticRole.Overwatch ? 1 : 0;
                    if (candidate > 0 && snapshots[j].CanCover) candidate += 2;
                    if (candidate <= priority) continue;
                    priority = candidate;
                    partner = Members[j];
                }
                if (Orders[i].Role != orders[i].Role) AddLog(member.Id + " now " + orders[i].Role);
                member.ApplyDemoOrder(orders[i], KnowsTarget ? SharedTarget : (Vector3?)null, partner);
            }
            Orders = orders;
        }

        static readonly Vector3[] PatrolSectors = {
            new Vector3(-.24f, 0f, -.28f), new Vector3(.22f, 0f, -.28f),
            new Vector3(.26f, 0f, .17f), new Vector3(-.20f, 0f, .25f)
        };

        void FillPatrolPoints()
        {
            var finder = AStarPathfinder.Instance;
            if (finder == null || CurrentPlay != SquadPlay.Freestyle && KnowsTarget) return;
            for (int i = 0; i < Orders.Length; i++)
            {
                if (CurrentPlay != SquadPlay.Freestyle || Orders[i].Role != TacticRole.Scout) continue;
                if (!Members[i].Alive || Members[i].DemoArrived || Members[i].DemoBlocked || Time.time - stepSince > 9f)
                {
                    nextMover = (i + 1) % Members.Length;
                    patrolSector++;
                    stepSince = Time.time;
                    AddLog("Scout rotates to the next sector");
                }
            }
            var grid = finder.grid;
            for (int i = 0; i < Members.Length; i++)
            {
                if (!snapshots[i].Alive) continue;
                if (CurrentPlay == SquadPlay.Freestyle && !Members[i].DemoArrived && !Members[i].DemoBlocked &&
                    Orders[i].Role == TacticRole.Scout && i == nextMover && Orders[i].HasPoint)
                {
                    snapshots[i].Patrol = Orders[i].Destination;
                    continue;
                }
                snapshots[i].Patrol = null;
                for (int offset = 0; offset < PatrolSectors.Length; offset++)
                {
                    var sector = PatrolSectors[(patrolSector + (i == nextMover ? 0 : i + 1) + offset) % PatrolSectors.Length];
                    var goal = grid.transform.position + new Vector3(sector.x * grid.gridWorldSize.x, 0f, sector.z * grid.gridWorldSize.y);
                    var route = finder.FindPath(snapshots[i].Position, goal);
                    if (route == null || route.Count == 0) continue;
                    var end = route[route.Count - 1];
                    if (UtilityEnemyController.FlatDistance(snapshots[i].Position, end) < 2f ||
                        Time.time < failedUntil[i] && IsFailedGoal(failedDestinations[i], end)) continue;
                    snapshots[i].Patrol = end;
                    break;
                }
            }
        }

        void FindPoints()
        {
            var positioning = Positioning.Instance;
            var finder = AStarPathfinder.Instance;
            if (positioning == null || finder == null) return;
            Vector3 forward = SharedTarget - approachOrigin;
            forward.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, forward.sqrMagnitude > .1f ? forward.normalized : Vector3.forward);
            for (int i = 0; i < Members.Length; i++)
            {
                ref var member = ref snapshots[i];
                if (!member.Alive) continue;
                var guard = Members[i];
                Vector3 from = member.Position;
                Vector3? failed = Time.time < failedUntil[i] ? failedDestinations[i] : null;
                bool Allowed(TacticalMarker marker) => !IsFailedGoal(failed, marker.Position) &&
                    (marker.kind != MarkerKind.Flank || UtilityEnemyController.FlatDistance(from, marker.Position) > 1.1f ||
                        guard.Role == TacticRole.Flanker && guard.DemoArrived && snapshots[i].HasClearShot);
                var flank = positioning.FindFlank(from, SharedTarget, out _, Allowed);
                var cover = positioning.FindCover(from, SharedTarget, out float coverCost, Allowed);
                member.Flank = flank == null ? null : Endpoint(finder, from, flank.Position);
                if (!member.Flank.HasValue)
                    member.Flank = FindAngle(finder, positioning, from, SharedTarget, failed, 11f);
                Vector3? previousShelter = Orders[i].Role == TacticRole.Shelter ? Orders[i].Destination : null;
                Vector3? markedShelter = cover != null ? Endpoint(finder, from, cover.Position) : null;
                member.Cover = previousShelter.HasValue && !IsFailedGoal(failed, previousShelter.Value) &&
                    !ClearAngle(positioning, previousShelter.Value, SharedTarget) ? previousShelter :
                    coverCost <= 7f && markedShelter.HasValue ? markedShelter : FindShelter(finder, positioning, from, SharedTarget, failed) ?? markedShelter;
                member.Peek = member.Cover.HasValue
                    ? FindAngle(finder, positioning, member.Cover.Value, SharedTarget, failed, 5f) : null;
                member.LeftSearch = SearchPoint(finder, failed, from, SharedTarget - side * 4f, -side);
                member.RightSearch = SearchPoint(finder, failed, from, SharedTarget + side * 4f, side);
                var approach = finder.FindPath(from, SharedTarget);
                member.Step = null;
                if (approach == null) continue;
                float distance = 0f;
                Vector3 previous = from;
                foreach (var point in approach)
                {
                    distance += UtilityEnemyController.FlatDistance(previous, point);
                    previous = point;
                    if (distance > (Difficulty == DemoDifficulty.Hard ? 6.3f : 4.6f)) break;
                    if (distance >= 1f && !IsFailedGoal(failed, point)) member.Step = point;
                }
            }
        }

        static bool ClearAngle(Positioning positioning, Vector3 point, Vector3 target)
        {
            Vector3 lift = Vector3.up * positioning.eyeHeight;
            return positioning.HasLineOfSight(point + lift, target + lift);
        }

        static readonly float[] AngleRadii = { 2.3f, 4.3f, 7f };

        static Vector3? FindAngle(AStarPathfinder finder, Positioning positioning, Vector3 from, Vector3 target,
            Vector3? failed, float maximumRoute)
        {
            Vector3? best = null;
            float bestCost = maximumRoute;
            foreach (float radius in AngleRadii)
            {
                if (radius > maximumRoute) break;
                for (int direction = 0; direction < 8; direction++)
                {
                    Vector3 test = from + Quaternion.Euler(0f, direction * 45f, 0f) * Vector3.forward * radius;
                    Vector3? point = Snap(finder, test);
                    if (!point.HasValue || IsFailedGoal(failed, point.Value) ||
                        UtilityEnemyController.FlatDistance(from, point.Value) < 1.4f ||
                        UtilityEnemyController.FlatDistance(point.Value, target) > 17f ||
                        UtilityEnemyController.FlatDistance(point.Value, target) < 3f ||
                        !ClearAngle(positioning, point.Value, target)) continue;
                    var path = finder.FindPath(from, point.Value);
                    if (path == null || path.Count == 0) continue;
                    float cost = 0f;
                    Vector3 previous = from;
                    foreach (var step in path)
                    {
                        cost += UtilityEnemyController.FlatDistance(previous, step);
                        previous = step;
                    }
                    if (cost >= bestCost) continue;
                    best = point;
                    bestCost = cost;
                }
                if (best.HasValue) break;
            }
            return best;
        }

        static Vector3? FindShelter(AStarPathfinder finder, Positioning positioning, Vector3 from, Vector3 target, Vector3? failed)
        {
            if (!ClearAngle(positioning, from, target) && !IsFailedGoal(failed, from)) return from;
            for (int direction = 0; direction < 8; direction++)
            {
                var test = from + Quaternion.Euler(0f, direction * 45f, 0f) * Vector3.forward * 4f;
                var point = Snap(finder, test);
                if (point.HasValue && !IsFailedGoal(failed, point.Value) && !ClearAngle(positioning, point.Value, target) &&
                    finder.FindPath(from, point.Value) != null) return point;
            }
            return null;
        }

        static Vector3? Snap(AStarPathfinder finder, Vector3 point)
        {
            var grid = finder.grid;
            var node = grid.NearestWalkable(grid.NodeFromWorldPoint(point), 1);
            return node != null && UtilityEnemyController.FlatDistance(node.worldPos, point) <= 1.3f ? node.worldPos : (Vector3?)null;
        }

        static bool IsFailedGoal(Vector3? failed, Vector3 destination) =>
            failed.HasValue && UtilityEnemyController.FlatDistance(failed.Value, destination) < 1.2f;

        static Vector3? SearchPoint(AStarPathfinder finder, Vector3? failed, Vector3 from, Vector3 center, Vector3 side)
        {
            for (int i = 0; i < 3; i++)
            {
                var point = Endpoint(finder, from, center + side * i);
                if (point.HasValue && !IsFailedGoal(failed, point.Value)) return point;
            }
            return null;
        }

        static Vector3? Endpoint(AStarPathfinder finder, Vector3 from, Vector3 goal)
        {
            var path = finder.FindPath(from, goal);
            if (path == null) return null;
            Vector3 end = path.Count == 0 ? from : path[path.Count - 1];
            return UtilityEnemyController.FlatDistance(end, goal) <= 1.2f ? end : (Vector3?)null;
        }

        public static string PlayName(SquadPlay play)
        {
            switch (play)
            {
                case SquadPlay.PinAndFlank: return "Pin and flank";
                case SquadPlay.BoundingAdvance: return "Advance in turns";
                case SquadPlay.CoveredRetreat: return "Cover the retreat";
                case SquadPlay.SplitSearch: return "Search both sides";
                case SquadPlay.Contain: return "Change firing angles";
                default: return "Scout and cover";
            }
        }

        void AddLog(string message)
        {
            log.Add($"{Time.timeSinceLevelLoad:F1}s  {message}");
            if (log.Count > 12) log.RemoveAt(0);
        }
    }
}
