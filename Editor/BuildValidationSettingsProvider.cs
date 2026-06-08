// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JetXR.Unity.BuildValidation.Editor
{
    public static class BuildValidationSettingsProvider
    {
        static readonly HashSet<string> ExpandedSettings = new HashSet<string>();
        static GUIStyle disabledLabelStyle;
        static GUIContent settingsButtonContent;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Build Validation", SettingsScope.Project)
            {
                label = "Build Validation",
                guiHandler = _ => DrawSettings()
            };
        }

        static void DrawSettings()
        {
            EditorGUILayout.LabelField("Build Type Validators", EditorStyles.boldLabel);
            SerializedObject settingsObject = new SerializedObject(BuildValidationSettings.instance);

            foreach (BuildTypeValidatorDescriptor descriptor in BuildTypeValidatorRegistry.Descriptors)
            {
                if (IsTestValidator(descriptor))
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUI.DisabledScope(!descriptor.IsValid))
                    {
                        DrawValidatorHeader(descriptor, settingsObject);
                    }

                    if (!descriptor.IsValid)
                        EditorGUILayout.HelpBox(descriptor.ValidationError, MessageType.Error);
                }
            }

            if (GUILayout.Button("Refresh Validators"))
                BuildTypeValidatorRegistry.Refresh();
        }

        static void DrawValidatorHeader(BuildTypeValidatorDescriptor descriptor, SerializedObject settingsObject)
        {
            bool enabled = BuildValidationSettings.instance.IsBuildTypeValidatorEnabled(descriptor.Id);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(18));
                if (newEnabled != enabled)
                {
                    BuildValidationSettings.instance.SetBuildTypeValidatorEnabled(descriptor.Id, newEnabled);
                    enabled = newEnabled;
                    if (!enabled)
                        ExpandedSettings.Remove(descriptor.Id);
                }

                GUILayout.Label(GetLabel(descriptor), enabled ? EditorStyles.label : GetDisabledLabelStyle());
                GUILayout.FlexibleSpace();

                if (enabled && descriptor.HasSettings)
                {
                    bool expanded = ExpandedSettings.Contains(descriptor.Id);
                    bool newExpanded = GUILayout.Toggle(expanded, GetSettingsButtonContent(), EditorStyles.miniButton, GUILayout.Width(24));
                    if (newExpanded != expanded)
                    {
                        if (newExpanded)
                            ExpandedSettings.Add(descriptor.Id);
                        else
                            ExpandedSettings.Remove(descriptor.Id);
                    }
                }
            }

            if (enabled && descriptor.HasSettings && ExpandedSettings.Contains(descriptor.Id))
                DrawValidatorSettings(settingsObject, descriptor);
        }

        static void DrawValidatorSettings(SerializedObject settingsObject, BuildTypeValidatorDescriptor descriptor)
        {
            BuildValidationSettings.instance.GetOrCreateBuildTypeValidatorSettings(descriptor);
            settingsObject.Update();

            SerializedProperty settingsEntries = settingsObject.FindProperty("buildTypeValidatorSettings");
            SerializedProperty settingsProperty = FindSettingsProperty(settingsEntries, descriptor.Id);
            if (settingsProperty == null)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                SerializedProperty child = settingsProperty.Copy();
                SerializedProperty end = child.GetEndProperty();
                bool enterChildren = true;
                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;
                    EditorGUILayout.PropertyField(child, includeChildren: true);
                }
            }

            if (settingsObject.ApplyModifiedProperties())
                BuildValidationSettings.instance.SaveSettings();
        }

        static SerializedProperty FindSettingsProperty(SerializedProperty settingsEntries, string validatorId)
        {
            for (int i = 0; i < settingsEntries.arraySize; i++)
            {
                SerializedProperty entry = settingsEntries.GetArrayElementAtIndex(i);
                SerializedProperty validatorIdProperty = entry.FindPropertyRelative("validatorId");
                if (validatorIdProperty.stringValue != validatorId)
                    continue;

                return entry.FindPropertyRelative("settings");
            }

            return null;
        }

        static GUIContent GetLabel(BuildTypeValidatorDescriptor descriptor)
        {
            string validatorName = ToReadableTypeName(descriptor.ValidatorType);
            string targetTypeName = descriptor.TargetType != null ? ToReadableTypeName(descriptor.TargetType) : "<null>";
            string childMode = descriptor.UseForChildren ? " + children" : string.Empty;
            string tooltipTargetName = descriptor.TargetType != null ? descriptor.TargetType.FullName : "<null>";
            return new GUIContent($"{validatorName} ({targetTypeName}{childMode})", $"Validator: {descriptor.ValidatorType.FullName}\nTarget: {tooltipTargetName}");
        }

        static string ToReadableTypeName(System.Type type)
        {
            return SplitCamelCase(type.Name);
        }

        static string SplitCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && ShouldInsertSpace(value, i))
                    builder.Append(' ');

                builder.Append(current);
            }

            return builder.ToString();
        }

        static bool ShouldInsertSpace(string value, int index)
        {
            char previous = value[index - 1];
            char current = value[index];
            char next = index + 1 < value.Length ? value[index + 1] : '\0';

            if (char.IsUpper(current) && (char.IsLower(previous) || (next != '\0' && char.IsLower(next))))
                return true;

            if (char.IsDigit(current) && !char.IsDigit(previous))
                return true;

            return false;
        }

        static GUIStyle GetDisabledLabelStyle()
        {
            if (disabledLabelStyle == null)
            {
                disabledLabelStyle = new GUIStyle(EditorStyles.label);
                disabledLabelStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.45f, 0.45f, 0.45f);
            }

            return disabledLabelStyle;
        }

        static GUIContent GetSettingsButtonContent()
        {
            if (settingsButtonContent != null)
                return settingsButtonContent;

            settingsButtonContent = EditorGUIUtility.IconContent("SettingsIcon");
            if (settingsButtonContent == null || settingsButtonContent.image == null)
                settingsButtonContent = EditorGUIUtility.IconContent("_Popup");

            if (settingsButtonContent == null)
                settingsButtonContent = new GUIContent("...");

            settingsButtonContent.tooltip = "Show validator settings";
            return settingsButtonContent;
        }

        static bool IsTestValidator(BuildTypeValidatorDescriptor descriptor)
        {
            string assemblyName = descriptor.ValidatorType.Assembly.GetName().Name;
            return assemblyName.EndsWith(".Tests", System.StringComparison.Ordinal) ||
                   assemblyName.Contains(".Tests.", System.StringComparison.Ordinal);
        }
    }
}
