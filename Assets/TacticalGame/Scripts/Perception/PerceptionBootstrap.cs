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
            SceneManager.sceneLoaded += (scene, mode) => Install();
            Install();
        }

        static void Install()
        {
            foreach (var guard in Object.FindObjectsByType<BasicGuard>(FindObjectsSortMode.None))
                if (guard.GetComponent<GuardPerception>() == null)
                    guard.gameObject.AddComponent<GuardPerception>();

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null && player.GetComponent<PlayerNoiseEmitter>() == null)
                player.gameObject.AddComponent<PlayerNoiseEmitter>();
        }
    }
}
