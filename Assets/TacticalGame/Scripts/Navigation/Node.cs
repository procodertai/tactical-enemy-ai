using UnityEngine;

namespace TacticalGame
{
    public class Node
    {
        public bool walkable;
        public Vector3 worldPos;
        public int x, y;
        public int gCost, hCost;   // g = cost from start, h = estimate to goal
        public Node parent;
        public int FCost => gCost + hCost;

        public Node(bool walkable, Vector3 worldPos, int x, int y)
        {
            this.walkable = walkable;
            this.worldPos = worldPos;
            this.x = x;
            this.y = y;
        }
    }
}