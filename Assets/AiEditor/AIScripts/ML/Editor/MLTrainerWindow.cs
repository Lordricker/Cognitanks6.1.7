using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Minimal in-Editor "Train" flow (BottomUpAgentPlan.md Section 13 Phase 3) - launches/monitors
/// mlagents-learn via MLTrainerProcessManager instead of a hand-run PowerShell terminal. First cut:
/// same config-path/run-id/initialize-from arguments run_training.ps1 has taken by hand all
/// session, no MLGoalNodeAsset integration yet (that's still Phase 2's authoring UI work).
/// </summary>
public class MLTrainerWindow : EditorWindow
{
    [MenuItem("Cognitanks/ML Trainer")]
    private static void Open()
    {
        var window = GetWindow<MLTrainerWindow>("ML Trainer");
        window.minSize = new Vector2(420, 300);
    }

    private string configPath = "MLTraining/configs/solo_tank.yaml";
    private string runId = "ML-";
    private string initializeFromRunId = "";

    private MLTrainerProcessManager manager;
    private readonly StringBuilder logBuilder = new StringBuilder();
    private readonly ConcurrentQueue<string> pendingLines = new ConcurrentQueue<string>();
    private Vector2 scroll;
    private double stopRequestedAt = -1;

    private void OnEnable()
    {
        EditorApplication.update += DrainPendingLines;
    }

    private void OnDisable()
    {
        EditorApplication.update -= DrainPendingLines;
    }

    private void DrainPendingLines()
    {
        bool gotAny = false;
        while (pendingLines.TryDequeue(out string line))
        {
            logBuilder.AppendLine(line);
            gotAny = true;
        }
        if (gotAny)
        {
            scroll.y = float.MaxValue; // auto-scroll to bottom
            Repaint();
        }
    }

    private void OnGUI()
    {
        bool isRunning = manager != null && manager.IsRunning;

        using (new EditorGUI.DisabledScope(isRunning))
        {
            configPath = EditorGUILayout.TextField("Config Path", configPath);
            runId = EditorGUILayout.TextField("Run ID", runId);
            initializeFromRunId = EditorGUILayout.TextField("Initialize From (optional)", initializeFromRunId);
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(isRunning || string.IsNullOrWhiteSpace(runId)))
            {
                if (GUILayout.Button("Start Training", GUILayout.Height(28)))
                    StartTraining();
            }
            using (new EditorGUI.DisabledScope(!isRunning))
            {
                if (GUILayout.Button("Stop (graceful)", GUILayout.Height(28)))
                {
                    manager.RequestGracefulStop();
                    stopRequestedAt = EditorApplication.timeSinceStartup;
                    logBuilder.AppendLine("[MLTrainerWindow] Graceful stop requested - waiting for checkpoint save...");
                }
            }
        }

        // Force-kill only offered once a graceful stop has had a real chance to work - not an
        // instant option, so it can't be reached for by habit and skip the checkpoint save.
        if (isRunning && stopRequestedAt > 0 && EditorApplication.timeSinceStartup - stopRequestedAt > 30)
        {
            EditorGUILayout.HelpBox("Graceful stop has been pending over 30s - the process may be hung.", MessageType.Warning);
            if (GUILayout.Button("Force Kill (skips checkpoint save)"))
            {
                manager.ForceKill();
                stopRequestedAt = -1;
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(isRunning ? "Status: running" : "Status: idle");

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.SelectableLabel(logBuilder.ToString(), EditorStyles.textArea, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void StartTraining()
    {
        logBuilder.Clear();
        stopRequestedAt = -1;
        string repoRoot = Directory.GetParent(Application.dataPath).FullName;

        manager = new MLTrainerProcessManager();
        manager.OnOutputLine += line => pendingLines.Enqueue(line);
        manager.OnExited += exitCode =>
        {
            pendingLines.Enqueue($"[MLTrainerWindow] Process exited (code {exitCode}).");
        };

        string initFrom = string.IsNullOrWhiteSpace(initializeFromRunId) ? null : initializeFromRunId.Trim();
        manager.Start(repoRoot, configPath, runId.Trim(), initFrom);
        logBuilder.AppendLine($"[MLTrainerWindow] Starting: {configPath} --run-id={runId}" + (initFrom != null ? $" --initialize-from={initFrom}" : ""));
    }
}
