// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
    [InitializeOnLoad]
    public static class BuildTypeValidatorRegistry
    {
        static List<BuildTypeValidatorDescriptor> descriptors;

        static BuildTypeValidatorRegistry()
        {
            Refresh();
        }

        public static IReadOnlyList<BuildTypeValidatorDescriptor> Descriptors
        {
            get
            {
                EnsureDiscovered();
                return descriptors;
            }
        }

        public static void Refresh()
        {
            descriptors = null;
            EnsureDiscovered();
        }

        public static IEnumerable<BuildTypeValidatorDescriptor> GetEnabledValidatorsFor(Type targetType)
        {
            EnsureDiscovered();

            foreach (BuildTypeValidatorDescriptor descriptor in descriptors)
            {
                if (!descriptor.IsValid || !BuildValidationSettings.instance.IsBuildTypeValidatorEnabled(descriptor.Id))
                    continue;

                if (descriptor.TargetType == targetType || (descriptor.UseForChildren && descriptor.TargetType.IsAssignableFrom(targetType)))
                    yield return descriptor;
            }
        }

        static void EnsureDiscovered()
        {
            if (descriptors != null)
                return;

            descriptors = new List<BuildTypeValidatorDescriptor>();
            foreach (Type validatorType in TypeCache.GetTypesWithAttribute<BuildTypeValidatorAttribute>())
            {
                foreach (BuildTypeValidatorAttribute attribute in validatorType.GetCustomAttributes<BuildTypeValidatorAttribute>())
                    descriptors.Add(CreateDescriptor(validatorType, attribute));
            }
        }

        static BuildTypeValidatorDescriptor CreateDescriptor(Type validatorType, BuildTypeValidatorAttribute attribute)
        {
            string id = $"{validatorType.Assembly.GetName().Name}:{validatorType.FullName}";
            string error = null;

            if (attribute.TargetType == null)
                error = "Target type is null.";
            else if (!typeof(Object).IsAssignableFrom(attribute.TargetType))
                error = $"Target type '{attribute.TargetType.FullName}' does not inherit from UnityEngine.Object.";
            else if (validatorType.IsAbstract)
                error = "Validator type is abstract.";
            else if (validatorType.IsGenericTypeDefinition)
                error = "Validator type is an open generic type definition.";
            else if (!typeof(IBuildTypeValidator).IsAssignableFrom(validatorType))
                error = $"Validator type does not implement {typeof(IBuildTypeValidator).FullName}.";
            else if (validatorType.GetConstructor(Type.EmptyTypes) == null)
                error = "Validator type must have a public parameterless constructor.";

            return new BuildTypeValidatorDescriptor(id, validatorType, attribute.TargetType, attribute.UseForChildren, error);
        }
    }

    public sealed class BuildTypeValidatorDescriptor
    {
        public BuildTypeValidatorDescriptor(string id, Type validatorType, Type targetType, bool useForChildren, string validationError)
        {
            Id = id;
            ValidatorType = validatorType;
            TargetType = targetType;
            UseForChildren = useForChildren;
            ValidationError = validationError;
        }

        public string Id { get; }
        public Type ValidatorType { get; }
        public Type TargetType { get; }
        public bool UseForChildren { get; }
        public string ValidationError { get; }
        public bool IsValid => string.IsNullOrEmpty(ValidationError);

        public IBuildTypeValidator CreateInstance()
        {
            return (IBuildTypeValidator)Activator.CreateInstance(ValidatorType);
        }
    }
}
