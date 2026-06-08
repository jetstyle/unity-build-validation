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
    [BuildTypeValidator(typeof(PlayableDirector))]
    public sealed class TimelineReferencesValidator : BuildTypeValidator<PlayableDirector>
    {
        static readonly Dictionary<Type, ValidatedExposedReferenceField[]> FieldCache = new Dictionary<Type, ValidatedExposedReferenceField[]>();

        protected override BuildValidationResult Validate(PlayableDirector target, BuildTypeValidationContext context)
        {
            if (target.playableAsset == null)
                return BuildValidationResult.Pass();

            ValidateBindings(target, context);
            ValidateMarkedExposedReferences(target, context);
            return BuildValidationResult.Pass();
        }

        static void ValidateBindings(PlayableDirector director, BuildTypeValidationContext context)
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
                context.Fatal($"Timeline binding is not set. Timeline='{timelinePath}', Track='{GetTrackPath(track)}', Binding='{bindingName}', BindingType='{binding.outputTargetType?.FullName}'.");
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
            ValidatedExposedReferenceField[] fields = GetValidatedExposedReferenceFields(asset.GetType());
            if (fields.Length == 0)
                return;

            var serializedObject = new SerializedObject(asset);
            foreach (ValidatedExposedReferenceField field in fields)
            {
                SerializedProperty property = serializedObject.FindProperty(field.Field.Name);
                string propertyPath = property != null ? property.propertyPath : field.Field.Name;
                Object resolvedValue = ResolveExposedReference(field.Field, asset, director);
                if (resolvedValue != null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(asset);
                context.Report(field.Attribute.Severity, $"Timeline exposed reference is not resolved. Timeline='{timelinePath}', Asset='{assetPath}', ObjectType='{asset.GetType().FullName}', Field='{field.Field.Name}', Property='{propertyPath}'.", field.Attribute.FailMessage);
            }
        }

        static ValidatedExposedReferenceField[] GetValidatedExposedReferenceFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out ValidatedExposedReferenceField[] cachedFields))
                return cachedFields;

            var fields = new List<ValidatedExposedReferenceField>();
            for (Type currentType = type; currentType != null && currentType != typeof(MonoBehaviour) && currentType != typeof(ScriptableObject); currentType = currentType.BaseType)
            {
                foreach (FieldInfo field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    ValidateReferenceSetAttribute attribute = field.GetCustomAttribute<ValidateReferenceSetAttribute>();
                    if (attribute == null)
                        continue;

                    if (!IsUnitySerializedField(field))
                        continue;

                    if (!IsSupportedExposedReferenceField(field.FieldType))
                        continue;

                    fields.Add(new ValidatedExposedReferenceField(field, attribute));
                }
            }

            cachedFields = fields.ToArray();
            FieldCache[type] = cachedFields;
            return cachedFields;
        }

        static bool IsSupportedExposedReferenceField(Type fieldType)
        {
            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(ExposedReference<>))
                return false;

            return typeof(Object).IsAssignableFrom(fieldType.GetGenericArguments()[0]);
        }

        static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
                return false;

            if (field.IsDefined(typeof(NonSerializedAttribute), inherit: true))
                return false;

            return field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: true);
        }

        static Object ResolveExposedReference(FieldInfo field, Object target, IExposedPropertyTable resolver)
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

        readonly struct ValidatedExposedReferenceField
        {
            public ValidatedExposedReferenceField(FieldInfo field, ValidateReferenceSetAttribute attribute)
            {
                Field = field;
                Attribute = attribute;
            }

            public FieldInfo Field { get; }
            public ValidateReferenceSetAttribute Attribute { get; }
        }
    }
}
