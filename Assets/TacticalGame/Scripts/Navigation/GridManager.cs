using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame
{
    public sealed class GridManager : MonoBehaviour
    {
        public Vector2 gridWorldSize = new Vector2(42, 40);
        public float nodeRadius = 0.5f;
        [Tooltip("Free cell centres stay at least this far from solid obstacles.")]
        public float clearance = 0.6f;
        [Tooltip("Obstacles lower than this are walkable (cables, floor plates).")]
        public float stepHeight = 0.35f;
        public float bodyHeight = 1.8f;
        public bool drawGizmos = true;

        Node[,] grid;
        float nodeDiameter;
        readonly Collider[] hits = new Collider[16];
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }

        void Awake() { Build(); }

        [ContextMenu("Rebuild Grid")]
        public void Build()
        {
            Physics.SyncTransforms();
            nodeDiameter = nodeRadius * 2f;
            SizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
            SizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
            grid = new Node[SizeX, SizeY];

            Vector3 bottomLeft = transform.position
                - Vector3.right * gridWorldSize.x / 2f
                - Vector3.forward * gridWorldSize.y / 2f;

            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                {
                    Vector3 p = bottomLeft
                        + Vector3.right * (x * nodeDiameter + nodeRadius)
                        + Vector3.forward * (y * nodeDiameter + nodeRadius);
                    grid[x, y] = new Node(!Blocked(p), p, x, y);
                }
        }

        // A capsule from stepHeight up to bodyHeight: low clutter passes underneath it.
        bool Blocked(Vector3 p)
        {
            Vector3 bottom = p + Vector3.up * (stepHeight + clearance);
            Vector3 top = p + Vector3.up * Mathf.Max(bodyHeight - clearance, stepHeight + clearance);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, clearance, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].GetComponentInParent<Health>() == null) return true;   // units are not obstacles
            return false;
        }

        // A random walkable point between minRadius and maxRadius from center (used for patrol and search).
        public bool TryRandomWalkable(Vector3 center, float minRadius, float maxRadius, out Vector3 point)
        {
            for (int i = 0; i < 40; i++)
            {
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(minRadius, maxRadius);
                Vector3 p = center + new Vector3(c.x, 0f, c.y);
                Node n = NodeFromWorldPoint(p);
                if (n.walkable) { point = n.worldPos; return true; }
            }
            point = center;
            return false;
        }

        public Node NodeFromWorldPoint(Vector3 pos)
        {
            float px = Mathf.Clamp01((pos.x - transform.position.x + gridWorldSize.x / 2f) / gridWorldSize.x);
            float py = Mathf.Clamp01((pos.z - transform.position.z + gridWorldSize.y / 2f) / gridWorldSize.y);
            int x = Mathf.Clamp(Mathf.FloorToInt(px * SizeX), 0, SizeX - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(py * SizeY), 0, SizeY - 1);
            return grid[x, y];
        }

        // If a unit or marker sits inside the inflated zone, snap to the closest free cell.
        public Node NearestWalkable(Node n, int maxRings = 4)
        {
            if (n.walkable) return n;
            for (int r = 1; r <= maxRings; r++)
            {
                Node best = null; float bestD = float.MaxValue;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int nx = n.x + dx, ny = n.y + dy;
                        if (nx < 0 || ny < 0 || nx >= SizeX || ny >= SizeY) continue;
                        if (!grid[nx, ny].walkable) continue;
                        float d = dx * dx + dy * dy;
                        if (d < bestD) { bestD = d; best = grid[nx, ny]; }
                    }
                if (best != null) return best;
            }
            return null;
        }

        public List<Node> GetNeighbours(Node n)
        {
            var result = new List<Node>(8);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = n.x + dx, ny = n.y + dy;
                    if (nx < 0 || ny < 0 || nx >= SizeX || ny >= SizeY) continue;
                    if (!grid[nx, ny].walkable) continue;
                    if (dx != 0 && dy != 0 &&
                        (!grid[n.x + dx, n.y].walkable || !grid[n.x, n.y + dy].walkable))
                        continue;   // no corner cutting
                    result.Add(grid[nx, ny]);
                }
            return result;
        }

        void OnDrawGizmos()
        {
            Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 0.1f, gridWorldSize.y));
            if (!drawGizmos || grid == null) return;
            foreach (Node n in grid)
            {
                Gizmos.color = n.walkable ? new Color(1, 1, 1, 0.15f) : new Color(1, 0, 0, 0.5f);
                Gizmos.DrawCube(n.worldPos + Vector3.up * 0.05f, new Vector3(nodeDiameter - 0.1f, 0.05f, nodeDiameter - 0.1f));
            }
        }
    }
}