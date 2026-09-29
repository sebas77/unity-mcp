using System.Collections.Generic;
using Newtonsoft.Json;

namespace OpenCode.UnityUI.Editor
{
    public sealed class UGUIInspectionResult
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("cursor")]
        public int Cursor { get; set; }

        [JsonProperty("next_cursor")]
        public int? NextCursor { get; set; }

        [JsonProperty("screen")]
        public object Screen { get; set; }

        [JsonProperty("nodes")]
        public IReadOnlyList<UGUINodeData> Nodes { get; set; }
    }

    public sealed class UGUINodeData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("instance_id")]
        public int InstanceId { get; set; }

        [JsonProperty("entity_id")]
        public string EntityId { get; set; }

        [JsonProperty("parent_path")]
        public string ParentPath { get; set; }

        [JsonProperty("sibling_index")]
        public int SiblingIndex { get; set; }

        [JsonProperty("active_self")]
        public bool ActiveSelf { get; set; }

        [JsonProperty("active_in_hierarchy")]
        public bool ActiveInHierarchy { get; set; }

        [JsonProperty("components")]
        public IReadOnlyList<string> Components { get; set; }

        [JsonProperty("rect_transform")]
        public object RectTransform { get; set; }

        [JsonProperty("screen_bounds")]
        public UGUIScreenBounds ScreenBounds { get; set; }

        [JsonProperty("canvas", NullValueHandling = NullValueHandling.Ignore)]
        public object Canvas { get; set; }

        [JsonProperty("canvas_scaler", NullValueHandling = NullValueHandling.Ignore)]
        public object CanvasScaler { get; set; }

        [JsonProperty("layout_element", NullValueHandling = NullValueHandling.Ignore)]
        public object LayoutElement { get; set; }

        [JsonProperty("layout_group", NullValueHandling = NullValueHandling.Ignore)]
        public object LayoutGroup { get; set; }

        [JsonProperty("content_size_fitter", NullValueHandling = NullValueHandling.Ignore)]
        public object ContentSizeFitter { get; set; }

        [JsonProperty("graphic", NullValueHandling = NullValueHandling.Ignore)]
        public object Graphic { get; set; }

        [JsonProperty("button", NullValueHandling = NullValueHandling.Ignore)]
        public object Button { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public object Text { get; set; }
    }

    public sealed class UGUIValidationResult
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("cursor")]
        public int Cursor { get; set; }

        [JsonProperty("next_cursor")]
        public int? NextCursor { get; set; }

        [JsonProperty("summary")]
        public object Summary { get; set; }

        [JsonProperty("findings")]
        public IReadOnlyList<UGUIFinding> Findings { get; set; }
    }

    public sealed class UGUIFinding
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("severity")]
        public string Severity { get; set; }

        [JsonProperty("path", NullValueHandling = NullValueHandling.Ignore)]
        public string Path { get; set; }

        [JsonProperty("component", NullValueHandling = NullValueHandling.Ignore)]
        public string Component { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("screen_bounds", NullValueHandling = NullValueHandling.Ignore)]
        public UGUIScreenBounds ScreenBounds { get; set; }
    }
}
