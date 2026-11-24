using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalGame
{
    public enum RoundState { Briefing, Playing, Paused, Won, Lost }

    // Mission rules only. This class deliberately makes no enemy decisions.
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public string missionName = "SUBSTATION";
        [TextArea] public string briefing;
        [Min(30)] public float roundSeconds = 360f;
        public PlayerController player;
        public ObjectiveTerminal[] objectives;
        public TargetDummy[] targets;
        public TacticalGame.Coordination.SquadCoordinator squadCoordinator;
        public ExitGate exitGate;
        public Camera overviewCamera;
        public ReflectionProbe environmentReflections;
        public string nextScene = "RailDepot";

        public RoundState State { get; private set; } = RoundState.Briefing;
        public float Remaining { get; private set; }
        public float Elapsed => roundSeconds - Remaining;
        public bool Overview { get; private set; }
        public bool CanControl => State == RoundState.Playing && !Overview;
        public string Outcome { get; private set; }
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public float HitUntil { get; private set; }
        public float HurtUntil { get; private set; }
        public float LastDamage { get; private set; }
        public Vector3 LastHitFrom { get; private set; }
        public float LastHitTime { get; private set; } = -99f;
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
            Time.timeScale = 1f;
            Remaining = roundSeconds;
            Application.targetFrameRate = 120;
            player.health.Died += PlayerDefeated;
            player.health.Damaged += OnPlayerDamaged;
            SetPointer(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == RoundState.Playing) Pause();
                else if (State == RoundState.Paused) Resume();
            }
            if (State != RoundState.Playing) return;
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleOverview();
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
            State = RoundState.Playing;
            Time.timeScale = 1f;
            SetPointer(true);
            Notify("Restore both terminals. Hold T for markers. Use cover against the guards.");
        }

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
            SetPointer(!Overview);
        }

        public void ToggleOverview()
        {
            Overview = !Overview;
            player.viewCamera.enabled = !Overview;
            overviewCamera.enabled = Overview;
            SetPointer(!Overview);
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
        void OnPlayerDamaged(float amount) { LastDamage = amount; HurtUntil = Time.unscaledTime + .5f; }
        public void ReportPlayerHitFrom(Vector3 source) { LastHitFrom = source; LastHitTime = Time.unscaledTime; }

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
