// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
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

    public abstract class BuildTypeValidator<T> : IBuildTypeValidator where T : Object
    {
        public BuildValidationResult Validate(Object target, BuildTypeValidationContext context)
        {
            return Validate((T)target, context);
        }

        protected abstract BuildValidationResult Validate(T target, BuildTypeValidationContext context);
    }
}
