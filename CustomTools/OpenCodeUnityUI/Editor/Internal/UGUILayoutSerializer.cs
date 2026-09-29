using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OpenCode.UnityUI.Editor
{
    public static class UGUILayoutSerializer
    {
        public static Component ResolveSupportedComponent(GameObject target, string componentName)
        {
            switch (componentName)
            {
                case "RectTransform": return target.GetComponent<RectTransform>();
                case "LayoutElement": return target.GetComponent<LayoutElement>();
                case "HorizontalLayoutGroup": return target.GetComponent<HorizontalLayoutGroup>();
                case "VerticalLayoutGroup": return target.GetComponent<VerticalLayoutGroup>();
                case "GridLayoutGroup": return target.GetComponent<GridLayoutGroup>();
                case "ContentSizeFitter": return target.GetComponent<ContentSizeFitter>();
                case "Graphic": return target.GetComponent<Graphic>();
                case "Image": return target.GetComponent<Image>();
                case "Selectable": return target.GetComponent<Selectable>();
                case "Button": return target.GetComponent<Button>();
                case "TMP_Text": return target.GetComponent<TMP_Text>();
                case "TextMeshProUGUI": return target.GetComponent<TextMeshProUGUI>();
                default: throw new ArgumentException($"Unsupported preview component '{componentName}'.");
            }
        }

        public static void ValidateProperties(Component component, JObject properties)
        {
            if (properties == null || properties.HasValues == false)
                throw new ArgumentException("set_properties requires a non-empty properties object.");

            HashSet<string> allowed = GetAllowedProperties(component);
            foreach (JProperty property in properties.Properties())
            {
                if (allowed.Contains(property.Name) == false)
                    throw new ArgumentException($"Property '{property.Name}' is unsupported for {component.GetType().Name}.");
                ValidateValue(component, property.Name, property.Value);
            }
        }

        public static void ApplyProperties(Component component, JObject properties)
        {
            if (component is RectTransform rect)
                ApplyRectTransform(rect, properties);
            else if (component is LayoutElement element)
                ApplyLayoutElement(element, properties);
            else if (component is HorizontalOrVerticalLayoutGroup linear)
                ApplyLinearLayout(linear, properties);
            else if (component is GridLayoutGroup grid)
                ApplyGridLayout(grid, properties);
            else if (component is ContentSizeFitter fitter)
                ApplyContentSizeFitter(fitter, properties);
            else if (component is TMP_Text text)
                ApplyText(text, properties);
            else if (component is Graphic graphic)
                ApplyGraphic(graphic, properties);
            else if (component is Selectable selectable)
                ApplySelectable(selectable, properties);
            else
                throw new ArgumentException($"Unsupported preview component '{component.GetType().Name}'.");
        }

        static HashSet<string> GetAllowedProperties(Component component)
        {
            if (component is RectTransform)
                return Set("anchor_min", "anchor_max", "pivot", "anchored_position", "size_delta", "local_scale");
            if (component is LayoutElement)
                return Set("ignore_layout", "min_width", "min_height", "preferred_width", "preferred_height", "flexible_width", "flexible_height", "layout_priority");
            if (component is HorizontalOrVerticalLayoutGroup)
            {
                return Set(
                    "padding", "spacing", "child_alignment", "child_control_width", "child_control_height",
                    "child_force_expand_width", "child_force_expand_height", "child_scale_width", "child_scale_height",
                    "reverse_arrangement");
            }
            if (component is GridLayoutGroup)
                return Set("padding", "child_alignment", "cell_size", "spacing", "start_corner", "start_axis", "constraint", "constraint_count");
            if (component is ContentSizeFitter)
                return Set("horizontal_fit", "vertical_fit");
            if (component is TMP_Text)
            {
                return Set(
                    "enabled", "margin", "font_size", "font_size_min", "font_size_max", "auto_sizing",
                    "word_wrapping", "alignment", "overflow_mode", "raycast_target", "color");
            }
            if (component is Graphic)
                return Set("enabled", "raycast_target", "color");
            if (component is Selectable)
                return Set("interactable");
            throw new ArgumentException($"Unsupported preview component '{component.GetType().Name}'.");
        }

        static void ValidateValue(Component component, string name, JToken value)
        {
            if (name == "anchor_min" || name == "anchor_max" || name == "pivot" || name == "anchored_position" ||
                name == "size_delta" || name == "cell_size" || component is GridLayoutGroup && name == "spacing")
            {
                ReadVector2(value, name);
                return;
            }
            if (name == "local_scale")
            {
                ReadVector3(value, name);
                return;
            }
            if (name == "margin" || name == "color")
            {
                ReadVector4(value, name);
                return;
            }
            if (name == "padding")
            {
                ReadRectOffset(value);
                return;
            }
            if (name == "ignore_layout" || name == "child_control_width" || name == "child_control_height" ||
                name == "child_force_expand_width" || name == "child_force_expand_height" || name == "child_scale_width" ||
                name == "child_scale_height" || name == "reverse_arrangement" || name == "enabled" ||
                name == "auto_sizing" || name == "word_wrapping" || name == "raycast_target" || name == "interactable")
            {
                value.Value<bool>();
                return;
            }
            if (name == "layout_priority" || name == "constraint_count")
            {
                value.Value<int>();
                return;
            }
            if (name == "min_width" || name == "min_height" || name == "preferred_width" || name == "preferred_height" ||
                name == "flexible_width" || name == "flexible_height" || name == "font_size" || name == "font_size_min" ||
                name == "font_size_max" || name == "spacing")
            {
                value.Value<float>();
                return;
            }

            string enumValue = value.Value<string>();
            if (component is LayoutGroup && name == "child_alignment")
                ParseEnum<TextAnchor>(enumValue, name);
            else if (component is GridLayoutGroup && name == "start_corner")
                ParseEnum<GridLayoutGroup.Corner>(enumValue, name);
            else if (component is GridLayoutGroup && name == "start_axis")
                ParseEnum<GridLayoutGroup.Axis>(enumValue, name);
            else if (component is GridLayoutGroup && name == "constraint")
                ParseEnum<GridLayoutGroup.Constraint>(enumValue, name);
            else if (component is ContentSizeFitter && (name == "horizontal_fit" || name == "vertical_fit"))
                ParseEnum<ContentSizeFitter.FitMode>(enumValue, name);
            else if (component is TMP_Text && name == "alignment")
                ParseEnum<TextAlignmentOptions>(enumValue, name);
            else if (component is TMP_Text && name == "overflow_mode")
                ParseEnum<TextOverflowModes>(enumValue, name);
            else
                throw new ArgumentException($"Unsupported property '{name}'.");
        }

        static void ApplyRectTransform(RectTransform rect, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                switch (property.Name)
                {
                    case "anchor_min": rect.anchorMin = ReadVector2(property.Value, property.Name); break;
                    case "anchor_max": rect.anchorMax = ReadVector2(property.Value, property.Name); break;
                    case "pivot": rect.pivot = ReadVector2(property.Value, property.Name); break;
                    case "anchored_position": rect.anchoredPosition = ReadVector2(property.Value, property.Name); break;
                    case "size_delta": rect.sizeDelta = ReadVector2(property.Value, property.Name); break;
                    case "local_scale": rect.localScale = ReadVector3(property.Value, property.Name); break;
                }
            }
        }

        static void ApplyLayoutElement(LayoutElement element, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                switch (property.Name)
                {
                    case "ignore_layout": element.ignoreLayout = property.Value.Value<bool>(); break;
                    case "min_width": element.minWidth = property.Value.Value<float>(); break;
                    case "min_height": element.minHeight = property.Value.Value<float>(); break;
                    case "preferred_width": element.preferredWidth = property.Value.Value<float>(); break;
                    case "preferred_height": element.preferredHeight = property.Value.Value<float>(); break;
                    case "flexible_width": element.flexibleWidth = property.Value.Value<float>(); break;
                    case "flexible_height": element.flexibleHeight = property.Value.Value<float>(); break;
                    case "layout_priority": element.layoutPriority = property.Value.Value<int>(); break;
                }
            }
        }

        static void ApplyLinearLayout(HorizontalOrVerticalLayoutGroup group, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                switch (property.Name)
                {
                    case "padding": group.padding = ReadRectOffset(property.Value); break;
                    case "spacing": group.spacing = property.Value.Value<float>(); break;
                    case "child_alignment": group.childAlignment = ParseEnum<TextAnchor>(property.Value.Value<string>(), property.Name); break;
                    case "child_control_width": group.childControlWidth = property.Value.Value<bool>(); break;
                    case "child_control_height": group.childControlHeight = property.Value.Value<bool>(); break;
                    case "child_force_expand_width": group.childForceExpandWidth = property.Value.Value<bool>(); break;
                    case "child_force_expand_height": group.childForceExpandHeight = property.Value.Value<bool>(); break;
                    case "child_scale_width": group.childScaleWidth = property.Value.Value<bool>(); break;
                    case "child_scale_height": group.childScaleHeight = property.Value.Value<bool>(); break;
                    case "reverse_arrangement": SetSerializedBoolean(group, "m_ReverseArrangement", property.Value.Value<bool>(), property.Name); break;
                }
            }
        }

        static void ApplyGridLayout(GridLayoutGroup group, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                switch (property.Name)
                {
                    case "padding": group.padding = ReadRectOffset(property.Value); break;
                    case "child_alignment": group.childAlignment = ParseEnum<TextAnchor>(property.Value.Value<string>(), property.Name); break;
                    case "cell_size": group.cellSize = ReadVector2(property.Value, property.Name); break;
                    case "spacing": group.spacing = ReadVector2(property.Value, property.Name); break;
                    case "start_corner": group.startCorner = ParseEnum<GridLayoutGroup.Corner>(property.Value.Value<string>(), property.Name); break;
                    case "start_axis": group.startAxis = ParseEnum<GridLayoutGroup.Axis>(property.Value.Value<string>(), property.Name); break;
                    case "constraint": group.constraint = ParseEnum<GridLayoutGroup.Constraint>(property.Value.Value<string>(), property.Name); break;
                    case "constraint_count": group.constraintCount = property.Value.Value<int>(); break;
                }
            }
        }

        static void ApplyContentSizeFitter(ContentSizeFitter fitter, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                switch (property.Name)
                {
                    case "horizontal_fit": fitter.horizontalFit = ParseEnum<ContentSizeFitter.FitMode>(property.Value.Value<string>(), property.Name); break;
                    case "vertical_fit": fitter.verticalFit = ParseEnum<ContentSizeFitter.FitMode>(property.Value.Value<string>(), property.Name); break;
                }
            }
        }

        static void ApplyGraphic(Graphic graphic, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                switch (property.Name)
                {
                    case "enabled": graphic.enabled = property.Value.Value<bool>(); break;
                    case "raycast_target": graphic.raycastTarget = property.Value.Value<bool>(); break;
                    case "color": graphic.color = ReadVector4(property.Value, property.Name); break;
                }
            }
        }

        static void ApplySelectable(Selectable selectable, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                if (property.Name == "interactable")
                    selectable.interactable = property.Value.Value<bool>();
            }
        }

        static void ApplyText(TMP_Text text, JObject properties)
        {
            foreach (JProperty property in properties.Properties())
            {
                switch (property.Name)
                {
                    case "enabled": text.enabled = property.Value.Value<bool>(); break;
                    case "margin": text.margin = ReadVector4(property.Value, property.Name); break;
                    case "font_size": text.fontSize = property.Value.Value<float>(); break;
                    case "font_size_min": text.fontSizeMin = property.Value.Value<float>(); break;
                    case "font_size_max": text.fontSizeMax = property.Value.Value<float>(); break;
                    case "auto_sizing": text.enableAutoSizing = property.Value.Value<bool>(); break;
                    case "word_wrapping": SetSerializedBoolean(text, "m_enableWordWrapping", property.Value.Value<bool>(), property.Name); break;
                    case "alignment": text.alignment = ParseEnum<TextAlignmentOptions>(property.Value.Value<string>(), property.Name); break;
                    case "overflow_mode": text.overflowMode = ParseEnum<TextOverflowModes>(property.Value.Value<string>(), property.Name); break;
                    case "raycast_target": text.raycastTarget = property.Value.Value<bool>(); break;
                    case "color": text.color = ReadVector4(property.Value, property.Name); break;
                }
            }
        }

        static Vector2 ReadVector2(JToken token, string propertyName)
        {
            float[] values = ReadNumbers(token, propertyName, 2);
            return new Vector2(values[0], values[1]);
        }

        static Vector3 ReadVector3(JToken token, string propertyName)
        {
            float[] values = ReadNumbers(token, propertyName, 3);
            return new Vector3(values[0], values[1], values[2]);
        }

        static Vector4 ReadVector4(JToken token, string propertyName)
        {
            float[] values = ReadNumbers(token, propertyName, 4);
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

        static float[] ReadNumbers(JToken token, string propertyName, int count)
        {
            if (token is JArray array && array.Count == count)
                return array.Select(value => value.Value<float>()).ToArray();
            if (token is JObject valueObject)
            {
                string[] names = count == 2 ? new[] { "x", "y" } : count == 3 ? new[] { "x", "y", "z" } : new[] { "x", "y", "z", "w" };
                if (names.All(name => valueObject[name] != null))
                    return names.Select(name => valueObject[name].Value<float>()).ToArray();
            }
            throw new ArgumentException($"Property '{propertyName}' requires {count} numeric values.");
        }

        static RectOffset ReadRectOffset(JToken token)
        {
            if (token is JObject valueObject && valueObject["left"] != null && valueObject["right"] != null &&
                valueObject["top"] != null && valueObject["bottom"] != null)
            {
                return new RectOffset(
                    valueObject["left"].Value<int>(),
                    valueObject["right"].Value<int>(),
                    valueObject["top"].Value<int>(),
                    valueObject["bottom"].Value<int>());
            }
            throw new ArgumentException("Property 'padding' requires integer left, right, top, and bottom values.");
        }

        static T ParseEnum<T>(string value, string propertyName) where T : struct
        {
            if (Enum.TryParse(value, true, out T parsed) == false || Enum.IsDefined(typeof(T), parsed) == false)
                throw new ArgumentException($"Property '{propertyName}' has invalid {typeof(T).Name} value '{value}'.");
            return parsed;
        }

        static void SetSerializedBoolean(Component component, string serializedName, bool value, string publicName)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(serializedName);
            if (property == null)
                throw new ArgumentException($"Property '{publicName}' is unavailable in this Unity/package version.");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static HashSet<string> Set(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.Ordinal);
        }
    }
}
