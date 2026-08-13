// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using JetXR.Unity.BuildValidation;
using JetXR.Unity.BuildValidation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Timeline.Editor
{
    [Serializable]
    public sealed class TimelineReferencesValidatorSettings : BuildTypeValidatorSettings
    {
        public bool validateBindings = true;
        public bool validateExposedReferences = true;
        public ReferenceValidationSeverity missingBindingSeverity = ReferenceValidationSeverity.Fatal;
    }

    [BuildTypeValidator(typeof(PlayableDirector))]
    public sealed class TimelineReferencesValidator : BuildTypeValidator<PlayableDirector, TimelineReferencesValidatorSettings>
    {
        protected override BuildValidationResult Validate(PlayableDirector target, TimelineReferencesValidatorSettings settings, BuildTypeValidationContext context)
        {
            if (target.playableAsset == null)
                return BuildValidationResult.Pass();

            if (settings.validateBindings)
                ValidateBindings(target, settings.missingBindingSeverity, context);

            if (settings.validateExposedReferences)
                ValidateMarkedExposedReferences(target, context);

            return BuildValidationResult.Pass();
        }

        static void ValidateBindings(PlayableDirector director, ReferenceValidationSeverity missingBindingSeverity, BuildTypeValidationContext context)
        {
            if (!(director.playableAsset is TimelineAsset timelineAsset))
                return;

            string timelinePath = GetAssetPath(director.playableAsset);
            foreach (PlayableBinding binding in timelineAsset.outputs)
            {
                if (!(binding.sourceObject is TrackAsset track))
                    continue;

                if (!RequiresBinding(binding))
                    continue;

                if (director.GetGenericBinding(binding.sourceObject) != null)
                    continue;

                string bindingName = string.IsNullOrEmpty(binding.streamName) ? track.name : binding.streamName;
                context.Report(missingBindingSeverity, $"Timeline binding is not set. Timeline='{timelinePath}', Track='{GetTrackPath(track)}', Binding='{bindingName}', BindingType='{binding.outputTargetType?.FullName}'.");
            }
        }

        static bool RequiresBinding(PlayableBinding binding)
        {
            return binding.outputTargetType != null && binding.outputTargetType != typeof(void);
        }

        static void ValidateMarkedExposedReferences(PlayableDirector director, BuildTypeValidationContext context)
        {
            string timelinePath = GetAssetPath(director.playableAsset);
            if (string.IsNullOrEmpty(timelinePath))
                return;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(timelinePath))
            {
                if (asset == null || asset is GameObject || asset is Component || asset is MonoScript)
                    continue;

                ValidateMarkedExposedReferencesOnObject(asset, director, timelinePath, context);
            }
        }

        static void ValidateMarkedExposedReferencesOnObject(Object asset, PlayableDirector director, string timelinePath, BuildTypeValidationContext context)
        {
            SerializedFieldWalker.Walk(asset, serializedField =>
            {
                ValidateReferenceSetAttribute attribute = serializedField.Field.GetCustomAttribute<ValidateReferenceSetAttribute>();
                if (attribute == null || !IsSupportedExposedReferenceField(serializedField.Field.FieldType))
                    return;

                string propertyPath = serializedField.Property != null ? serializedField.Property.propertyPath : serializedField.Field.Name;
                Object resolvedValue = ResolveExposedReference(serializedField.Field, serializedField.Owner, director);
                if (resolvedValue != null)
                    return;

                string assetPath = AssetDatabase.GetAssetPath(asset);
                context.Report(attribute.Severity, $"Timeline exposed reference is not resolved. Timeline='{timelinePath}', Asset='{assetPath}', ObjectType='{asset.GetType().FullName}', Field='{serializedField.Field.Name}', Property='{propertyPath}'.", attribute.FailMessage);
            });
        }

        static bool IsSupportedExposedReferenceField(Type fieldType)
        {
            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(ExposedReference<>))
                return false;

            return typeof(Object).IsAssignableFrom(fieldType.GetGenericArguments()[0]);
        }

        static Object ResolveExposedReference(FieldInfo field, object target, IExposedPropertyTable resolver)
        {
            object exposedReference = field.GetValue(target);
            MethodInfo resolveMethod = field.FieldType.GetMethod(nameof(ExposedReference<Object>.Resolve), new[] { typeof(IExposedPropertyTable) });
            return resolveMethod?.Invoke(exposedReference, new object[] { resolver }) as Object;
        }

        static string GetAssetPath(Object asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(assetPath) ? "<in-memory timeline>" : assetPath;
        }

        static string GetTrackPath(TrackAsset track)
        {
            var names = new Stack<string>();
            for (TrackAsset current = track; current != null; current = current.parent as TrackAsset)
                names.Push(current.name);

            return string.Join("/", names);
        }

    }
}
