using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame
{
    // Presenter-only demo: converts guards to Utility AI, drives a scripted intruder, offers cameras and hotkeys.
    public sealed class SquadDemoDirector : MonoBehaviour
    {
        const string DifficultyPreference = "SquadDemoDifficulty";
        readonly List<UtilityEnemyController> guards = new List<UtilityEnemyController>();
        SpectatorCamera spectator;
        GameHud hud;
        GameObject routeBarrier;
        GameObject playerBody;
        string barrierStatus;
        float barrierStatusUntil;
        bool started, frozen, slow, manual;
        int selected;
        GUIStyle keys, status, badge;
        float scale;

        public IReadOnlyList<UtilityEnemyController> Guards => guards;
        public UtilityEnemyController SelectedGuard => selected >= 0 && selected < guards.Count ? guards[selected] : null;
        public CameraMode CameraMode { get; private set; } = CameraMode.Map;
        public ScriptedIntruder Intruder { get; private set; }
        public SquadTactician Tactician { get; private set; }

        void Update()
        {
            var game = GameSession.Instance;
            if (game == null) return;
            game.DemoOwnsOverview = true;
            if (hud == null)
            {
#if UNITY_2022_2_OR_NEWER
                hud = FindFirstObjectByType<GameHud>();
#else
                hud = FindObjectOfType<GameHud>();
#endif
            }
            if (started && hud != null) hud.enabled = game.State != RoundState.Playing; // pause/win/lose menus still need it
            if (!started)
            {
                if (game.State == RoundState.Playing) StartDemo(game);
                return;
            }
            if (game.State != RoundState.Playing) return;
            HandleKeys();
            UpdatePointer();
            float wanted = frozen ? 0f : slow ? .4f : 1f;
            if (!Mathf.Approximately(Time.timeScale, wanted)) Time.timeScale = wanted;
        }

        void StartDemo(GameSession game)
        {
            started = true;
            var found = new List<TargetDummy>();
#if UNITY_2023_1_OR_NEWER
            var dummies = FindObjectsByType<TargetDummy>(FindObjectsSortMode.None);
#else
            var dummies = FindObjectsOfType<TargetDummy>();
#endif
            foreach (var d in dummies) if (d.GetComponent<BasicGuard>() != null || d.GetComponent<UtilityEnemyController>() != null) found.Add(d);
            found.Sort((a, b) => string.CompareOrdinal(a.targetId, b.targetId));
            if (game.squadCoordinator != null) game.squadCoordinator.SetCoordinationEnabled(false);
            foreach (var d in found) guards.Add(Convert(d));
#if UNITY_2022_2_OR_NEWER
            var tactician = FindFirstObjectByType<SquadTactician>();
#else
            var tactician = FindObjectOfType<SquadTactician>();
#endif
            if (tactician == null) tactician = new GameObject("Squad Tactician").AddComponent<SquadTactician>();
            tactician.Initialize(guards);
            tactician.SetDifficulty((DemoDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyPreference, (int)DemoDifficulty.Medium), 0, 2));
            Tactician = tactician;
            var overlay = GetComponent<SquadTacticsOverlay>();
            if (overlay == null) overlay = gameObject.AddComponent<SquadTacticsOverlay>();
            overlay.tactician = tactician;

            var player = game.player;
            if (player != null)
            {
                player.enabled = false;
                if (player.rifle != null) player.rifle.enabled = false;
                player.health.invulnerable = true;
                if (player.TryGetComponent<PlayerNoiseEmitter>(out var noise)) noise.debugNoiseKey = KeyCode.None;
                Intruder = player.GetComponent<ScriptedIntruder>();
                if (Intruder == null) Intruder = player.gameObject.AddComponent<ScriptedIntruder>();
                Intruder.enabled = true;
                foreach (var d in found) Intruder.guards.Add(d);
            }
            spectator = SpectatorCamera.Create(player != null ? player.viewCamera : null);
            if (player != null) CreatePlayerBody(player);
            SetManualControl(true);
        }

        void CreatePlayerBody(PlayerController player)
        {
            foreach (var guard in guards)
            {
                var dummy = guard.GetComponent<TargetDummy>();
                if (dummy.body == null) continue;
                var skin = dummy.body.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin == null) continue;
                Transform model = skin.transform;
                while (model.parent != dummy.body && model != dummy.body) model = model.parent;
                if (model == dummy.body) continue;
                playerBody = Instantiate(model.gameObject, player.transform, false);
                playerBody.name = "Player world body";
                foreach (var collider in playerBody.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_Color", new Color(.5f, .85f, 1f));
                foreach (var renderer in playerBody.GetComponentsInChildren<Renderer>())
                    renderer.SetPropertyBlock(tint);
                playerBody.AddComponent<GuardLocomotion>();
                return;
            }
        }

        // Copies BasicGuard settings the same way the Utility AI demo scene does; skips parts that already exist.
        static UtilityEnemyController Convert(TargetDummy dummy)
        {
            var go = dummy.gameObject;
            if (go.TryGetComponent<GuardPerception>(out var perception))
                perception.showOverlay = false;
            if (go.TryGetComponent<AutoChase>(out var chase)) chase.enabled = false;
            if (go.TryGetComponent<GuardScout>(out var scout)) scout.enabled = false;
            if (go.TryGetComponent<BasicGuard>(out var basic)) basic.enabled = false;
            if (!go.TryGetComponent<PathMover>(out var mover)) mover = go.AddComponent<PathMover>();
            var existing = go.GetComponent<UtilityEnemyController>();
            if (existing != null)
            {
                existing.EnableDemo(mover);
                return existing;
            }
            var weapon = go.GetComponent<EnemyWeapon>();
            if (weapon == null)
            {
                weapon = go.AddComponent<EnemyWeapon>(); // must exist before the controller (RequireComponent)
                var g = go.GetComponent<BasicGuard>();
                if (g != null)
                {
                    weapon.warningLight = g.warningLight; weapon.tracer = g.tracer;
                    weapon.range = g.range; weapon.damage = g.damage;
                    weapon.warningSeconds = g.warningSeconds; weapon.shotInterval = g.shotInterval;
                }
            }
            var provider = go.GetComponent<PerceptionObservation>();
            if (provider == null) provider = go.AddComponent<PerceptionObservation>();
            var controller = go.AddComponent<UtilityEnemyController>();
            controller.observations = provider;
            controller.EnableDemo(mover);
            return controller;
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.V)) SetManualControl(!manual);
            if (Input.GetKeyDown(KeyCode.N)) EmitNoise();
            if (Input.GetKeyDown(KeyCode.O)) ToggleRouteBarrier();
            if (Input.GetKeyDown(KeyCode.M))
            {
                var difficulty = (DemoDifficulty)(((int)Tactician.Difficulty + 1) % 3);
                Tactician.SetDifficulty(difficulty);
                PlayerPrefs.SetInt(DifficultyPreference, (int)difficulty);
            }
            if (Input.GetKeyDown(KeyCode.Alpha0)) SetCamera(CameraMode.Map);
            if (Input.GetKeyDown(KeyCode.Tab))
                SetCamera(CameraMode == CameraMode.Map ? CameraMode.Intruder : CameraMode.Map);
            for (int i = 0; i < 3; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < guards.Count)
                {
                    if (manual) SetManualControl(false);
                    selected = i;
                    SetCamera(CameraMode.Follow);
                }
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetCamera(CameraMode.Intruder);
            if (Input.GetKeyDown(KeyCode.X)) Run(IntruderScenario.Exposed);
            if (Input.GetKeyDown(KeyCode.H)) Run(IntruderScenario.Hide);
            if (Input.GetKeyDown(KeyCode.B)) Run(IntruderScenario.Ambush);
            if (CameraMode == CameraMode.Map)
            {
                float zoom = Input.mouseScrollDelta.y;
                if (Input.GetKey(KeyCode.U)) zoom += Time.unscaledDeltaTime * 6f;
                if (Input.GetKey(KeyCode.J)) zoom -= Time.unscaledDeltaTime * 6f;
                if (zoom != 0f) spectator.ZoomMap(zoom);
            }
            if (Input.GetKeyDown(KeyCode.P)) frozen = !frozen;
            if (Input.GetKeyDown(KeyCode.L)) slow = !slow;
        }

        public void Run(IntruderScenario scenario)
        {
            if (Intruder == null) return;
            if (manual) SetManualControl(false);
            var g = SelectedGuard;
            Intruder.Begin(scenario, g != null ? g.GetComponent<TargetDummy>() : null);
        }

        public void EmitNoise()
        {
            var game = GameSession.Instance;
            if (game != null && game.player != null)
                GuardPerception.BroadcastNoise(game.player.transform.position, 26f);
        }

        public bool ToggleRouteBarrier()
        {
            if (routeBarrier != null)
            {
                routeBarrier.SetActive(false);
                Destroy(routeBarrier);
                routeBarrier = null;
                Tactician.RefreshGeometry();
                ReportBarrier("Barrier removed");
                return true;
            }
            foreach (var guard in guards)
            {
                var navigation = guard.DemoNavigation;
                var path = navigation.ActiveRoute;
                if (path == null) continue;
                Vector3 position = guard.transform.position;
                Vector3 ahead = path[navigation.NextWaypointIndex] - position;
                ahead.y = 0f;
                if (ahead.magnitude < .8f) continue;
                routeBarrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                routeBarrier.name = "Demo Route Barrier";
                routeBarrier.transform.SetPositionAndRotation(position + Vector3.up * .95f + ahead.normalized * .75f,
                    Quaternion.LookRotation(ahead));
                routeBarrier.transform.localScale = new Vector3(1.15f, 1.9f, .26f);
                Physics.SyncTransforms();
                ReportBarrier("Barrier placed on " + guard.Id + "'s route");
                return true;
            }
            ReportBarrier("Wait for a moving guard, then press O");
            return false;
        }

        void ReportBarrier(string message)
        {
            barrierStatus = message;
            barrierStatusUntil = Time.unscaledTime + 3f;
        }

        public void SetManualControl(bool value)
        {
            var player = GameSession.Instance.player;
            if (player == null || Intruder == null) return;
            manual = value;
            frozen = false;
            slow = false;
            if (!manual) Intruder.Stop();
            Intruder.enabled = !manual;
            player.enabled = manual;
            player.health.invulnerable = !manual;
            if (player.rifle != null) player.rifle.enabled = manual;
            if (manual && CameraMode == CameraMode.Follow) CameraMode = CameraMode.Map;
            SetCamera(CameraMode);
        }

        void SetCamera(CameraMode mode)
        {
            CameraMode = mode;
            if (playerBody != null) playerBody.SetActive(mode != CameraMode.Intruder);
            var g = SelectedGuard;
            var player = GameSession.Instance.player;
            if (spectator != null)
            {
                spectator.SetMode(mode, mode == CameraMode.Follow && g != null ? g.transform : null,
                    player != null ? player.transform : null);
                if (player != null) player.DemoMapCamera = manual ? spectator.MapCamera : null;
            }
            UpdatePointer();
        }

        void UpdatePointer()
        {
            bool locked = manual && CameraMode == CameraMode.Intruder;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnDisable() { Time.timeScale = 1f; }
        void OnDestroy() { Time.timeScale = 1f; }

        void OnGUI()
        {
            var game = GameSession.Instance;
            if (!started || game == null || game.State != RoundState.Playing || Event.current.type != EventType.Repaint) return;
            scale = Mathf.Max(.35f, Mathf.Min(Screen.width / 1440f, Screen.height / 900f));
            if (keys == null)
            {
                keys = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                status = new GUIStyle(keys) { fontStyle = FontStyle.Bold };
                badge = new GUIStyle(keys) { fontStyle = FontStyle.Bold };
            }
            keys.fontSize = Mathf.RoundToInt(15f * scale); status.fontSize = Mathf.RoundToInt(17f * scale); badge.fontSize = Mathf.RoundToInt(16f * scale);
            float virtualWidth = Screen.width / scale;
            float w = 1050f, x = (virtualWidth - w) * .5f, y = Screen.height / scale - 100f, h = 90f;
            var bar = new Rect(x * scale, y * scale, w * scale, h * scale);
            GUI.color = new Color(.02f, .04f, .06f, .88f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.white;
            string controls = "0 Map   1–3 Guard   4 Intruder   X Expose   H Hide   B Ambush   N Sound   O Barrier   P Freeze   L Slow";
            GUI.Label(new Rect(bar.x, bar.y + 6f * scale, bar.width, 24f * scale), controls, keys);
            string cam = CameraMode == CameraMode.Map ? "Map" : CameraMode == CameraMode.Intruder ? "Intruder" : "Guard " + (SelectedGuard != null ? SelectedGuard.GetComponent<TargetDummy>().targetId : "?");
            string line = Time.unscaledTime < barrierStatusUntil ? barrierStatus :
                manual ? (CameraMode == CameraMode.Map ? "Map · WASD Move · Mouse Aim" : "Intruder · Manual control") :
                "Camera: " + cam + " · " + (Intruder != null ? Intruder.Status : "");
            GUI.color = new Color(.6f, .95f, 1f);
            GUI.Label(new Rect(bar.x, bar.y + 34f * scale, bar.width, 26f * scale), line, status);
            if (manual && CameraMode == CameraMode.Intruder)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(Screen.width * .5f - 2f, Screen.height * .5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            }
            string hints = manual ? "V Watch" : "V Play";
            hints += $"   ·   M Difficulty: {Tactician.Difficulty}";
            if (CameraMode == CameraMode.Map) hints += "   ·   Scroll or U / J Zoom";
            if (manual && game.player.rifle != null)
                hints += $"   ·   Health {Mathf.CeilToInt(game.player.health.Current)}   ·   Ammo {game.player.rifle.Ammo}/{game.player.rifle.Reserve}   ·   R Reload";
            if (frozen || slow)
            {
                GUI.color = new Color(1f, .67f, .29f);
                hints += frozen ? "   ·   FROZEN" : "   ·   SLOW 0.4x";
            }
            GUI.Label(new Rect(bar.x, bar.y + 62f * scale, bar.width, 24f * scale), hints, badge);
            GUI.color = Color.white;
        }
    }
}
