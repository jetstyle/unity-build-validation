// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
    [Serializable]
    public abstract class BuildTypeValidatorSettings
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class BuildTypeValidatorAttribute : Attribute
    {
        public BuildTypeValidatorAttribute(Type targetType, bool useForChildren = false)
        {
            TargetType = targetType;
            UseForChildren = useForChildren;
        }

        public Type TargetType { get; }
        public bool UseForChildren { get; }
    }

    public interface IBuildTypeValidator
    {
        BuildValidationResult Validate(Object target, BuildTypeValidationContext context);
    }

    public interface IBuildTypeValidatorWithSettings : IBuildTypeValidator
    {
        BuildValidationResult Validate(Object target, BuildTypeValidationContext context, BuildTypeValidatorSettings settings);
    }

    public abstract class BuildTypeValidator<T> : IBuildTypeValidator where T : Object
    {
        public BuildValidationResult Validate(Object target, BuildTypeValidationContext context)
        {
            return Validate((T)target, context);
        }

        protected abstract BuildValidationResult Validate(T target, BuildTypeValidationContext context);
    }

    public abstract class BuildTypeValidator<T, TSettings> : IBuildTypeValidatorWithSettings
        where T : Object
        where TSettings : BuildTypeValidatorSettings, new()
    {
        public BuildValidationResult Validate(Object target, BuildTypeValidationContext context)
        {
            return Validate((T)target, new TSettings(), context);
        }

        public BuildValidationResult Validate(Object target, BuildTypeValidationContext context, BuildTypeValidatorSettings settings)
        {
            return Validate((T)target, settings as TSettings ?? new TSettings(), context);
        }

        protected abstract BuildValidationResult Validate(T target, TSettings settings, BuildTypeValidationContext context);
    }
}
