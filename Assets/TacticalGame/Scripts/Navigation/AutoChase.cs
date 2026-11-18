using UnityEngine;

namespace TacticalGame
{
    // TEMPORARY chase behaviour: the guard hunts the player along A* paths by itself,
    // stops when it has a clear shot, and BasicGuard does the shooting.
    [RequireComponent(typeof(PathMover))]
    public sealed class AutoChase : MonoBehaviour
    {
        public float attackDistance = 12f;   // stop and shoot when this close with a clear line
        public float minDistance = 3f;       // never walk closer than this
        public float repathSeconds = 0.5f;   // how often the route to the player is recomputed

        PathMover mover;
        Health health;
        float nextRepath;

        void Awake()
        {
            mover = GetComponent<PathMover>();
            health = GetComponent<Health>();
            nextRepath = Random.value * repathSeconds;   // stagger so 3 guards don't all search on one frame
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (game == null || game.State != RoundState.Playing) return;
            if (health != null && !health.IsAlive) return;

            Vector3 playerPos = game.player.transform.position;
            Vector3 flat = playerPos - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;

            bool clearShot = false;
            var pos = Positioning.Instance;
            if (pos != null)
                clearShot = pos.HasLineOfSight(game.player.viewCamera.transform.position,
                                               transform.position + Vector3.up * pos.markerHeight);

            // Close enough with a clear line, or simply too close: stand still and let BasicGuard shoot.
            if (distance < minDistance || (distance <= attackDistance && clearShot))
            {
                mover.Stop();
                return;
            }

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathSeconds;
                mover.MoveTo(playerPos);   // A* around walls to where the player is now
            }
        }
    }
}