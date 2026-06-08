// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using UnityEditor;
using UnityEngine;

namespace JetXR.Unity.BuildValidation.Editor
{
    public static class BuildValidationSettingsProvider
    {
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

            foreach (BuildTypeValidatorDescriptor descriptor in BuildTypeValidatorRegistry.Descriptors)
            {
                if (IsTestValidator(descriptor))
                    continue;

                using (new EditorGUI.DisabledScope(!descriptor.IsValid))
                {
                    bool enabled = BuildValidationSettings.instance.IsBuildTypeValidatorEnabled(descriptor.Id);
                    bool newEnabled = EditorGUILayout.ToggleLeft(GetLabel(descriptor), enabled);
                    if (newEnabled != enabled)
                        BuildValidationSettings.instance.SetBuildTypeValidatorEnabled(descriptor.Id, newEnabled);
                }

                if (!descriptor.IsValid)
                    EditorGUILayout.HelpBox(descriptor.ValidationError, MessageType.Error);
            }

            if (GUILayout.Button("Refresh Validators"))
                BuildTypeValidatorRegistry.Refresh();
        }

        static string GetLabel(BuildTypeValidatorDescriptor descriptor)
        {
            string targetTypeName = descriptor.TargetType != null ? descriptor.TargetType.FullName : "<null>";
            string childMode = descriptor.UseForChildren ? " + children" : string.Empty;
            return $"{descriptor.ValidatorType.FullName} -> {targetTypeName}{childMode}";
        }

        static bool IsTestValidator(BuildTypeValidatorDescriptor descriptor)
        {
            string assemblyName = descriptor.ValidatorType.Assembly.GetName().Name;
            return assemblyName.EndsWith(".Tests", System.StringComparison.Ordinal) ||
                   assemblyName.Contains(".Tests.", System.StringComparison.Ordinal);
        }
    }
}
