using UnityEngine;
using TacticalGame.Coordination;

namespace TacticalGame
{
    // Immediate-mode UI needs no canvas setup or downloaded UI packages.
    public sealed class GameHud : MonoBehaviour
    {
        static readonly Color Ink = new Color(.025f, .045f, .065f, .94f);
        static readonly Color Muted = new Color(.59f, .68f, .72f);
        static readonly Color Cyan = new Color(.30f, .91f, .93f);
        static readonly Color Green = new Color(.40f, 1f, .65f);
        static readonly Color Orange = new Color(1f, .67f, .29f);
        GUIStyle text;
        GUIStyleState textNormal; // GUIStyle.normal returns a new wrapper on every access; keep one
        readonly GUIContent labelContent = new GUIContent(); // GUI.Label(string) builds new content each call
        GUIStyle button;
        float scale;
        float width;
        float height;

        // Cached HUD strings: rebuilt only when the value they show changes, so the HUD does not
        // allocate new strings every frame.
        string missionTitle, missionTitleFor, clockText, healthText, ammoText, reserveText, guardsText, promptText;
        int clockValue = -1, healthValue = -1, ammoValue = -1, reserveValue = -1, guardsValue = -1, promptVersion = -1;
        WorldInteractable promptFocus;
        string[] objectiveTexts;
        int[] objectiveStates;
        string[] markerTexts;
        int[] markerValues;

        // The HUD uses absolute rects only, so IMGUI's layout pass is skipped entirely.
        void Awake() => useGUILayout = false;

        void OnGUI()
        {
            var game = GameSession.Instance;
            if (game == null) return;
            scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            scale = Mathf.Max(.35f, scale);
            width = Screen.width / scale;
            height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = false, wordWrap = true };
                textNormal = text.normal;
                button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
                button.normal.textColor = Color.white;
            }

            if (game.State == RoundState.Playing)
            {
                // The in-game HUD has no controls: only the repaint event needs to draw anything.
                if (Event.current.type != EventType.Repaint) return;
                DrawMission(game);
                DrawPlayer(game);
                DrawMarkers(game);
                if (!game.FirstPerson) DrawPlayerMarker(game);
                else
                {
                    DrawCrosshair(game);
                    DrawDetection(game);
                }
                if (Time.unscaledTime < game.HurtUntil)
                {
                    Box(0, 0, width, 8, new Color(1f, .22f, .1f, .8f));
                    Box(0, height - 8, width, 8, new Color(1f, .22f, .1f, .8f));
                    Box(0, 0, 8, height, new Color(1f, .22f, .1f, .8f));
                    Box(width - 8, 0, 8, height, new Color(1f, .22f, .1f, .8f));
                }
                DrawGuardOverlays(game);
            }
            else DrawMenu(game);
        }

        void DrawMission(GameSession game)
        {
            Box(30, 28, 345, 166, Ink);
            Box(30, 28, 3, 166, Cyan);
            if (missionTitleFor != game.missionName) { missionTitleFor = game.missionName; missionTitle = "OPERATION / " + game.missionName; }
            Label(49, 42, 310, 22, missionTitle, 13, Cyan, true);
            if (objectiveTexts == null || objectiveTexts.Length != game.objectives.Length)
            {
                objectiveTexts = new string[game.objectives.Length];
                objectiveStates = new int[game.objectives.Length];
                for (int i = 0; i < objectiveStates.Length; i++) objectiveStates[i] = -1;
            }
            for (int i = 0; i < game.objectives.Length; i++)
            {
                var item = game.objectives[i];
                int state = item.Completed ? 1 : 0;
                if (objectiveStates[i] != state)
                {
                    objectiveStates[i] = state;
                    objectiveTexts[i] = (item.Completed ? "[+] " : "[  ] ") + item.displayName;
                }
                Label(49, 78 + i * 35, 303, 28, objectiveTexts[i], 19, item.Completed ? Green : Color.white);
            }
            Label(49, 153, 310, 24, game.ExitUnlocked ? "EXIT OPEN / Reach the green gate" : "Restore power to unlock extraction", 14, game.ExitUnlocked ? Green : Muted);

            Box(width - 236, 28, 206, 92, Ink);
            Label(width - 217, 39, 173, 19, "MISSION CLOCK", 12, Muted);
            int time = Mathf.CeilToInt(game.Remaining);
            if (time != clockValue) { clockValue = time; clockText = (time / 60).ToString("00") + ":" + (time % 60).ToString("00"); }
            Label(width - 217, 60, 173, 44, clockText, 34, time < 60 ? Orange : Color.white, true);

            if (game.GodView)
            {
                float left = width / 2 - 210;
                Box(left, 28, 420, 90, Ink);
                Label(left, 34, 420, 26, "GOD VIEW", 20, Cyan, true, TextAnchor.MiddleCenter);
                var squad = game.squadCoordinator;
                string status = squad == null || !squad.CoordinationEnabled ? "Squad coordination off" : "Squad active · No shared sighting";
                if (squad != null && squad.TryGetSharedSighting(out var sighting))
                    status = $"Guard {sighting.Report.ReporterId} shared a sighting · {sighting.AgeSeconds:0.0}s ago";
                Label(left, 62, 420, 23, status, 14, Color.white, false, TextAnchor.MiddleCenter);
                Label(left + 27, 89, 102, 22, "A* paths", 13, SquadCoordinationDebug.RouteColor, false, TextAnchor.MiddleCenter);
                Label(left + 132, 89, 148, 22, "Claimed positions", 13, SquadCoordinationDebug.ClaimColor, false, TextAnchor.MiddleCenter);
                Label(left + 275, 89, 125, 22, "Shared sighting", 13, SquadCoordinationDebug.SightingColor, false, TextAnchor.MiddleCenter);
            }
            else if (game.Overview)
            {
                Box(width / 2 - 250, height - 145, 500, 75, Ink);
                Label(width / 2 - 230, height - 134, 460, 25, "MAP", 20, Cyan, true, TextAnchor.MiddleCenter);
                Label(width / 2 - 230, height - 103, 460, 22, "Mouse to aim   /   Point and press E to interact", 14, Muted, false, TextAnchor.MiddleCenter);
            }
        }

        void DrawPlayer(GameSession game)
        {
            var player = game.player;
            Box(30, height - 131, 263, 98, Ink);
            Label(49, height - 116, 110, 24, "HEALTH", 13, Muted, true);
            int health = Mathf.CeilToInt(player.health.Current);
            if (health != healthValue) { healthValue = health; healthText = health.ToString("000"); }
            Label(186, height - 119, 85, 34, healthText, 27, player.health.Current <= 30 ? Orange : Color.white, true, TextAnchor.MiddleRight);
            Box(49, height - 67, 222, 7, new Color(.18f, .24f, .27f));
            Box(49, height - 67, 222 * player.health.Current / player.health.maximum, 7, Green);

            Box(width - 277, height - 131, 247, 98, Ink);
            Label(width - 257, height - 117, 190, 22, "R-24 / SERVICE RIFLE", 12, Muted, true);
            if (player.rifle.Ammo != ammoValue) { ammoValue = player.rifle.Ammo; ammoText = ammoValue.ToString("00"); }
            if (player.rifle.Reserve != reserveValue) { reserveValue = player.rifle.Reserve; reserveText = "/ " + reserveValue.ToString("000"); }
            Label(width - 259, height - 93, 110, 49, ammoText, 39, Color.white, true);
            Label(width - 163, height - 77, 110, 29, reserveText, 22, Muted);
            if (player.rifle.IsReloading)
            {
                Label(width / 2 - 100, height / 2 + 70, 200, 24, "RELOADING", 14, Orange, true, TextAnchor.MiddleCenter);
                Box(width / 2 - 75, height / 2 + 101, 150, 4, Ink);
                Box(width / 2 - 75, height / 2 + 101, 150 * player.rifle.ReloadProgress, 4, Orange);
            }

            string controls = game.FirstPerson
                ? "WASD move    SHIFT sprint    SPACE jump    LMB fire    RMB aim    R reload    E interact    T markers    TAB map    V God View    ESC pause"
                : "WASD move    SHIFT sprint    SPACE jump    LMB fire    R reload    E interact    TAB map    V perspective    ESC pause";
            if (game.GodView) controls += "    F3 squad panel";
            Label(30, height - 29, width - 60, 22, controls, 12, Muted, false, TextAnchor.MiddleCenter);
            if (player.Focus != null)
            {
                float promptTop = game.GodView ? height - 87 : height / 2 + 128;
                Box(width / 2 - 250, promptTop, 500, 43, Ink);
                // A prompt only changes with the focused object or with mission progress.
                int version = game.Completed + (game.ExitUnlocked ? 100 : 0);
                if (!ReferenceEquals(player.Focus, promptFocus) || version != promptVersion)
                { promptFocus = player.Focus; promptVersion = version; promptText = player.Focus.Prompt; }
                Label(width / 2 - 240, promptTop + 8, 480, 29, promptText, 17, Cyan, true, TextAnchor.MiddleCenter);
            }
            if (Time.unscaledTime < game.MessageUntil)
            {
                float messageTop = game.GodView ? 126 : height - 207;
                Box(width / 2 - 350, messageTop, 700, 49, Ink);
                Label(width / 2 - 332, messageTop + 9, 664, 34, game.Message, 16, Color.white, false, TextAnchor.MiddleCenter);
            }
            int down = game.TargetsDown;
            if (down != guardsValue) { guardsValue = down; guardsText = "GUARDS " + down + " / " + game.targets.Length; }
            Label(width - 236, 126, 206, 27, guardsText, 13, Muted, false, TextAnchor.MiddleRight);
        }

        void DrawCrosshair(GameSession game)
        {
            float x = width / 2;
            float y = height / 2;
            Color color = Time.unscaledTime < game.HitUntil ? Orange : new Color(1f, 1f, 1f, .85f);
            if (Time.unscaledTime < game.HitUntil)
            {
                Label(x - 15, y - 17, 30, 34, "x", 26, Orange, true, TextAnchor.MiddleCenter);
                return;
            }
            float gap = game.player.rifle.IsAiming ? 4 : 8;
            Box(x - gap - 7, y - 1, 7, 2, color);
            Box(x + gap, y - 1, 7, 2, color);
            Box(x - 1, y - gap - 7, 2, 7, color);
            Box(x - 1, y + gap, 2, 7, color);
            Box(x - 1, y - 1, 2, 2, color);
        }

        // Marks around the crosshair point toward guards that are suspicious (yellow, grows with
        // awareness) or alerted (red). It gives direction only, never a guard's position through walls.
        void DrawDetection(GameSession game)
        {
            if (Event.current.type != EventType.Repaint || game.player.viewCamera == null) return;
            Transform view = game.player.viewCamera.transform;
            Vector3 forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < .0001f) return;
            Vector3 center = new Vector3(width / 2, height / 2, 0f);
            Matrix4x4 baseMatrix = GUI.matrix;
            foreach (var target in game.targets)
            {
                if (target == null || !target.IsAlive || !target.TryGetComponent<GuardPerception>(out var sense)) continue;
                if (!sense.isActiveAndEnabled || sense.State == GuardPerception.AwarenessState.Unaware) continue;
                Vector3 toGuard = target.transform.position - view.position;
                toGuard.y = 0f;
                if (toGuard.sqrMagnitude < .01f) continue;
                float angle = Vector3.SignedAngle(forward, toGuard, Vector3.up);
                bool alerted = sense.State == GuardPerception.AwarenessState.Alerted;
                Color color = alerted ? new Color(1f, .25f, .15f, .95f) : new Color(1f, .8f, .2f, .35f + .55f * sense.Awareness);
                float length = alerted ? 64f : 26f + 34f * sense.Awareness;
                GUI.matrix = baseMatrix * Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
                Box(-length / 2, -118, length, alerted ? 6 : 4, color);
                Box(-3, -128, 6, 9, color);
            }
            GUI.matrix = baseMatrix;
        }

        // Guard icons (and the F7 debug bars) are drawn here in screen pixels, after the scaled HUD.
        void DrawGuardOverlays(GameSession game)
        {
            Matrix4x4 hudMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            foreach (var target in game.targets)
                if (target != null && target.TryGetComponent<GuardPerception>(out var sense) && sense.isActiveAndEnabled)
                    sense.DrawOverlay();
            GUI.matrix = hudMatrix;
        }

        void DrawMarkers(GameSession game)
        {
            if (!game.Overview && !Input.GetKey(KeyCode.T)) return;
            var camera = game.ActiveCamera;
            if (markerTexts == null || markerTexts.Length != game.objectives.Length)
            {
                markerTexts = new string[game.objectives.Length];
                markerValues = new int[game.objectives.Length];
                for (int i = 0; i < markerValues.Length; i++) markerValues[i] = int.MinValue;
            }
            for (int index = 0; index < game.objectives.Length; index++)
            {
                var objective = game.objectives[index];
                Vector3 point = camera.WorldToScreenPoint(objective.transform.position + Vector3.up * 2.9f);
                if (point.z < 0f) continue;
                float x = point.x / scale;
                float y = (Screen.height - point.y) / scale;
                if (x < 80 || x > width - 80 || y < (game.Overview ? 80 : 205) || y > height - (game.Overview ? 150 : 240)) continue;
                int metres = objective.Completed ? -1 : Mathf.RoundToInt(Vector3.Distance(game.player.transform.position, objective.transform.position));
                if (markerValues[index] != metres)
                {
                    markerValues[index] = metres;
                    markerTexts[index] = objective.letter + (objective.Completed ? " / OK" : " / " + metres + "m");
                }
                Box(x - 47, y - 14, 94, 31, Ink);
                Label(x - 45, y - 11, 90, 26, markerTexts[index], 14, objective.Completed ? Green : Cyan, true, TextAnchor.MiddleCenter);
            }
            if (game.ExitUnlocked && game.exitGate != null)
            {
                var point = camera.WorldToScreenPoint(game.exitGate.transform.position + Vector3.up * 3.5f);
                if (point.z > 0f)
                {
                    float x = Mathf.Clamp(point.x / scale, 70, width - 70);
                    float y = Mathf.Clamp((Screen.height - point.y) / scale, 85, height - 180);
                    Box(x - 60, y - 14, 120, 31, Ink);
                    Label(x - 58, y - 11, 116, 26, "EXIT / OPEN", 14, Green, true, TextAnchor.MiddleCenter);
                }
            }
        }

        void DrawPlayerMarker(GameSession game)
        {
            var camera = game.ActiveCamera;
            var player = game.player.transform;
            var point = camera.WorldToScreenPoint(player.position);
            float x = point.x / scale;
            float y = (Screen.height - point.y) / scale;
            if (game.GodView)
            {
                Box(x - 21, y + 5, 42, 19, Ink);
                Label(x - 21, y + 4, 42, 20, "YOU", 12, Cyan, true, TextAnchor.MiddleCenter);
                return;
            }
            var facing = camera.WorldToScreenPoint(player.position + player.forward * .75f);
            Box(facing.x / scale - 2, (Screen.height - facing.y) / scale - 2, 4, 4, Color.white);
            Box(x - 7, y - 7, 14, 14, Ink);
            Box(x - 5, y - 5, 10, 10, Cyan);
            Label(x + 10, y - 10, 60, 25, "YOU", 12, Cyan, true);
        }

        void DrawMenu(GameSession game)
        {
            Box(0, 0, width, height, new Color(.01f, .025f, .04f, .76f));
            float x = width / 2 - 480;
            float y = height / 2 - 330;
            Box(x, y, 960, 660, Ink);
            Box(x, y, 4, 660, Cyan);
            Label(x + 38, y + 29, 850, 24, "TACTICAL ENEMY AI  /  COURSE FOUNDATION 01", 13, Cyan, true);
            string title = game.State == RoundState.Briefing ? "LAST LIGHT" : game.State == RoundState.Paused ? "MISSION PAUSED" : game.State == RoundState.Won ? "EXTRACTION COMPLETE" : "MISSION ENDED";
            Label(x + 36, y + 66, 890, 65, title, 43, Color.white, true);
            Label(x + 39, y + 141, 850, 26, (game.nextScene == "RailDepot" ? "01" : "02") + "  /  " + game.missionName + "     ·     3D FIRST-PERSON PROTOTYPE", 14, Muted, true);
            Box(x + 39, y + 186, 882, 1, new Color(.2f, .29f, .33f));

            if (game.State == RoundState.Briefing || game.State == RoundState.Paused)
            {
                Label(x + 39, y + 210, 470, 100, game.briefing, 20, Color.white);
                Label(x + 39, y + 327, 460, 85, "Restore terminals A and B in either order, then extract. Hold T for directions. Armed guards cover the routes; solid walls block their shots.", 17, Muted);
                Label(x + 39, y + 427, 470, 90, "Guards warn before firing. Use solid cover, listen for movement, and collect medical supplies when hurt.", 15, Orange);
                if (game.useUtilitySquad && game.State == RoundState.Briefing)
                    Label(x + 39, y + 506, 470, 26, "GUARD AI: " + (game.AiMode == GuardAiMode.Squad ? "Squad" : "Chase only") + "  [F6]", 15, Cyan, true);
                Label(x + 550, y + 213, 325, 25, "FIELD CONTROLS", 13, Cyan, true);
                Label(x + 550, y + 255, 335, 198, "WASD / arrows       Move\nMouse                      Look / aim\nShift / Space          Sprint / Jump\nLeft / Right click    Fire / Zoom in first person\nR / E                         Reload / Interact\nTab / V                    Map / 3D God View\nF3 / F7                    Squad / Perception\nEscape                     Pause", 17, Color.white);
                Label(x + 550, y + 460, 300, 22, "LOOK SENSITIVITY", 12, Muted, true);
                float sensitivity = GUI.HorizontalSlider(new Rect(x + 550, y + 494, 255, 20), game.player.sensitivity, .3f, 5f);
                if (!Mathf.Approximately(sensitivity, game.player.sensitivity)) game.player.SetSensitivity(sensitivity);
            }
            else
            {
                Label(x + 39, y + 216, 850, 67, game.Outcome, 22, Color.white);
                Label(x + 39, y + 313, 400, 130, "OBJECTIVES RESTORED\nTARGETS DISABLED\nTIME ELAPSED\nSHOOTING ACCURACY", 17, Muted);
                int seconds = Mathf.RoundToInt(game.Elapsed);
                string accuracy = game.ShotsFired == 0 ? "--" : Mathf.RoundToInt(100f * game.ShotsHit / game.ShotsFired) + "%";
                Label(x + 510, y + 313, 350, 130, game.Completed + " / " + game.objectives.Length + "\n" + game.TargetsDown + " / " + game.targets.Length + "\n" + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + "\n" + accuracy, 17, Cyan, true);
            }

            string primary = game.State == RoundState.Briefing ? "BEGIN MISSION  >" : game.State == RoundState.Paused ? "RESUME  >" : "RETRY MISSION  >";
            if (Button(x + 39, y + 551, 280, 49, primary, Cyan))
            {
                if (game.State == RoundState.Briefing) game.Begin();
                else if (game.State == RoundState.Paused) game.Resume();
                else game.Restart();
            }
            if (Button(x + 335, y + 551, 280, 49, game.nextScene == "RailDepot" ? "PLAY RAIL DEPOT" : "PLAY SUBSTATION", new Color(.22f, .36f, .42f))) game.LoadMission(game.nextScene);
            if (Button(x + 631, y + 551, 130, 49, "RESTART", new Color(.22f, .28f, .32f))) game.Restart();
            if (Button(x + 777, y + 551, 144, 49, "QUIT", new Color(.22f, .28f, .32f))) game.Quit();
            Label(x + 39, y + 621, 850, 24, "Visual assets: Unity Multiplayer FPS template / Unity Technologies     ·     LAST LIGHT", 11, Muted);
        }

        bool Button(float x, float y, float w, float h, string label, Color color)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = GUI.Button(new Rect(x, y, w, h), label, button);
            GUI.backgroundColor = old;
            return clicked;
        }

        void Label(float x, float y, float w, float h, string value, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            textNormal.textColor = color;
            text.alignment = align;
            labelContent.text = value;
            GUI.Label(new Rect(x, y, w, h), labelContent, text);
        }

        static void Box(float x, float y, float w, float h, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
