"""
Canonical entry point for launching mlagents-learn as a managed subprocess
(BottomUpAgentPlan.md Section 13 Phase 3 - "in-editor Train flow", 2026-09-03).

Used two ways:
  1. Directly via the venv's python.exe, invoked by MLTrainerProcessManager.cs (Unity Editor tool)
     instead of mlagents-learn.exe, so the graceful-stop mechanism below is available.
  2. As the PyInstaller freeze entry point (Section 11 Risk 2) - replaces the throwaway
     scratchpad/mlagents_freeze_entry.py version once the bundled-trainer build actually happens.

Graceful stop, not a hard kill: MLTrainerProcessManager sets the MLAGENTS_STOP_FILE environment
variable to a path before launching, then creates that file to request a stop. A background thread
here polls for it and calls _thread.interrupt_main(), which raises a real KeyboardInterrupt in the
main thread - the exact same mechanism a real Ctrl+C uses on any platform. This deliberately avoids
Process.Kill() (skips the finally-block checkpoint save entirely) and avoids Windows console
control-event APIs (GenerateConsoleCtrlEvent/process groups - fragile, hard to verify reliably
reaches Python's KeyboardInterrupt handling rather than a different, uncaught signal like SIGBREAK).
"""
import multiprocessing
import os
import threading
import time


def _watch_for_stop_file():
    stop_file = os.environ.get("MLAGENTS_STOP_FILE")
    if not stop_file:
        return
    while True:
        if os.path.exists(stop_file):
            import _thread
            _thread.interrupt_main()
            return
        time.sleep(0.5)


if __name__ == "__main__":
    # Required on Windows for any frozen build that uses multiprocessing (mlagents does, for its
    # subprocess environment workers) - harmless when run unfrozen via python.exe too.
    multiprocessing.freeze_support()

    watcher = threading.Thread(target=_watch_for_stop_file, daemon=True)
    watcher.start()

    from mlagents.trainers.learn import main
    main()
