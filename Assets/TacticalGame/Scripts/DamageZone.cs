using UnityEngine;

namespace TacticalGame
{
    // A visible, fixed electrical hazard for testing health. No enemy sensing or AI.
    public sealed class DamageZone : MonoBehaviour
    {
        public Vector3 size = new Vector3(3f, 3f, 3f);
        public float damage = 20f;
        public float interval = .8f;
        float nextTick;

        void Update()
        {
            var game = GameSession.Instance;
            if (game == null || !game.CanControl || Time.time < nextTick) return;
            if (!new Bounds(transform.position + Vector3.up, size).Contains(game.player.transform.position + Vector3.up)) return;
            nextTick = Time.time + interval;
            game.player.health.TakeDamage(damage);
            game.Notify("LIVE ELECTRICAL TEST / Leave the red floor zone.");
            GameAudio.Instance.PlayCue(85f, .16f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .2f, .1f, .35f);
            Gizmos.DrawWireCube(transform.position + Vector3.up, size);
        }
    }
}
