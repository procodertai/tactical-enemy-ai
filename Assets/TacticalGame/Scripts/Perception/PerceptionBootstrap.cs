using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame
{
    // Adds GuardPerception to every BasicGuard and PlayerNoiseEmitter to the player when a scene
    // loads, so no scene or prefab needs editing. Later this can be replaced by adding the
    // components to the prefabs directly.
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

            foreach (var target in game.targets)
                if (target != null && target.TryGetComponent<BasicGuard>(out _) && !target.TryGetComponent<GuardPerception>(out _))
                    target.gameObject.AddComponent<GuardPerception>();

            var player = game.player;
            if (player != null && player.GetComponent<PlayerNoiseEmitter>() == null)
                player.gameObject.AddComponent<PlayerNoiseEmitter>();
        }
    }
}
