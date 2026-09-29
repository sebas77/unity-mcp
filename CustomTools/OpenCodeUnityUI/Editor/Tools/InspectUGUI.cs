using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OpenCode.UnityUI.Editor
{
    [McpForUnityTool(
        "ugui_inspect",
        Description = "Inspect uGUI hierarchy objects, normalized layout/component data, and screen-space bounds. Read-only.",
        Group = "ui")]
    public static class InspectUGUI
    {
        public sealed class Parameters
        {
            [ToolParameter("Optional root selector: hierarchy path, exact name, or instance ID.", Required = false)]
            public string root { get; set; }

            [ToolParameter("Optional selectors to inspect. Each selector may be a hierarchy path, exact name, or instance ID.", Required = false)]
            public string[] targets { get; set; }

            [ToolParameter("Include inactive scene objects.", Required = false, DefaultValue = "true")]
            public bool? include_inactive { get; set; }

            [ToolParameter("Maximum descendant depth per selected root, from 0 through 50.", Required = false, DefaultValue = "10")]
            public int? max_depth { get; set; }

            [ToolParameter("Maximum returned nodes, from 1 through 500.", Required = false, DefaultValue = "100")]
            public int? page_size { get; set; }

            [ToolParameter("Zero-based paging cursor.", Required = false, DefaultValue = "0")]
            public int? cursor { get; set; }

            [ToolParameter("Include TMP text content. Metrics are always returned.", Required = false, DefaultValue = "false")]
            public bool? include_text { get; set; }
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

                IReadOnlyList<RectTransform> rects = UGUIObjectResolver.Resolve(
                    parameters.root,
                    parameters.targets,
                    parameters.include_inactive ?? true,
                    maxDepth);
                UGUINodeData[] allNodes = rects
                    .Select(rect => UGUIDataBuilder.BuildNode(rect, parameters.include_text ?? false))
                    .ToArray();
                UGUINodeData[] page = allNodes.Skip(cursor).Take(pageSize).ToArray();
                int next = cursor + page.Length;
                var result = new UGUIInspectionResult
                {
                    Total = allNodes.Length,
                    Cursor = cursor,
                    NextCursor = next < allNodes.Length ? next : (int?)null,
                    Screen = new { width = Screen.width, height = Screen.height },
                    Nodes = page
                };
                return new SuccessResponse($"Inspected {page.Length} of {allNodes.Length} uGUI objects.", result);
            }
            catch (UGUIResolutionException exception)
            {
                return new ErrorResponse(exception.Message);
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"uGUI inspection failed: {exception.Message}");
            }
        }
    }
}
