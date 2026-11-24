using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace TacticalGame.Editor
{
    // Plays Substation for a fixed time with the player standing at the start (kept alive) while the
    // squad perceives and engages, and records per-frame main thread time and managed allocations.
    // Results are appended to Logs/perf-benchmark.txt so before/after runs can be compared.
    // Keep the Unity window focused while it runs: an unfocused editor pauses the round.
    public static class PerformanceBenchmark
    {
        const string Running = "TacticalGame.PerfBench.Running";
        const float WarmupSeconds = 3f;
        const float MeasureSeconds = 20f;
        const int Capacity = 6000;

        static int stage;
        static bool withoutHud;
        static double stageStart;
        static bool oldOptionsEnabled;
        static EnterPlayModeOptions oldOptions;
        static readonly List<(string Label, ProfilerRecorder Recorder, bool Time)> recorders = new List<(string, ProfilerRecorder, bool)>();

        [MenuItem("Tactical Game/Run performance benchmark")]
        public static void Run() => Start(false);

        // Same run with the HUD and guard overlays switched off, to separate their cost from the rest.
        [MenuItem("Tactical Game/Run performance benchmark (no HUD)")]
        public static void RunWithoutHud() => Start(true);

        static void Start(bool noHud)
        {
            withoutHud = noHud;
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before running the benchmark."); return; }
            stage = 0;
            SessionState.SetBool(Running, true);
            oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(ProjectBuilder.SubstationPath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                var game = GameSession.Instance;
                if (game == null || game.player == null) return;
                if (stage == 0)
                {
                    UnityEngine.Random.InitState(1234);
                    game.Begin();
                    if (withoutHud)
                    {
                        foreach (var hud in UnityEngine.Object.FindObjectsByType<GameHud>()) hud.enabled = false;
                        foreach (var sense in UnityEngine.Object.FindObjectsByType<GuardPerception>()) sense.showOverlay = false;
                    }
                    stage = 1;
                    stageStart = EditorApplication.timeSinceStartup;
                    return;
                }
                KeepRoundRunning(game);
                double elapsed = EditorApplication.timeSinceStartup - stageStart;
                if (stage == 1 && elapsed >= WarmupSeconds)
                {
                    StartRecorders();
                    ProfilerDriver.ClearAllFrames();
                    ProfilerDriver.profileEditor = false;
                    ProfilerDriver.enabled = true;
                    stage = 2;
                    stageStart = EditorApplication.timeSinceStartup;
                }
                else if (stage == 2 && elapsed >= MeasureSeconds)
                {
                    Report(elapsed);
                    Finish();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish();
            }
        }

        static void KeepRoundRunning(GameSession game)
        {
            if (game.State == RoundState.Paused) game.Resume();
            if (game.Overview) game.ToggleOverview();
            game.player.health.Heal(1000f);
        }

        static void StartRecorders()
        {
            DisposeRecorders();
            Add("Main thread (ms)", ProfilerCategory.Internal, "Main Thread", true);
            Add("Scripts Update (ms)", ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", true);
            Add("Scripts LateUpdate (ms)", ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", true);
            Add("IMGUI repaint (ms)", ProfilerCategory.Gui, "GUI.Repaint", true);
            Add("IMGUI events (ms)", ProfilerCategory.Gui, "GUIUtility.ProcessEvent", true);
            Add("GC alloc per frame (bytes)", ProfilerCategory.Memory, "GC Allocated In Frame", false);
            Add("GC allocations per frame (count)", ProfilerCategory.Memory, "GC Allocation In Frame Count", false);
        }

        static void Add(string label, ProfilerCategory category, string stat, bool time)
        {
            recorders.Add((label, ProfilerRecorder.StartNew(category, stat, Capacity), time));
        }

        static void Report(double seconds)
        {
            var text = new StringBuilder();
            text.AppendLine("=== Performance benchmark " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                " | Unity " + Application.unityVersion + " | " + SystemInfo.processorType + " | Substation" + (withoutHud ? " (HUD off)" : "") + ", " +
                MeasureSeconds + " s after " + WarmupSeconds + " s warm-up ===");
            foreach (var (label, recorder, time) in recorders)
            {
                if (!recorder.Valid || recorder.Count == 0) { text.AppendLine(label + ": not available"); continue; }
                var values = new List<double>(recorder.Count);
                for (int i = 0; i < recorder.Count; i++)
                    values.Add(time ? recorder.GetSample(i).Value / 1e6 : recorder.GetSample(i).Value);
                values.Sort();
                double sum = 0;
                foreach (var v in values) sum += v;
                double mean = sum / values.Count;
                double median = values[values.Count / 2];
                double p95 = values[Mathf.Min(values.Count - 1, (int)(values.Count * .95))];
                double max = values[values.Count - 1];
                string fmt = time ? "F3" : "F0";
                text.AppendLine($"{label}: mean {mean.ToString(fmt)} | median {median.ToString(fmt)} | p95 {p95.ToString(fmt)} | max {max.ToString(fmt)} | frames {values.Count}");
            }
            ReportGameAllocations(text);
            if (recorders.Count > 0 && recorders[0].Recorder.Valid)
                text.AppendLine($"Frames rendered: {recorders[0].Recorder.Count} in {seconds:F1} s ({recorders[0].Recorder.Count / seconds:F1} fps, capped by targetFrameRate)");
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "perf-benchmark.txt");
            File.AppendAllText(path, text.ToString() + Environment.NewLine);
            Debug.Log(text.ToString());
        }

        // Reads the Profiler's own frames: only the PlayerLoop subtree (game code), not the editor.
        static void ReportGameAllocations(StringBuilder text)
        {
            ProfilerDriver.enabled = false;
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            if (first < 0 || last < first) { text.AppendLine("Profiler frames: none captured"); return; }
            var perMethod = new Dictionary<string, double>();
            var perMethodMs = new Dictionary<string, double>();
            double playerLoopBytes = 0;
            int frames = 0;
            var children = new List<int>();
            var stack = new Stack<int>();
            for (int f = first; f <= last; f++)
            {
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    HierarchyFrameDataView.columnGcMemory, false))
                {
                    if (view == null || !view.valid) continue;
                    frames++;
                    stack.Clear();
                    stack.Push(view.GetRootItemID());
                    while (stack.Count > 0)
                    {
                        int id = stack.Pop();
                        string name = view.GetItemName(id);
                        if (name == "PlayerLoop") playerLoopBytes += view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);
                        if (name.Contains("()"))
                        {
                            perMethod.TryGetValue(name, out double bytes);
                            perMethod[name] = bytes + view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);
                            perMethodMs.TryGetValue(name, out double ms);
                            perMethodMs[name] = ms + view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
                            continue;
                        }
                        view.GetItemChildren(id, children);
                        foreach (int child in children) stack.Push(child);
                    }
                }
            }
            if (frames == 0) { text.AppendLine("Profiler frames: none readable"); return; }
            text.AppendLine($"Game code (PlayerLoop) GC alloc: {playerLoopBytes / frames:F0} bytes/frame over the last {frames} profiled frames");
            var top = new List<KeyValuePair<string, double>>(perMethod);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < top.Count && i < 12; i++)
                text.AppendLine($"  {top[i].Key}: {top[i].Value / frames:F0} bytes/frame, {perMethodMs[top[i].Key] / frames:F3} ms/frame");
        }

        static void DisposeRecorders()
        {
            foreach (var entry in recorders) entry.Recorder.Dispose();
            recorders.Clear();
        }

        static void Finish()
        {
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            DisposeRecorders();
            EditorApplication.isPlaying = false;
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
        }
    }
}
