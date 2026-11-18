using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame
{
    // Moves this object along an A* path. Other scripts only call MoveTo / Stop.
    public sealed class PathMover : MonoBehaviour
    {
        public static readonly List<PathMover> All = new List<PathMover>();

        public float speed = 3.5f;
        public float turnSpeed = 8f;
        public float arriveDistance = 0.15f;

        public bool IsMoving => path != null && index < path.Count;
        public bool PathFailed { get; private set; }

        List<Vector3> path;
        int index;
        Health health;

        void OnEnable()  { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void Awake()     { health = GetComponent<Health>(); }

        public bool MoveTo(Vector3 destination)
        {
            var pf = AStarPathfinder.Instance;
            path = pf != null ? pf.FindPath(transform.position, destination) : null;
            index = 0;
            PathFailed = path == null;
            if (PathFailed) Debug.LogWarning(name + ": no path to " + destination);
            return !PathFailed;
        }

        public void Stop() { path = null; }

        void Update()
        {
            var game = GameSession.Instance;
            if (game != null && game.State != RoundState.Playing) return;
            if (health != null && !health.IsAlive) { path = null; return; }
            if (!IsMoving) return;

            Vector3 flat = path[index] - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= arriveDistance) { index++; return; }

            Vector3 step = flat.normalized * speed * Time.deltaTime;
            if (step.magnitude > flat.magnitude) step = flat;
            transform.position += step;   // guards have a plain collider, no CharacterController
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
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