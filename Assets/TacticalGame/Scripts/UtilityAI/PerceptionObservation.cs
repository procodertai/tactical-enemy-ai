using TacticalGame.Coordination;
using UnityEngine;

namespace TacticalGame
{
    // Adapts R4's GuardPerception (view cone, occlusion, memory) to the Utility AI snapshot.
    // The live player position is used only while perception confirms the player is seen.
    public sealed class PerceptionObservation : EnemyObservationProvider
    {
        GuardPerception perception;
        TargetDummy guard;
        public GuardPerception Perception => perception;

        public override EnemyObservation Observe(float range)
        {
            var game = GameSession.Instance;
            if (perception == null) TryGetComponent(out perception); // added at runtime by PerceptionBootstrap
            if (guard == null) TryGetComponent(out guard);
            if (game == null || game.player == null || perception == null || !perception.isActiveAndEnabled || guard == null)
                return new EnemyObservation { Distance = float.PositiveInfinity };
            bool seen = perception.CanSeeTarget && perception.State == GuardPerception.AwarenessState.Alerted &&
                game.player.health.IsAlive;
            bool knows = perception.HasLastKnown && perception.State != GuardPerception.AwarenessState.Unaware;
            Vector3 point = seen ? game.player.viewCamera.transform.position
                : knows ? perception.LastKnownPosition : transform.position;
            float distance = seen || knows
                ? Vector3.Distance(transform.position + Vector3.up * 1.45f, point) : float.PositiveInfinity;
            SightingReport? observation = knows
                ? new SightingReport(guard.EnemyId, perception.LastKnownPosition, perception.LastStimulusAt) : null;
            return new EnemyObservation { AimPoint = point, Distance = distance, Visible = seen, KnownTarget = observation };
        }
    }
}
