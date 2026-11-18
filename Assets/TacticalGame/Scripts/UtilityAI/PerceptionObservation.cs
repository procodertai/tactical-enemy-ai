using UnityEngine;

namespace TacticalGame
{
    // Adapts R4's GuardPerception (view cone, occlusion, memory) to the Utility AI snapshot.
    // The live player position is used only while perception confirms the player is seen.
    public sealed class PerceptionObservation : EnemyObservationProvider
    {
        GuardPerception perception;
        public GuardPerception Perception => perception;

        public override EnemyObservation Observe(float range)
        {
            var game = GameSession.Instance;
            if (perception == null) TryGetComponent(out perception); // added at runtime by PerceptionBootstrap
            if (game == null || game.player == null || perception == null)
                return new EnemyObservation { Distance = float.PositiveInfinity };
            bool seen = perception.CanSeeTarget && perception.State == GuardPerception.AwarenessState.Alerted &&
                game.player.health.IsAlive;
            Vector3 point = seen ? game.player.viewCamera.transform.position
                : perception.HasLastKnown ? perception.LastKnownPosition : transform.position;
            float distance = seen || perception.HasLastKnown
                ? Vector3.Distance(transform.position + Vector3.up * 1.45f, point) : float.PositiveInfinity;
            return new EnemyObservation { AimPoint = point, Distance = distance, Visible = seen && distance <= range };
        }
    }
}
