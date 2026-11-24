using TacticalGame.Coordination;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame
{
    // Wires the configured guards on scene load, including scenes made by the starter builder.
    public static class PerceptionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

        static void Install()
        {
            var game = GameSession.Instance;
            if (game == null) return;

            if (game.useUtilitySquad) InstallNavigation(game);
            foreach (var target in game.targets)
            {
                if (target == null || !target.TryGetComponent<BasicGuard>(out var baseline)) continue;
                if (!target.TryGetComponent<GuardPerception>(out _)) target.gameObject.AddComponent<GuardPerception>();
                if (game.useUtilitySquad) InstallUtilityGuard(target, baseline);
            }

            var player = game.player;
            if (player != null && player.GetComponent<PlayerNoiseEmitter>() == null)
                player.gameObject.AddComponent<PlayerNoiseEmitter>();
        }

        static void InstallNavigation(GameSession game)
        {
            var pathfinder = AStarPathfinder.Instance;
            if (pathfinder == null || pathfinder.gameObject.scene != game.gameObject.scene)
            {
                var navigation = new GameObject("Squad Navigation");
                SceneManager.MoveGameObjectToScene(navigation, game.gameObject.scene);
                var grid = navigation.AddComponent<GridManager>();
                grid.drawGizmos = false;
                pathfinder = navigation.AddComponent<AStarPathfinder>();
                pathfinder.grid = grid;
            }
            if (Positioning.Instance == null || Positioning.Instance.gameObject.scene != game.gameObject.scene)
                pathfinder.gameObject.AddComponent<Positioning>();
        }

        static void InstallUtilityGuard(TargetDummy guard, BasicGuard baseline)
        {
            if (guard.TryGetComponent<AutoChase>(out var chase)) chase.enabled = false;
            if (!guard.TryGetComponent<PathMover>(out var mover)) mover = guard.gameObject.AddComponent<PathMover>();
            if (!guard.TryGetComponent<GuardLocomotion>(out _)) guard.gameObject.AddComponent<GuardLocomotion>();
            if (!guard.TryGetComponent<SquadMovementFeedback>(out var feedback)) feedback = guard.gameObject.AddComponent<SquadMovementFeedback>();
            feedback.BindNavigation(mover);
            if (!guard.TryGetComponent<EnemyWeapon>(out var weapon))
            {
                weapon = guard.gameObject.AddComponent<EnemyWeapon>();
                weapon.warningLight = baseline.warningLight;
                weapon.tracer = baseline.tracer;
                weapon.range = baseline.range;
                weapon.damage = baseline.damage;
                weapon.warningSeconds = baseline.warningSeconds;
                weapon.shotInterval = baseline.shotInterval;
            }
            if (!guard.TryGetComponent<EnemyObservationProvider>(out var observations))
                observations = guard.gameObject.AddComponent<PerceptionObservation>();
            if (!guard.TryGetComponent<UtilityEnemyController>(out var utility))
            {
                utility = guard.gameObject.AddComponent<UtilityEnemyController>();
                utility.showOverlay = false;
            }
            utility.observations = observations;
            utility.SetNavigation(mover);
        }
    }
}
