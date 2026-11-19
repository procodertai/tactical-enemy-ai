using System;
using System.Collections.Generic;
using TacticalGame.Coordination;
using UnityEngine;

namespace TacticalGame
{
    public sealed class Positioning : MonoBehaviour, IPositioningProvider
    {
        public static Positioning Instance { get; private set; }
        public float eyeHeight = 1.45f;     // same height BasicGuard shoots from
        public float markerHeight = 1.0f;   // chest height of a unit standing on a marker

        TacticalMarker[] markers = new TacticalMarker[0];

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
#if UNITY_6000_6_OR_NEWER
            markers = FindObjectsByType<TacticalMarker>();
#elif UNITY_2023_1_OR_NEWER
            markers = FindObjectsByType<TacticalMarker>(FindObjectsSortMode.None);
#else
            markers = FindObjectsOfType<TacticalMarker>();
#endif
        }

        public IReadOnlyList<ReachableDestination> GetReachableCandidates(string enemyId, MarkerKind kind, SightingReport? knownThreat)
        {
            var game = GameSession.Instance;
            var squad = game != null ? game.squadCoordinator : null;
            var pathfinder = AStarPathfinder.Instance;
            if (squad == null || pathfinder == null || !squad.TryGetEnemy(enemyId, out var enemy) || !enemy.IsAlive)
                return Array.Empty<ReachableDestination>();

            var ranked = new List<(ReachableDestination Destination, float Cost)>();
            foreach (var marker in squad.Destinations)
            {
                if (marker.Kind != kind || !Suitable(marker, enemy.Position, knownThreat)) continue;
                var path = pathfinder.FindPath(enemy.Position, marker.Position);
                if (path == null || !squad.IsAtDestination(PathMover.EndPosition(enemy.Position, path), marker.Position)) continue;
                ranked.Add((new ReachableDestination(marker.MarkerId, marker.Position), PathLength(enemy.Position, path)));
            }
            ranked.Sort((a, b) => a.Cost == b.Cost ? a.Destination.MarkerId.CompareTo(b.Destination.MarkerId) : a.Cost.CompareTo(b.Cost));
            var candidates = new ReachableDestination[ranked.Count];
            for (int i = 0; i < ranked.Count; i++) candidates[i] = ranked[i].Destination;
            return candidates;
        }

        bool Suitable(ITacticalDestination marker, Vector3 from, SightingReport? knownThreat)
        {
            if (!knownThreat.HasValue) return true;
            Vector3 threat = knownThreat.Value.ObservedPosition;
            if (marker.Kind == MarkerKind.Fallback)
                return (marker.Position - threat).sqrMagnitude > (from - threat).sqrMagnitude;
            bool inSight = HasLineOfSight(threat + Vector3.up * eyeHeight, marker.Position + Vector3.up * markerHeight);
            return marker.Kind == MarkerKind.Flank ? inSight : !inSight;
        }

        // Walls block sight. Units (anything with Health) do not.
        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            var found = Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in found)
                if (h.collider.GetComponentInParent<Health>() == null) return false;
            return true;
        }

        // Nearest reachable cover marker that the threat cannot see.
        public TacticalMarker FindCover(Vector3 from, Vector3 threat, out float pathCost, Predicate<TacticalMarker> allowed = null)
        {
            Vector3 eye = threat + Vector3.up * eyeHeight;
            return Pick(MarkerKind.Cover, from, allowed, out pathCost,
                m => !HasLineOfSight(eye, m.transform.position + Vector3.up * markerHeight));
        }

        // Nearest reachable flank marker from which the target can be seen (and shot).
        public TacticalMarker FindFlank(Vector3 from, Vector3 target, out float pathCost, Predicate<TacticalMarker> allowed = null)
        {
            Vector3 eye = target + Vector3.up * eyeHeight;
            return Pick(MarkerKind.Flank, from, allowed, out pathCost,
                m => HasLineOfSight(eye, m.transform.position + Vector3.up * markerHeight));
        }

        TacticalMarker Pick(MarkerKind kind, Vector3 from, Predicate<TacticalMarker> allowed,
                            out float bestCost, Func<TacticalMarker, bool> tacticalOk)
        {
            TacticalMarker best = null;
            bestCost = float.MaxValue;
            var pf = AStarPathfinder.Instance;
            if (pf == null) return null;
            foreach (var m in markers)
            {
                if (m.kind != kind) continue;
                if (allowed != null && !allowed(m)) continue;
                if (!tacticalOk(m)) continue;                        // cheap check first
                var path = pf.FindPath(from, m.transform.position);
                if (path == null) continue;                          // unreachable: filtered out
                float cost = PathLength(from, path);
                if (cost < bestCost) { bestCost = cost; best = m; }
            }
            return best;
        }

        static float PathLength(Vector3 start, List<Vector3> path)
        {
            float total = 0f;
            Vector3 prev = start;
            foreach (var p in path) { total += Vector3.Distance(prev, p); prev = p; }
            return total;
        }
    }
}
