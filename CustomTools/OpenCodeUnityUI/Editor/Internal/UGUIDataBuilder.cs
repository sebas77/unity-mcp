using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OpenCode.UnityUI.Editor
{
    public static class UGUIDataBuilder
    {
        public static UGUINodeData BuildNode(RectTransform rect, bool includeText)
        {
            Canvas canvas = rect.GetComponent<Canvas>();
            CanvasScaler scaler = rect.GetComponent<CanvasScaler>();
            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            LayoutGroup layoutGroup = rect.GetComponent<LayoutGroup>();
            ContentSizeFitter fitter = rect.GetComponent<ContentSizeFitter>();
            Graphic graphic = rect.GetComponent<Graphic>();
            Button button = rect.GetComponent<Button>();
            TMP_Text text = rect.GetComponent<TMP_Text>();

            return new UGUINodeData
            {
                Name = rect.name,
                Path = UGUIObjectResolver.GetHierarchyPath(rect),
                InstanceId = UGUIObjectId.GetLegacyId(rect.gameObject),
                EntityId = UGUIObjectId.GetSessionId(rect.gameObject),
                ParentPath = rect.parent == null ? null : UGUIObjectResolver.GetHierarchyPath(rect.parent),
                SiblingIndex = rect.GetSiblingIndex(),
                ActiveSelf = rect.gameObject.activeSelf,
                ActiveInHierarchy = rect.gameObject.activeInHierarchy,
                Components = rect.GetComponents<Component>()
                    .Where(component => component != null)
                    .Select(component => component.GetType().FullName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray(),
                RectTransform = BuildRectTransform(rect),
                ScreenBounds = UGUIBoundsUtility.GetScreenBounds(rect),
                Canvas = BuildCanvas(canvas),
                CanvasScaler = BuildCanvasScaler(scaler),
                LayoutElement = BuildLayoutElement(layoutElement),
                LayoutGroup = BuildLayoutGroup(layoutGroup),
                ContentSizeFitter = BuildContentSizeFitter(fitter),
                Graphic = BuildGraphic(graphic),
                Button = BuildButton(button),
                Text = BuildText(text, includeText)
            };
        }

        static object BuildRectTransform(RectTransform rect)
        {
            return new
            {
                anchor_min = Vector2Data(rect.anchorMin),
                anchor_max = Vector2Data(rect.anchorMax),
                pivot = Vector2Data(rect.pivot),
                anchored_position = Vector2Data(rect.anchoredPosition),
                size_delta = Vector2Data(rect.sizeDelta),
                local_scale = Vector3Data(rect.localScale),
                rect = new { x = rect.rect.x, y = rect.rect.y, width = rect.rect.width, height = rect.rect.height }
            };
        }

        static object BuildCanvas(Canvas canvas)
        {
            if (canvas == null)
                return null;
            return new
            {
                render_mode = canvas.renderMode.ToString(),
                sorting_order = canvas.sortingOrder,
                sorting_layer = canvas.sortingLayerName,
                target_display = canvas.targetDisplay,
                scale_factor = canvas.scaleFactor,
                override_sorting = canvas.overrideSorting,
                world_camera = canvas.worldCamera == null ? null : UGUIObjectResolver.GetHierarchyPath(canvas.worldCamera)
            };
        }

        static object BuildCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
                return null;
            return new
            {
                ui_scale_mode = scaler.uiScaleMode.ToString(),
                reference_resolution = Vector2Data(scaler.referenceResolution),
                screen_match_mode = scaler.screenMatchMode.ToString(),
                match_width_or_height = scaler.matchWidthOrHeight,
                reference_pixels_per_unit = scaler.referencePixelsPerUnit,
                scale_factor = scaler.scaleFactor
            };
        }

        static object BuildLayoutElement(LayoutElement element)
        {
            if (element == null)
                return null;
            return new
            {
                ignore_layout = element.ignoreLayout,
                min_width = element.minWidth,
                min_height = element.minHeight,
                preferred_width = element.preferredWidth,
                preferred_height = element.preferredHeight,
                flexible_width = element.flexibleWidth,
                flexible_height = element.flexibleHeight,
                layout_priority = element.layoutPriority
            };
        }

        static object BuildLayoutGroup(LayoutGroup group)
        {
            if (group == null)
                return null;

            object common = new
            {
                type = group.GetType().Name,
                padding = RectOffsetData(group.padding),
                child_alignment = group.childAlignment.ToString()
            };
            if (group is HorizontalOrVerticalLayoutGroup linear)
            {
                return new
                {
                    common,
                    spacing = linear.spacing,
                    child_control_width = linear.childControlWidth,
                    child_control_height = linear.childControlHeight,
                    child_force_expand_width = linear.childForceExpandWidth,
                    child_force_expand_height = linear.childForceExpandHeight,
                    child_scale_width = linear.childScaleWidth,
                    child_scale_height = linear.childScaleHeight
                };
            }

            if (group is GridLayoutGroup grid)
            {
                return new
                {
                    common,
                    cell_size = Vector2Data(grid.cellSize),
                    spacing = Vector2Data(grid.spacing),
                    start_corner = grid.startCorner.ToString(),
                    start_axis = grid.startAxis.ToString(),
                    constraint = grid.constraint.ToString(),
                    constraint_count = grid.constraintCount
                };
            }

            return common;
        }

        static object BuildContentSizeFitter(ContentSizeFitter fitter)
        {
            if (fitter == null)
                return null;
            return new
            {
                horizontal_fit = fitter.horizontalFit.ToString(),
                vertical_fit = fitter.verticalFit.ToString()
            };
        }

        static object BuildGraphic(Graphic graphic)
        {
            if (graphic == null)
                return null;
            return new
            {
                type = graphic.GetType().Name,
                enabled = graphic.enabled,
                raycast_target = graphic.raycastTarget,
                color = ColorData(graphic.color)
            };
        }

        static object BuildButton(Button button)
        {
            if (button == null)
                return null;
            return new
            {
                interactable = button.interactable,
                transition = button.transition.ToString(),
                target_graphic = button.targetGraphic == null
                    ? null
                    : UGUIObjectResolver.GetHierarchyPath(button.targetGraphic),
                persistent_listener_count = button.onClick.GetPersistentEventCount()
            };
        }

        static object BuildText(TMP_Text text, bool includeText)
        {
            if (text == null)
                return null;

            var serialized = new SerializedObject(text);
            SerializedProperty wrapping = serialized.FindProperty("m_enableWordWrapping");
            return new
            {
                type = text.GetType().Name,
                content = includeText ? text.text : null,
                character_count = text.textInfo == null ? 0 : text.textInfo.characterCount,
                font_size = text.fontSize,
                font_size_min = text.fontSizeMin,
                font_size_max = text.fontSizeMax,
                auto_sizing = text.enableAutoSizing,
                word_wrapping = wrapping != null && wrapping.boolValue,
                alignment = text.alignment.ToString(),
                overflow_mode = text.overflowMode.ToString(),
                margin = Vector4Data(text.margin),
                preferred_width = text.preferredWidth,
                preferred_height = text.preferredHeight,
                overflowing = text.isTextOverflowing
            };
        }

        static object Vector2Data(Vector2 value)
        {
            return new { x = value.x, y = value.y };
        }

        static object Vector3Data(Vector3 value)
        {
            return new { x = value.x, y = value.y, z = value.z };
        }

        static object Vector4Data(Vector4 value)
        {
            return new { x = value.x, y = value.y, z = value.z, w = value.w };
        }

        static object ColorData(Color value)
        {
            return new { r = value.r, g = value.g, b = value.b, a = value.a };
        }

        static object RectOffsetData(RectOffset value)
        {
            return new { left = value.left, right = value.right, top = value.top, bottom = value.bottom };
        }
    }
}
