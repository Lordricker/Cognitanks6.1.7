using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Debug = UnityEngine.Debug;

/// <summary>
/// Manages mlagents-learn as a subprocess launched from Unity (BottomUpAgentPlan.md Section 13
/// Phase 3 - "in-editor Train flow"), replacing the manual run_training.ps1 + separate-terminal
/// workflow this whole Phase 1 spike used by hand.
///
/// Not Editor-only on purpose (no UnityEditor references) - System.Diagnostics.Process management
/// itself doesn't need the Editor, only the UI wrapping it does (see the Editor/ window). Keeping
/// this class outside an Editor/ folder means it can be reused for Phase 4's eventual player-facing
/// "train an ML version" flow without moving it later.
///
/// Launches MLTraining/mlagents_launcher.py via the venv's python.exe rather than
/// mlagents-learn.exe directly, specifically for that script's graceful-stop mechanism (a polled
/// stop-file that raises a real KeyboardInterrupt via _thread.interrupt_main() - see that file's
/// own header comment for why this was chosen over Process.Kill() or Windows console-signal APIs).
/// </summary>
public class MLTrainerProcessManager
{
    public event Action<string> OnOutputLine;
    public event Action<int> OnExited;

    public bool IsRunning => process != null && !process.HasExited;

    private Process process;
    private string stopFilePath;

    /// <summary>
    /// Starts training. repoRoot is the project root (Application.dataPath's parent in a real
    /// caller); configPath/runId/initializeFromRunId are the same arguments run_training.ps1 has
    /// taken by hand all session.
    /// </summary>
    public void Start(string repoRoot, string configPath, string runId, string initializeFromRunId = null)
    {
        if (IsRunning)
        {
            Debug.LogWarning("[MLTrainerProcessManager] Start called while already running - ignoring.");
            return;
        }

        string pythonExe = Path.Combine(repoRoot, "MLTraining", "venv", "Scripts", "python.exe");
        string launcherScript = Path.Combine(repoRoot, "MLTraining", "mlagents_launcher.py");
        if (!File.Exists(pythonExe))
        {
            Debug.LogError($"[MLTrainerProcessManager] python.exe not found at {pythonExe} - is the venv set up?");
            return;
        }
        if (!File.Exists(launcherScript))
        {
            Debug.LogError($"[MLTrainerProcessManager] Launcher script not found at {launcherScript}.");
            return;
        }

        string logDir = Path.Combine(repoRoot, "MLTraining", "logs");
        Directory.CreateDirectory(logDir);
        stopFilePath = Path.Combine(logDir, $".stop-{runId}-{DateTime.Now:yyyyMMdd-HHmmss}");
        if (File.Exists(stopFilePath))
            File.Delete(stopFilePath);

        var argsBuilder = new StringBuilder();
        argsBuilder.Append($"\"{launcherScript}\" \"{configPath}\" --run-id={runId}");
        if (!string.IsNullOrEmpty(initializeFromRunId))
            argsBuilder.Append($" --initialize-from={initializeFromRunId}");

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = argsBuilder.ToString(),
            WorkingDirectory = repoRoot, // results/ lands relative to this, matching every run this session
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.EnvironmentVariables["MLAGENTS_STOP_FILE"] = stopFilePath;

        process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (s, e) => { if (e.Data != null) OnOutputLine?.Invoke(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) OnOutputLine?.Invoke(e.Data); };
        process.Exited += (s, e) => OnExited?.Invoke(process.ExitCode);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    /// <summary>
    /// Requests a graceful stop (creates the stop file the launcher script is polling for) and
    /// returns immediately - the process exits asynchronously once mlagents-learn's own
    /// KeyboardInterrupt handling finishes (checkpoint save, graph write). Callers should watch
    /// OnExited rather than assuming the process is gone when this returns.
    /// </summary>
    public void RequestGracefulStop()
    {
        if (!IsRunning || string.IsNullOrEmpty(stopFilePath))
            return;
        File.WriteAllText(stopFilePath, "stop");
    }

    /// <summary>
    /// Last-resort hard kill - skips the finally-block checkpoint save entirely. Only call this
    /// after RequestGracefulStop() has had a real chance to work (the caller should apply its own
    /// timeout, e.g. via ScheduleWakeup-equivalent polling on IsRunning) and the process is still
    /// not exiting, e.g. it's genuinely hung.
    ///
    /// Plain Kill(), not the newer Kill(entireProcessTree: true) overload - that one isn't
    /// guaranteed present under every API compatibility level Unity might be configured for, and
    /// this is a rarely-hit safety-net path, not worth a build-breaking risk over. Known limitation:
    /// only kills this direct process, not any subprocess env workers mlagents-learn may have
    /// spawned - acceptable for a force-kill path that shouldn't be hit often, but worth knowing if
    /// you ever see an orphaned python process lingering after using this.
    /// </summary>
    public void ForceKill()
    {
        if (!IsRunning)
            return;
        try
        {
            process.Kill();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MLTrainerProcessManager] ForceKill failed: {ex.Message}");
        }
    }
}
