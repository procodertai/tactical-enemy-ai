using UnityEngine;

namespace TacticalGame
{
    public enum MarkerKind { Cover, Flank, Fallback, EnemySpawn }

    // Data for future lessons. No route search or destination selection is implemented.
    public sealed class TacticalMarker : MonoBehaviour
    {
        public MarkerKind kind;
        public int id;

        void OnDrawGizmos()
        {
            Gizmos.color = kind == MarkerKind.Cover ? Color.cyan : kind == MarkerKind.Flank ? Color.yellow : Color.magenta;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .25f, .3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
