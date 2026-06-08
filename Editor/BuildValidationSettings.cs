// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JetXR.Unity.BuildValidation.Editor
{
    [FilePath("ProjectSettings/BuildValidationSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class BuildValidationSettings : ScriptableSingleton<BuildValidationSettings>
    {
        [SerializeField]
        List<string> disabledBuildTypeValidatorIds = new List<string>();

        [SerializeField]
        List<BuildTypeValidatorSettingsEntry> buildTypeValidatorSettings = new List<BuildTypeValidatorSettingsEntry>();

        public IReadOnlyList<string> DisabledBuildTypeValidatorIds => disabledBuildTypeValidatorIds;

        public bool IsBuildTypeValidatorEnabled(string validatorId)
        {
            return !disabledBuildTypeValidatorIds.Contains(validatorId);
        }

        public void SetBuildTypeValidatorEnabled(string validatorId, bool enabled)
        {
            bool currentlyDisabled = disabledBuildTypeValidatorIds.Contains(validatorId);
            if (enabled && currentlyDisabled)
                disabledBuildTypeValidatorIds.Remove(validatorId);
            else if (!enabled && !currentlyDisabled)
                disabledBuildTypeValidatorIds.Add(validatorId);
            else
                return;

            Save(saveAsText: true);
        }

        public BuildTypeValidatorSettings GetOrCreateBuildTypeValidatorSettings(BuildTypeValidatorDescriptor descriptor)
        {
            if (descriptor == null || descriptor.SettingsType == null)
                return null;

            BuildTypeValidatorSettingsEntry entry = GetOrCreateSettingsEntry(descriptor.Id);
            if (entry.Settings == null || entry.Settings.GetType() != descriptor.SettingsType)
            {
                entry.Settings = (BuildTypeValidatorSettings)Activator.CreateInstance(descriptor.SettingsType);
                Save(saveAsText: true);
            }

            return entry.Settings;
        }

        public void SaveSettings()
        {
            Save(saveAsText: true);
        }

        BuildTypeValidatorSettingsEntry GetOrCreateSettingsEntry(string validatorId)
        {
            foreach (BuildTypeValidatorSettingsEntry entry in buildTypeValidatorSettings)
            {
                if (entry.ValidatorId == validatorId)
                    return entry;
            }

            var newEntry = new BuildTypeValidatorSettingsEntry(validatorId);
            buildTypeValidatorSettings.Add(newEntry);
            return newEntry;
        }

        [Serializable]
        public sealed class BuildTypeValidatorSettingsEntry
        {
            [SerializeField]
            string validatorId;

            [SerializeReference]
            BuildTypeValidatorSettings settings;

            public BuildTypeValidatorSettingsEntry()
            {
            }

            public BuildTypeValidatorSettingsEntry(string validatorId)
            {
                this.validatorId = validatorId;
            }

            public string ValidatorId => validatorId;
            public BuildTypeValidatorSettings Settings
            {
                get => settings;
                set => settings = value;
            }
        }
    }
}
