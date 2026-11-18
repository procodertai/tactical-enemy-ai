using UnityEngine;

namespace TacticalGame
{
    public struct EnemyObservation
    {
        public bool Visible;
        public Vector3 AimPoint;
        public float Distance;
    }

    // Teammates can replace this component with a perception-backed subclass.
    public abstract class EnemyObservationProvider : MonoBehaviour
    {
        public abstract EnemyObservation Observe(float range);
    }
}
