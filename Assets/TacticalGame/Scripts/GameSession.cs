using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame
{
    public enum RoundState { Briefing, Playing, Paused, Won, Lost }
    public enum GuardAiMode { Squad, ChaseOnly }

    // Mission rules only. This class deliberately makes no enemy decisions.
    [DefaultExecutionOrder(-10)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        static GuardAiMode selectedAiMode;
        public string missionName = "SUBSTATION";
        [TextArea] public string briefing;
        [Min(30)] public float roundSeconds = 360f;
        public PlayerController player;
        public ObjectiveTerminal[] objectives;
        public TargetDummy[] targets;
        public TacticalGame.Coordination.SquadCoordinator squadCoordinator;
        public bool useUtilitySquad = true;
        public ExitGate exitGate;
        public Camera overviewCamera;
        public Renderer overviewCeiling;
        public Renderer[] godViewCeilings = System.Array.Empty<Renderer>();
        public ReflectionProbe environmentReflections;
        public string nextScene = "RailDepot";

        float godViewAspect;
        bool useGodView;

        public RoundState State { get; private set; } = RoundState.Briefing;
        public GuardAiMode AiMode { get; private set; }
        public float Remaining { get; private set; }
        public float Elapsed => roundSeconds - Remaining;
        public bool Overview { get; private set; }
        public bool GodView => useGodView && !Overview;
        public bool FirstPerson => !Overview && !useGodView;
        public Camera GodViewCamera { get; private set; }
        public Camera ActiveCamera => Overview ? overviewCamera : GodView ? GodViewCamera : player.viewCamera;
        public bool CanControl => State == RoundState.Playing;
        public string Outcome { get; private set; }
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public float HitUntil { get; private set; }
        public float HurtUntil { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }
        public int Completed
        {
            get
            {
                int count = 0;
                foreach (var objective in objectives) if (objective.Completed) count++;
                return count;
            }
        }
        public bool ExitUnlocked => Completed == objectives.Length;
        public int TargetsDown
        {
            get
            {
                int count = 0;
                foreach (var target in targets) if (!target.health.IsAlive) count++;
                return count;
            }
        }

        void Awake()
        {
            Instance = this;
            AiMode = selectedAiMode;
            Time.timeScale = 1f;
            Remaining = roundSeconds;
            Application.targetFrameRate = 120;
            var cameraObject = new GameObject("God View Camera");
            cameraObject.transform.SetParent(transform, false);
            GodViewCamera = cameraObject.AddComponent<Camera>();
            GodViewCamera.enabled = false;
            GodViewCamera.fieldOfView = 55f;
            GodViewCamera.nearClipPlane = .1f;
            GodViewCamera.farClipPlane = player.viewCamera.farClipPlane;
            GodViewCamera.cullingMask = player.viewCamera.cullingMask & ~(1 << 2);
            player.health.Died += PlayerDefeated;
            player.health.Damaged += OnPlayerDamaged;
            SetPointer(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7))
            {
                GuardPerception.DebugView = !GuardPerception.DebugView;
                Notify("Perception debug " + (GuardPerception.DebugView ? "ON: cones, awareness bars and last known positions" : "OFF"));
            }
            if (State == RoundState.Briefing && useUtilitySquad && Input.GetKeyDown(KeyCode.F6))
                SetAiMode(AiMode == GuardAiMode.Squad ? GuardAiMode.ChaseOnly : GuardAiMode.Squad);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == RoundState.Playing) Pause();
                else if (State == RoundState.Paused) Resume();
            }
            if (State != RoundState.Playing) return;
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleOverview();
            else if (Input.GetKeyDown(KeyCode.V)) ToggleGodView();
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            if (Remaining <= 0f) Finish(false, "Time ran out. Restore both terminals, then reach the exit.");
        }

        void Start()
        {
            // Headless checks have no graphics device and must not render reflection cubemaps.
            if (environmentReflections != null && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                environmentReflections.RenderProbe();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && State == RoundState.Playing) Pause();
        }

        public void Begin()
        {
            if (State != RoundState.Briefing) return;
            if (AiMode == GuardAiMode.ChaseOnly && squadCoordinator != null) squadCoordinator.SetCoordinationEnabled(false);
            State = RoundState.Playing;
            Time.timeScale = 1f;
            SetPointer(true);
            Notify("Restore both terminals. Hold T for markers. Use cover against the guards.");
        }

        public void SetAiMode(GuardAiMode mode)
        {
            if (State != RoundState.Briefing) return;
            AiMode = selectedAiMode = mode;
            if (squadCoordinator != null) squadCoordinator.SetCoordinationEnabled(mode == GuardAiMode.Squad);
        }

        public static void SetNextAiMode(GuardAiMode mode) => selectedAiMode = mode;

        public void Pause()
        {
            if (State != RoundState.Playing) return;
            State = RoundState.Paused;
            Time.timeScale = 0f;
            SetPointer(false);
        }

        public void Resume()
        {
            if (State != RoundState.Paused) return;
            State = RoundState.Playing;
            Time.timeScale = 1f;
            SetPointer(FirstPerson);
        }

        public void ToggleOverview()
        {
            if (!CanControl || overviewCamera == null) return;
            Overview = !Overview;
            ApplyView();
        }

        public void ToggleGodView()
        {
            if (!CanControl) return;
            useGodView = Overview || !useGodView;
            Overview = false;
            ApplyView();
        }

        void ApplyView()
        {
            player.viewCamera.enabled = FirstPerson;
            if (overviewCamera != null) overviewCamera.enabled = Overview;
            GodViewCamera.enabled = GodView;
            if (overviewCeiling != null) overviewCeiling.forceRenderingOff = !FirstPerson;
            foreach (var ceiling in godViewCeilings)
                if (ceiling != null) ceiling.forceRenderingOff = GodView;
            if (GodView) FrameArena();
            player.ShowGodViewAvatar(GodView);
            SetPointer(FirstPerson);
        }

        internal bool IsViewCeiling(Collider collider)
        {
            if (overviewCeiling != null && collider.gameObject == overviewCeiling.gameObject) return true;
            if (!GodView) return false;
            foreach (var ceiling in godViewCeilings)
                if (ceiling != null && collider.gameObject == ceiling.gameObject) return true;
            return false;
        }

        void LateUpdate()
        {
            if (GodView && !Mathf.Approximately(GodViewCamera.aspect, godViewAspect)) FrameArena();
        }

        void FrameArena()
        {
            Vector3 center = overviewCamera != null ? overviewCamera.transform.position : Vector3.zero;
            center.y = 0f;
            float extent = overviewCamera != null ? overviewCamera.orthographicSize : 23f;
            Quaternion rotation = Quaternion.Euler(58f, 0f, 0f);
            Quaternion toCameraSpace = Quaternion.Inverse(rotation);
            godViewAspect = GodViewCamera.aspect;
            float vertical = Mathf.Tan(GodViewCamera.fieldOfView * .5f * Mathf.Deg2Rad) * .84f;
            float horizontal = vertical * Mathf.Max(.1f, godViewAspect);
            float distance = 0f;
            // Fit all eight corners so a fixed perspective still covers the arena on resize.
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3((corner & 1) == 0 ? -extent : extent,
                    (corner & 2) == 0 ? 0f : 6f, (corner & 4) == 0 ? -extent : extent);
                Vector3 point = toCameraSpace * offset;
                float depth = Mathf.Max(Mathf.Abs(point.x) / horizontal, Mathf.Abs(point.y) / vertical);
                distance = Mathf.Max(distance, depth - point.z);
            }
            GodViewCamera.farClipPlane = Mathf.Max(player.viewCamera.farClipPlane, distance + extent * 2f);
            GodViewCamera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
        }

        public void CompleteObjective(ObjectiveTerminal terminal)
        {
            Notify(ExitUnlocked ? "POWER RESTORED / Exit unlocked. Head to the green gate." : terminal.displayName + " online. One terminal remaining.");
            GameAudio.Instance.PlayCue(ExitUnlocked ? 880f : 660f, .25f);
        }

        public void Escape()
        {
            if (State == RoundState.Playing && ExitUnlocked)
                Finish(true, "Both systems restored. You reached the evacuation route.");
        }

        void PlayerDefeated() => Finish(false, "Health depleted. Break enemy fire with solid cover and use medical supplies.");
        void OnPlayerDamaged(float amount) => HurtUntil = Time.unscaledTime + .28f;

        public void Finish(bool won, string reason)
        {
            if (State != RoundState.Playing) return;
            State = won ? RoundState.Won : RoundState.Lost;
            Outcome = reason;
            Time.timeScale = 0f;
            SetPointer(false);
        }

        public void Notify(string message)
        {
            Message = message;
            MessageUntil = Time.unscaledTime + 4f;
        }

        public void RegisterShot(bool hit)
        {
            ShotsFired++;
            if (hit) { ShotsHit++; HitUntil = Time.unscaledTime + .13f; }
        }

        public void Restart() => LoadMission(SceneManager.GetActiveScene().name);

        public void LoadMission(string sceneName)
        {
            Time.timeScale = 1f;
            SetPointer(false);
            SceneManager.LoadScene(sceneName);
        }

        public void Quit()
        {
            Time.timeScale = 1f;
            SetPointer(false);
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        static void SetPointer(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Time.timeScale = 1f; SetPointer(false); }
        }
    }
}
