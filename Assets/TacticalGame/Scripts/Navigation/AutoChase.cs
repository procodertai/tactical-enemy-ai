using UnityEngine;

namespace TacticalGame
{
    // Temporary A* chase based on local observations or the squad's latest valid sighting.
    [RequireComponent(typeof(PathMover))]
    public sealed class AutoChase : MonoBehaviour
    {
        public float attackDistance = 12f;   // stop and shoot when this close with a clear line
        public float minDistance = 3f;       // never walk closer than this
        public float repathSeconds = 0.5f;   // how often the route to the player is recomputed

        PathMover mover;
        TargetDummy guard;
        GuardPerception perception;
        float nextRepath;

        void Awake()
        {
            mover = GetComponent<PathMover>();
            guard = GetComponent<TargetDummy>();
            nextRepath = Random.value * repathSeconds;   // stagger so 3 guards don't all search on one frame
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (game == null || game.State != RoundState.Playing) return;
            if (guard == null || !guard.IsAlive) return;
            if (perception == null) TryGetComponent(out perception);
            if (!TryGetDestination(out var destination))
            {
                mover.Stop();
                return;
            }

            Vector3 flat = destination - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;

            bool confirmedTarget = perception != null && perception.CanSeeTarget &&
                perception.State == GuardPerception.AwarenessState.Alerted;

            // Close enough with a clear line, or simply too close: stand still and let BasicGuard shoot.
            if (distance < minDistance || (distance <= attackDistance && confirmedTarget))
            {
                mover.Stop();
                return;
            }

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathSeconds;
                mover.MoveTo(destination);
            }
        }

        bool TryGetDestination(out Vector3 destination)
        {
            if (perception != null && perception.isActiveAndEnabled && perception.HasLastKnown &&
                perception.State != GuardPerception.AwarenessState.Unaware)
            {
                destination = perception.LastKnownPosition;
                return true;
            }
            var squad = guard.SquadCoordinator;
            if (squad != null && squad.TryGetSharedSighting(out var sighting))
            {
                destination = sighting.Report.ObservedPosition;
                return true;
            }
            destination = default;
            return false;
        }
    }
}
