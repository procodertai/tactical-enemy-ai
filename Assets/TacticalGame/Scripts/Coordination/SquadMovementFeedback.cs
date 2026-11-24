using UnityEngine;

namespace TacticalGame.Coordination
{
    // Optional hook for teammate 2's navigator. It forwards results; it never chooses an action/path.
    [RequireComponent(typeof(TargetDummy)), DisallowMultipleComponent]
    public sealed class SquadMovementFeedback : MonoBehaviour
    {
        [SerializeField, Tooltip("Assign the teammate's component implementing IEnemyNavigation when available.")]
        MonoBehaviour navigationSource = null;
        IEnemyNavigation subscribed;
        TargetDummy member;
        public bool NavigationConnected => subscribed != null;

        void OnEnable()
        {
            member = GetComponent<TargetDummy>();
            BindNavigation(navigationSource as IEnemyNavigation ?? GetComponent<PathMover>());
        }

        public void BindNavigation(IEnemyNavigation navigation)
        {
            if (subscribed != null) subscribed.MovementReported -= OnMovementReported;
            subscribed = navigation;
            if (subscribed != null && isActiveAndEnabled) subscribed.MovementReported += OnMovementReported;
        }

        void OnMovementReported(MovementReport report)
        {
            if (member != null && report.Request.EnemyId == member.EnemyId && member.SquadCoordinator != null)
                member.SquadCoordinator.HandleMovementReport(report);
        }

        void OnDisable()
        {
            if (subscribed != null) subscribed.MovementReported -= OnMovementReported;
            subscribed = null;
        }
    }
}
