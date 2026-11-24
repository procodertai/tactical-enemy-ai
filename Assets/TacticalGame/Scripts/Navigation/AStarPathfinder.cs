using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame
{
    public sealed class AStarPathfinder : MonoBehaviour
    {
        public static AStarPathfinder Instance { get; private set; }
        public GridManager grid;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos) => FindPath(startPos, targetPos, null);

        internal List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, Vector3? occupied)
        {
            if (grid == null || !isActiveAndEnabled) return null;
            Node start = grid.NearestWalkable(grid.NodeFromWorldPoint(startPos));
            Node goal  = grid.NearestWalkable(grid.NodeFromWorldPoint(targetPos));
            if (start == null || goal == null) return null;

            var open = new List<Node> { start };
            var closed = new HashSet<Node>();
            start.gCost = 0;
            start.hCost = Heuristic(start, goal);
            start.parent = null;

            while (open.Count > 0)
            {
                Node current = open[0];
                for (int i = 1; i < open.Count; i++)
                    if (open[i].FCost < current.FCost ||
                       (open[i].FCost == current.FCost && open[i].hCost < current.hCost))
                        current = open[i];

                open.Remove(current);
                closed.Add(current);

                if (current == goal) return Retrace(start, goal);

                foreach (Node nb in grid.GetNeighbours(current))
                {
                    if (closed.Contains(nb)) continue;
                    if (occupied.HasValue && CrossesOccupiedSpace(current == start ? startPos : current.worldPos, nb.worldPos, occupied.Value))
                        continue;
                    int newG = current.gCost + Heuristic(current, nb);   // 10 straight, 14 diagonal
                    if (newG < nb.gCost || !open.Contains(nb))
                    {
                        nb.gCost = newG;
                        nb.hCost = Heuristic(nb, goal);
                        nb.parent = current;
                        if (!open.Contains(nb)) open.Add(nb);
                    }
                }
            }
            return null;   // unreachable
        }

        static bool CrossesOccupiedSpace(Vector3 from, Vector3 to, Vector3 occupied)
        {
            from.y = to.y = occupied.y = 0f;
            Vector3 offset = to - from;
            float progress = offset.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(occupied - from, offset) / offset.sqrMagnitude) : 0f;
            const float clearance = PathMover.GuardSeparation;
            float minimumDistance = Mathf.Min((occupied - from).sqrMagnitude, clearance * clearance);
            return (occupied - (from + offset * progress)).sqrMagnitude < minimumDistance - .0001f ||
                (occupied - to).sqrMagnitude < clearance * clearance;
        }

        // Octile distance: 10 per straight step, 14 per diagonal step
        int Heuristic(Node a, Node b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return 10 * Mathf.Max(dx, dy) + 4 * Mathf.Min(dx, dy);
        }

        List<Vector3> Retrace(Node start, Node end)
        {
            var path = new List<Vector3>();
            for (Node n = end; n != start; n = n.parent) path.Add(n.worldPos);
            path.Reverse();
            return path;
        }
    }
}
