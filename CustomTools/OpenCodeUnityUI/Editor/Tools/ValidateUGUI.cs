using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenCode.UnityUI.Editor
{
    [McpForUnityTool(
        "ugui_validate",
        Description = "Validate uGUI screen bounds, clipping, dimensions, TMP overflow, buttons, EventSystems, and optional sibling overlap. Read-only.",
        Group = "ui")]
    public static class ValidateUGUI
    {
        public sealed class Parameters
        {
            [ToolParameter("Optional root selector: hierarchy path, exact name, or instance ID.", Required = false)]
            public string root { get; set; }

            [ToolParameter("Optional selectors to validate.", Required = false)]
            public string[] targets { get; set; }

            [ToolParameter("Checks: off_screen, parent_clipping, zero_size, text_overflow, button_readiness, event_system_count, sibling_overlap.", Required = false)]
            public string[] checks { get; set; }

            [ToolParameter("Include inactive scene objects.", Required = false, DefaultValue = "true")]
            public bool? include_inactive { get; set; }

            [ToolParameter("Maximum descendant depth, from 0 through 50.", Required = false, DefaultValue = "10")]
            public int? max_depth { get; set; }

            [ToolParameter("Minimum severity: info, warning, or error.", Required = false, DefaultValue = "info")]
            public string severity { get; set; }

            [ToolParameter("Maximum returned findings, from 1 through 500.", Required = false, DefaultValue = "100")]
            public int? page_size { get; set; }

            [ToolParameter("Zero-based paging cursor.", Required = false, DefaultValue = "0")]
            public int? cursor { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            try
            {
                Parameters parameters = @params == null ? new Parameters() : @params.ToObject<Parameters>();
                int maxDepth = parameters.max_depth ?? 10;
                int pageSize = parameters.page_size ?? 100;
                int cursor = parameters.cursor ?? 0;
                if (maxDepth < 0 || maxDepth > 50)
                    return new ErrorResponse("max_depth must be between 0 and 50.");
                if (pageSize < 1 || pageSize > 500)
                    return new ErrorResponse("page_size must be between 1 and 500.");
                if (cursor < 0)
                    return new ErrorResponse("cursor cannot be negative.");

                HashSet<string> checks = ParseChecks(parameters.checks);
                int minimumSeverity = ParseSeverity(parameters.severity ?? "info");
                IReadOnlyList<RectTransform> rects = UGUIObjectResolver.Resolve(
                    parameters.root,
                    parameters.targets,
                    parameters.include_inactive ?? true,
                    maxDepth);
                List<UGUIFinding> findings = BuildFindings(rects, checks)
                    .Where(finding => SeverityRank(finding.Severity) >= minimumSeverity)
                    .OrderBy(finding => SeverityRank(finding.Severity))
                    .ThenBy(finding => finding.Path, StringComparer.Ordinal)
                    .ThenBy(finding => finding.Code, StringComparer.Ordinal)
                    .ToList();

                UGUIFinding[] page = findings.Skip(cursor).Take(pageSize).ToArray();
                int next = cursor + page.Length;
                var result = new UGUIValidationResult
                {
                    Total = findings.Count,
                    Cursor = cursor,
                    NextCursor = next < findings.Count ? next : (int?)null,
                    Summary = new
                    {
                        info = findings.Count(finding => finding.Severity == "info"),
                        warning = findings.Count(finding => finding.Severity == "warning"),
                        error = findings.Count(finding => finding.Severity == "error")
                    },
                    Findings = page
                };
                return new SuccessResponse($"Validation found {findings.Count} issue(s).", result);
            }
            catch (UGUIResolutionException exception)
            {
                return new ErrorResponse(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return new ErrorResponse(exception.Message);
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"uGUI validation failed: {exception.Message}");
            }
        }

        static List<UGUIFinding> BuildFindings(IReadOnlyList<RectTransform> rects, ISet<string> checks)
        {
            var findings = new List<UGUIFinding>();
            foreach (RectTransform rect in rects)
            {
                UGUIScreenBounds bounds = UGUIBoundsUtility.GetScreenBounds(rect);
                if (checks.Contains("zero_size") && bounds.Valid && (bounds.Width <= 0f || bounds.Height <= 0f))
                {
                    findings.Add(Finding("zero_size", "error", rect, "RectTransform has a non-positive rendered width or height.", bounds));
                }

                if (checks.Contains("off_screen") && bounds.Valid)
                    AddOffScreenFinding(rect, bounds, findings);

                if (checks.Contains("parent_clipping") && bounds.Valid)
                {
                    UGUIScreenBounds clipBounds = UGUIBoundsUtility.GetClippingBounds(rect);
                    if (clipBounds != null && UGUIBoundsUtility.Contains(clipBounds, bounds) == false)
                        findings.Add(Finding("parent_clipped", "warning", rect, "Rendered bounds extend outside an ancestor Mask or RectMask2D.", bounds));
                }

                if (checks.Contains("text_overflow"))
                {
                    TMP_Text text = rect.GetComponent<TMP_Text>();
                    if (text != null && text.isTextOverflowing)
                        findings.Add(Finding("text_overflow", "warning", rect, "TMP text reports overflow in its current rendered state.", bounds, text.GetType().Name));
                }

                if (checks.Contains("button_readiness"))
                    AddButtonFindings(rect, bounds, findings);
            }

            if (checks.Contains("event_system_count"))
                AddEventSystemFinding(findings);
            if (checks.Contains("sibling_overlap"))
                AddSiblingOverlapFindings(rects, findings);
            return findings;
        }

        static void AddOffScreenFinding(RectTransform rect, UGUIScreenBounds bounds, ICollection<UGUIFinding> findings)
        {
            var screenBounds = new UGUIScreenBounds
            {
                Valid = true,
                MinX = 0f,
                MinY = 0f,
                MaxX = Screen.width,
                MaxY = Screen.height
            };
            if (UGUIBoundsUtility.Intersects(screenBounds, bounds) == false)
            {
                findings.Add(Finding("off_screen", "error", rect, "Rendered bounds are completely outside the Game view.", bounds));
                return;
            }
            if (UGUIBoundsUtility.Contains(screenBounds, bounds) == false)
                findings.Add(Finding("off_screen", "warning", rect, "Rendered bounds extend outside the Game view.", bounds));
        }

        static void AddButtonFindings(RectTransform rect, UGUIScreenBounds bounds, ICollection<UGUIFinding> findings)
        {
            Button button = rect.GetComponent<Button>();
            if (button == null || button.interactable == false)
                return;
            if (button.targetGraphic == null)
            {
                findings.Add(Finding("button_missing_target_graphic", "warning", rect, "Interactable Button has no target Graphic.", bounds, nameof(Button)));
            }
            else if (button.targetGraphic.raycastTarget == false)
            {
                findings.Add(Finding("button_target_not_raycastable", "warning", rect, "Interactable Button target Graphic does not receive raycasts.", bounds, nameof(Button)));
            }
            if (button.onClick.GetPersistentEventCount() == 0)
            {
                findings.Add(Finding(
                    "button_no_persistent_listener",
                    "info",
                    rect,
                    "Button has no persistent onClick listener. Runtime-added listeners are not observable here.",
                    bounds,
                    nameof(Button)));
            }
        }

        static void AddEventSystemFinding(ICollection<UGUIFinding> findings)
        {
            EventSystem[] eventSystems = Resources.FindObjectsOfTypeAll<EventSystem>()
                .Where(system => system.gameObject.scene.IsValid() && system.gameObject.scene.isLoaded)
                .Where(system => system.gameObject.activeInHierarchy)
                .ToArray();
            if (eventSystems.Length == 1)
                return;

            findings.Add(new UGUIFinding
            {
                Code = "event_system_count",
                Severity = "error",
                Message = $"Expected exactly one active EventSystem across loaded scenes; found {eventSystems.Length}."
            });
        }

        static void AddSiblingOverlapFindings(IReadOnlyList<RectTransform> rects, ICollection<UGUIFinding> findings)
        {
            foreach (IGrouping<Transform, RectTransform> siblings in rects
                         .Where(rect => rect.parent != null && rect.gameObject.activeInHierarchy)
                         .Where(rect => rect.GetComponent<Graphic>() != null)
                         .GroupBy(rect => rect.parent))
            {
                RectTransform[] items = siblings.OrderBy(rect => rect.GetSiblingIndex()).ToArray();
                for (int leftIndex = 0; leftIndex < items.Length; leftIndex++)
                {
                    UGUIScreenBounds leftBounds = UGUIBoundsUtility.GetScreenBounds(items[leftIndex]);
                    for (int rightIndex = leftIndex + 1; rightIndex < items.Length; rightIndex++)
                    {
                        UGUIScreenBounds rightBounds = UGUIBoundsUtility.GetScreenBounds(items[rightIndex]);
                        if (UGUIBoundsUtility.Intersects(leftBounds, rightBounds))
                        {
                            findings.Add(Finding(
                                "sibling_overlap",
                                "info",
                                items[leftIndex],
                                $"Graphic overlaps sibling '{UGUIObjectResolver.GetHierarchyPath(items[rightIndex])}'.",
                                leftBounds,
                                nameof(Graphic)));
                        }
                    }
                }
            }
        }

        static HashSet<string> ParseChecks(IReadOnlyList<string> requested)
        {
            string[] defaults =
            {
                "off_screen",
                "parent_clipping",
                "zero_size",
                "text_overflow",
                "button_readiness",
                "event_system_count"
            };
            var allowed = new HashSet<string>(defaults.Concat(new[] { "sibling_overlap" }), StringComparer.OrdinalIgnoreCase);
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string check in requested == null || requested.Count == 0 ? defaults : requested)
            {
                if (allowed.Contains(check) == false)
                    throw new ArgumentException($"Unknown check '{check}'. Allowed: {string.Join(", ", allowed.OrderBy(value => value))}.");
                result.Add(check);
            }
            return result;
        }

        static int ParseSeverity(string severity)
        {
            int rank = SeverityRank(severity.ToLowerInvariant());
            if (rank < 0)
                throw new ArgumentException("severity must be info, warning, or error.");
            return rank;
        }

        static int SeverityRank(string severity)
        {
            switch (severity)
            {
                case "info": return 0;
                case "warning": return 1;
                case "error": return 2;
                default: return -1;
            }
        }

        static UGUIFinding Finding(
            string code,
            string severity,
            RectTransform rect,
            string message,
            UGUIScreenBounds bounds,
            string component = nameof(RectTransform))
        {
            return new UGUIFinding
            {
                Code = code,
                Severity = severity,
                Path = UGUIObjectResolver.GetHierarchyPath(rect),
                Component = component,
                Message = message,
                ScreenBounds = bounds
            };
        }
    }
}
