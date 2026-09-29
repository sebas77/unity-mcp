using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace OpenCode.UnityUI.Editor
{
    public sealed class UGUIScreenBounds
    {
        [JsonProperty("valid")]
        public bool Valid { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        [JsonProperty("min_x")]
        public float MinX { get; set; }

        [JsonProperty("min_y")]
        public float MinY { get; set; }

        [JsonProperty("max_x")]
        public float MaxX { get; set; }

        [JsonProperty("max_y")]
        public float MaxY { get; set; }

        [JsonProperty("width")]
        public float Width => MaxX - MinX;

        [JsonProperty("height")]
        public float Height => MaxY - MinY;
    }

    public static class UGUIBoundsUtility
    {
        public static UGUIScreenBounds GetScreenBounds(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = ResolveCamera(canvas);
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && camera == null)
            {
                return new UGUIScreenBounds
                {
                    Valid = false,
                    Error = $"Canvas '{UGUIObjectResolver.GetHierarchyPath(canvas)}' needs a render camera for screen bounds."
                };
            }

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            return new UGUIScreenBounds
            {
                Valid = true,
                MinX = minX,
                MinY = minY,
                MaxX = maxX,
                MaxY = maxY
            };
        }

        public static UGUIScreenBounds GetClippingBounds(RectTransform rect)
        {
            Transform current = rect.parent;
            while (current != null)
            {
                if (current.GetComponent<RectMask2D>() != null || current.GetComponent<Mask>() != null)
                    return GetScreenBounds((RectTransform)current);
                current = current.parent;
            }

            return null;
        }

        public static bool Intersects(UGUIScreenBounds left, UGUIScreenBounds right)
        {
            return left.Valid && right.Valid &&
                   left.MinX < right.MaxX && left.MaxX > right.MinX &&
                   left.MinY < right.MaxY && left.MaxY > right.MinY;
        }

        public static bool Contains(UGUIScreenBounds outer, UGUIScreenBounds inner)
        {
            return outer.Valid && inner.Valid &&
                   inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX &&
                   inner.MinY >= outer.MinY && inner.MaxY <= outer.MaxY;
        }

        static Camera ResolveCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }
    }
}
