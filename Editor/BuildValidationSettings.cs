// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

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
    }
}
