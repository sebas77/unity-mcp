using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using MCPForUnity.Runtime.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenCode.UnityUI.Editor
{
    [McpForUnityTool(
        "ugui_capture_matrix",
        Description = "Capture exact Play-mode Game view resolutions including Screen Space Overlay UI, with bounded polling and Game-view restoration.",
        Group = "ui",
        RequiresPolling = true,
        PollAction = "status",
        MaxPollSeconds = 120)]
    public static class CaptureUGUIMatrix
    {
        public sealed class Parameters
        {
            [ToolParameter("Action: start, status, or cancel.", Required = false, DefaultValue = "start")]
            public string action { get; set; }

            [ToolParameter("One through twelve exact width/height resolutions for start.", Required = false)]
            public ResolutionParameter[] resolutions { get; set; }

            [ToolParameter("Optional absolute output folder or path relative to the Unity project root.", Required = false)]
            public string output_folder { get; set; }

            [ToolParameter("Sanitized file prefix.", Required = false, DefaultValue = "ugui")]
            public string file_prefix { get; set; }

            [ToolParameter("Rendered frames to wait after selecting each size, from 1 through 10.", Required = false, DefaultValue = "2")]
            public int? frames_to_wait { get; set; }
        }

        public sealed class ResolutionParameter
        {
            public int width { get; set; }
            public int height { get; set; }
            public string label { get; set; }
        }

        [Serializable]
        public sealed class CaptureRecord
        {
            public string label;
            public int width;
            public int height;
            public string path;
        }

        [Serializable]
        public sealed class JobState
        {
            public string job_id;
            public string status;
            public string error;
            public int current_index;
            public int total;
            public bool restored;
            public List<CaptureRecord> captures = new List<CaptureRecord>();
        }

        sealed class ActiveJob
        {
            public JobState State;
            public GameViewStateSnapshot Snapshot;
            public GameViewResolution[] Resolutions;
            public string OutputFolder;
            public string FilePrefix;
            public int FramesToWait;
            public int RemainingFrames;
            public double Deadline;
            public bool CapturePending;
            public GameViewSizeController Controller;
        }

        static CaptureUGUIMatrix()
        {
            AssemblyReloadEvents.beforeAssemblyReload += AbortBeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += AbortBeforeReload;
        }

        public static object HandleCommand(JObject @params)
        {
            try
            {
                Parameters parameters = @params == null ? new Parameters() : @params.ToObject<Parameters>();
                string action = string.IsNullOrWhiteSpace(parameters.action) ? "start" : parameters.action.ToLowerInvariant();
                switch (action)
                {
                    case "start": return Start(parameters);
                    case "status": return Status();
                    case "cancel": return Cancel();
                    default: return new ErrorResponse($"Unknown action '{parameters.action}'. Allowed: start, status, cancel.");
                }
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                return new ErrorResponse($"uGUI capture matrix failed: {exception.Message}");
            }
        }

        static object Start(Parameters parameters)
        {
            if (Application.isPlaying == false)
                return new ErrorResponse("ugui_capture_matrix start is Play-mode-only.");
            if (_activeJob != null)
                return new ErrorResponse($"Capture job '{_activeJob.State.job_id}' is already running.");
            if (parameters.resolutions == null || parameters.resolutions.Length < 1 || parameters.resolutions.Length > 12)
                return new ErrorResponse("resolutions must contain between 1 and 12 entries.");

            int framesToWait = parameters.frames_to_wait ?? 2;
            if (framesToWait < 1 || framesToWait > 10)
                return new ErrorResponse("frames_to_wait must be between 1 and 10.");
            GameViewResolution[] resolutions = parameters.resolutions.Select((resolution, index) => ValidateResolution(resolution, index)).ToArray();
            string outputFolder = ResolveOutputFolder(parameters.output_folder);
            string prefix = Sanitize(string.IsNullOrWhiteSpace(parameters.file_prefix) ? "ugui" : parameters.file_prefix);
            Directory.CreateDirectory(outputFolder);

            string jobId = Guid.NewGuid().ToString("N");
            var controller = new GameViewSizeController();
            GameViewStateSnapshot snapshot = controller.CaptureState();
            try
            {
                foreach (GameViewResolution resolution in resolutions)
                    controller.EnsureResolution(snapshot, resolution, jobId);
            }
            catch
            {
                controller.Restore(snapshot);
                throw;
            }

            var state = new JobState
            {
                job_id = jobId,
                status = "pending",
                total = resolutions.Length
            };
            _activeJob = new ActiveJob
            {
                State = state,
                Snapshot = snapshot,
                Resolutions = resolutions,
                OutputFolder = outputFolder,
                FilePrefix = prefix,
                FramesToWait = framesToWait,
                RemainingFrames = framesToWait,
                Deadline = EditorApplication.timeSinceStartup + 120d,
                Controller = controller
            };
            SelectCurrentResolution();
            McpJobStateStore.SaveState(ToolName, state);
            EditorApplication.update += Update;
            return new PendingResponse("uGUI capture matrix started.", 0.25d, state);
        }

        static object Status()
        {
            JobState state = _activeJob?.State ?? McpJobStateStore.LoadState<JobState>(ToolName);
            if (state == null)
                return new ErrorResponse("No uGUI capture matrix job exists.");
            if (state.status == "completed")
                return new { _mcp_status = "complete", message = "uGUI capture matrix completed.", data = state };
            if (state.status == "error" || state.status == "cancelled")
                return new { _mcp_status = "error", error = state.error ?? state.status, data = state };
            return new PendingResponse($"Captured {state.current_index} of {state.total} resolutions.", 0.25d, state);
        }

        static object Cancel()
        {
            if (_activeJob == null)
                return new ErrorResponse("No uGUI capture matrix job is running.");
            Fail("Capture cancelled by caller.", "cancelled");
            return new SuccessResponse("Cancelled uGUI capture matrix and restored Game-view state.");
        }

        static void Update()
        {
            if (_activeJob == null)
            {
                EditorApplication.update -= Update;
                return;
            }
            if (EditorApplication.timeSinceStartup >= _activeJob.Deadline)
            {
                Fail("Capture exceeded the 120-second deadline.");
                return;
            }
            if (_activeJob.CapturePending)
                return;
            if (_activeJob.RemainingFrames > 0)
            {
                _activeJob.RemainingFrames--;
                return;
            }

            _activeJob.CapturePending = true;
            ScreenshotCapturer.Begin(1, OnCaptured);
        }

        static void OnCaptured(Texture2D texture)
        {
            if (_activeJob == null)
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
                return;
            }

            try
            {
                GameViewResolution resolution = _activeJob.Resolutions[_activeJob.State.current_index];
                if (texture == null)
                    throw new InvalidOperationException("ScreenCapture.CaptureScreenshotAsTexture returned null.");
                if (texture.width != resolution.Width || texture.height != resolution.Height)
                {
                    throw new InvalidOperationException(
                        $"Requested {resolution.Width}x{resolution.Height}, but Unity captured {texture.width}x{texture.height}.");
                }

                string fileName = $"{_activeJob.FilePrefix}-{Sanitize(resolution.Label)}-{resolution.Width}x{resolution.Height}.png";
                string fullPath = EnsureUnique(Path.Combine(_activeJob.OutputFolder, fileName));
                File.WriteAllBytes(fullPath, texture.EncodeToPNG());
                _activeJob.State.captures.Add(new CaptureRecord
                {
                    label = resolution.Label,
                    width = texture.width,
                    height = texture.height,
                    path = fullPath.Replace('\\', '/')
                });
                _activeJob.State.current_index++;
                _activeJob.CapturePending = false;
                McpJobStateStore.SaveState(ToolName, _activeJob.State);

                if (_activeJob.State.current_index >= _activeJob.Resolutions.Length)
                {
                    Complete();
                    return;
                }
                SelectCurrentResolution();
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }
        }

        static void SelectCurrentResolution()
        {
            GameViewResolution resolution = _activeJob.Resolutions[_activeJob.State.current_index];
            int index = _activeJob.Controller.FindSizeIndex(resolution.Width, resolution.Height);
            if (index < 0)
                throw new InvalidOperationException($"Game view size {resolution.Width}x{resolution.Height} is unavailable.");
            _activeJob.Controller.SelectSize(index);
            _activeJob.RemainingFrames = _activeJob.FramesToWait;
            _activeJob.CapturePending = false;
        }

        static void Complete()
        {
            ActiveJob job = _activeJob;
            RestoreActiveJob(job);
            job.State.status = "completed";
            job.State.restored = true;
            McpJobStateStore.SaveState(ToolName, job.State);
            _activeJob = null;
        }

        static void Fail(string message, string status = "error")
        {
            if (_activeJob == null)
                return;
            ActiveJob job = _activeJob;
            try
            {
                RestoreActiveJob(job);
                job.State.restored = true;
            }
            catch (Exception restoreException)
            {
                message += $" Game-view restoration also failed: {restoreException.Message}";
                job.State.restored = false;
            }
            job.State.status = status;
            job.State.error = message;
            McpJobStateStore.SaveState(ToolName, job.State);
            _activeJob = null;
        }

        static void RestoreActiveJob(ActiveJob job)
        {
            EditorApplication.update -= Update;
            job.Controller.Restore(job.Snapshot);
        }

        static void AbortBeforeReload()
        {
            if (_activeJob != null)
                Fail("Capture interrupted by Editor reload or shutdown.");
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && _activeJob != null)
                Fail("Capture interrupted because Play Mode is exiting.");
        }

        static GameViewResolution ValidateResolution(ResolutionParameter resolution, int index)
        {
            if (resolution == null)
                throw new ArgumentException($"resolutions[{index}] cannot be null.");
            if (resolution.width < 1 || resolution.width > 8192 || resolution.height < 1 || resolution.height > 8192)
                throw new ArgumentException($"resolutions[{index}] width and height must be between 1 and 8192.");
            return new GameViewResolution
            {
                Width = resolution.width,
                Height = resolution.height,
                Label = string.IsNullOrWhiteSpace(resolution.label) ? $"view-{index + 1}" : resolution.label.Trim()
            };
        }

        static string ResolveOutputFolder(string requested)
        {
            if (string.IsNullOrWhiteSpace(requested))
                return Path.Combine(Application.temporaryCachePath, "OpenCodeUnityUI");
            if (Path.IsPathRooted(requested))
                return Path.GetFullPath(requested);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, requested));
        }

        static string Sanitize(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string cleaned = new string(value.Select(character => invalid.Contains(character) || character == '/' || character == '\\' ? '_' : character).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "capture" : cleaned;
        }

        static string EnsureUnique(string path)
        {
            if (File.Exists(path) == false)
                return path;
            string directory = Path.GetDirectoryName(path);
            string baseName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int suffix = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
                suffix++;
            } while (File.Exists(candidate));
            return candidate;
        }

        const string ToolName = "ugui_capture_matrix";
        static ActiveJob _activeJob;
    }
}
