using UnityEngine;

namespace TacticalGame
{
    public enum CameraMode { Map, Follow, Intruder }

    // One runtime camera for the presenter. No AudioListener: the player's camera already has one.
    public sealed class SpectatorCamera : MonoBehaviour
    {
        const float MapPitch = 60f, Fov = 50f;
        Camera cam;
        Transform follow;
        Transform mapFocus;
        CameraMode mode = CameraMode.Map;
        Vector3 velocity;
        float mapZoom = 1f;
        static SpectatorCamera instance;

        // The spectator camera while it renders, else null.
        public static Camera Active => instance != null && instance.cam != null && instance.cam.enabled ? instance.cam : null;
        public Camera MapCamera => mode == CameraMode.Map ? cam : null;

        // Camera that is really on screen: spectator if active, else the current game view.
        public static Camera Resolve(GameSession game)
        {
            var a = Active;
            if (a != null) return a;
            if (game == null || game.player == null) return null;
            return game.ActiveCamera;
        }

        void OnDestroy() { if (instance == this) instance = null; }

        public static SpectatorCamera Create(Camera playerCamera)
        {
            var go = new GameObject("Spectator Camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = Fov;
            cam.depth = (playerCamera != null ? playerCamera.depth : 0f) + 10f; // draws over the player camera
            cam.nearClipPlane = .3f; cam.farClipPlane = 300f;
            var spectator = go.AddComponent<SpectatorCamera>();
            spectator.cam = cam; instance = spectator;
            spectator.Snap();
            return spectator;
        }

        public void SetMode(CameraMode newMode, Transform target, Transform player)
        {
            mode = newMode; follow = target;
            mapFocus = player;
            cam.enabled = mode != CameraMode.Intruder; // intruder mode lets the player's own camera render
            if (cam.enabled)
            {
                velocity = Vector3.zero;
                Snap();
            }
        }

        public void ZoomMap(float amount)
        {
            if (mode == CameraMode.Map)
                mapZoom = Mathf.Clamp(mapZoom * Mathf.Pow(.87f, amount), .32f, 1.2f);
        }

        void Snap() { var pose = Wanted(out var rot); transform.SetPositionAndRotation(pose, rot); }

        Vector3 Wanted(out Quaternion rotation)
        {
            if (mode == CameraMode.Follow && follow != null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(follow.forward, Vector3.up).normalized;
                if (flat.sqrMagnitude < .01f) flat = Vector3.forward;
                Vector3 pos = follow.position - flat * 7f + Vector3.up * 5f;
                rotation = Quaternion.LookRotation(follow.position + Vector3.up * 1.5f - pos);
                return pos;
            }
            // Map: back away from the grid centre until the whole grid fits in view.
            var pf = AStarPathfinder.Instance;
            var grid = pf != null ? pf.grid : null;
            Vector3 centre = grid != null ? grid.transform.position : Vector3.zero;
            float size = grid != null ? Mathf.Max(grid.gridWorldSize.x, grid.gridWorldSize.y) : 40f;
            if (mapFocus != null)
            {
                Vector3 focus = mapFocus.position;
                focus.y = centre.y;
                centre = Vector3.Lerp(centre, focus, Mathf.InverseLerp(1f, .45f, mapZoom));
            }
            rotation = Quaternion.Euler(MapPitch, 0f, 0f);
            float distance = size * .62f * mapZoom / Mathf.Tan(Fov * .5f * Mathf.Deg2Rad);
            return centre - rotation * Vector3.forward * distance;
        }

        void LateUpdate()
        {
            if (!cam.enabled) return;
            float dt = Time.unscaledDeltaTime; // keeps moving while the demo is frozen
            Vector3 pos = Wanted(out var rot);
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref velocity, .35f, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-6f * dt));
        }
    }
}
