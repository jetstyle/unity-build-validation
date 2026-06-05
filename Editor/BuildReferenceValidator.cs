// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetXR.Unity.BuildValidation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
    public sealed class BuildReferenceValidator : IPreprocessBuildWithReport
    {
        const string MenuPath = "Tools/Validation/Validate Required References For Build";

        static readonly Dictionary<Type, ValidatedField[]> FieldCache = new Dictionary<Type, ValidatedField[]>();

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidationReport validationReport = ValidateBuildContent(logResults: true);

            if (validationReport.FatalCount > 0)
                throw new BuildFailedException($"Required reference validation failed: {validationReport.FatalCount} fatal issue(s), {validationReport.SummaryIssueCount} summarized issue(s).");
        }

        [MenuItem(MenuPath)]
        public static void ValidateBuildContentMenu()
        {
            ValidationReport report = ValidateBuildContent(logResults: true);

            if (report.FatalCount > 0)
                Debug.LogError($"Required reference validation failed: {report.FatalCount} fatal issue(s), {report.SummaryIssueCount} summarized issue(s).");
            else
                Debug.Log($"Required reference validation passed: {report.SummaryIssueCount} summarized non-fatal issue(s).");
        }

        public static ValidationReport ValidateBuildContent(bool logResults)
        {
            var report = new ValidationReport();
            var scannedObjects = new HashSet<int>();
            string[] buildScenes = GetEnabledBuildScenes();
            string[] resources = GetResourcesAssetPaths();
            string[] dependencies = GetDependencies(buildScenes.Concat(resources));

            foreach (string scenePath in buildScenes)
                ValidateScene(scenePath, report, scannedObjects);

            foreach (string assetPath in dependencies.Concat(resources).Distinct())
                ValidateAsset(assetPath, report, scannedObjects);

            if (logResults)
                LogReport(report);

            return report;
        }

        static string[] GetEnabledBuildScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))
                .Select(scene => scene.path)
                .Distinct()
                .ToArray();
        }

        static string[] GetResourcesAssetPaths()
        {
            return AssetDatabase.FindAssets(string.Empty, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsResourcesAssetPath)
                .Distinct()
                .ToArray();
        }

        static bool IsResourcesAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (Directory.Exists(assetPath))
                return false;

            string normalizedPath = assetPath.Replace('\\', '/');
            return normalizedPath.StartsWith("Assets/Resources/", StringComparison.Ordinal) ||
                   normalizedPath.Contains("/Resources/", StringComparison.Ordinal);
        }

        static string[] GetDependencies(IEnumerable<string> assetPaths)
        {
            string[] paths = assetPaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .ToArray();

            if (paths.Length == 0)
                return Array.Empty<string>();

            return AssetDatabase.GetDependencies(paths, recursive: true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Distinct()
                .ToArray();
        }

        static void ValidateScene(string scenePath, ValidationReport report, HashSet<int> scannedObjects)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;

            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    ValidateGameObjectHierarchy(root, scenePath, report, scannedObjects);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        static void ValidateAsset(string assetPath, ValidationReport report, HashSet<int> scannedObjects)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            Type mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (mainType == typeof(SceneAsset) || mainType == typeof(MonoScript))
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
                ValidateGameObjectHierarchy(prefab, assetPath, report, scannedObjects);

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset == null || asset is GameObject || asset is Component || asset is MonoScript)
                    continue;

                ValidateObject(asset, assetPath, null, report, scannedObjects);
            }
        }

        static void ValidateGameObjectHierarchy(GameObject root, string sourcePath, ValidationReport report, HashSet<int> scannedObjects)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (behaviour == null)
                    continue;

                ValidateObject(behaviour, sourcePath, GetHierarchyPath(behaviour.transform), report, scannedObjects);
            }
        }

        static void ValidateObject(Object target, string sourcePath, string hierarchyPath, ValidationReport report, HashSet<int> scannedObjects)
        {
            if (target == null)
                return;

            int instanceId = target.GetInstanceID();
            if (!scannedObjects.Add(instanceId))
                return;

            ValidatedField[] fields = GetValidatedFields(target.GetType());
            if (fields.Length == 0)
                return;

            var serializedObject = new UnityEditor.SerializedObject(target);

            foreach (ValidatedField field in fields)
            {
                if (field.IsUnsupported)
                {
                    report.Add(new ValidationIssue(ReferenceValidationSeverity.Warning, target, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, null, $"Field is marked for validation but has unsupported field type '{field.Field.FieldType.FullName}'. Use UnityEngine.Object references or arrays/List<T> of UnityEngine.Object references.", field.Attribute.FailMessage));
                    continue;
                }

                SerializedProperty property = serializedObject.FindProperty(field.Field.Name);
                if (property == null)
                {
                    report.Add(new ValidationIssue(field.Attribute.Severity, target, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, null, "Field is marked for validation but is not serialized by Unity.", field.Attribute.FailMessage));
                    continue;
                }

                if (field.IsCollection)
                    ValidateCollectionProperty(property, field, target, sourcePath, hierarchyPath, report);
                else
                    ValidateSingleReferenceProperty(property, field, target, sourcePath, hierarchyPath, report);
            }
        }

        static void ValidateSingleReferenceProperty(SerializedProperty property, ValidatedField field, Object target, string sourcePath, string hierarchyPath, ValidationReport report)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                report.Add(new ValidationIssue(field.Attribute.Severity, target, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, property.propertyPath, $"Unsupported serialized property type: {property.propertyType}.", field.Attribute.FailMessage));
                return;
            }

            if (property.objectReferenceValue == null)
                report.Add(new ValidationIssue(field.Attribute.Severity, target, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, property.propertyPath, "Reference is not set.", field.Attribute.FailMessage));
        }

        static void ValidateCollectionProperty(SerializedProperty property, ValidatedField field, Object target, string sourcePath, string hierarchyPath, ValidationReport report)
        {
            if (!property.isArray)
            {
                report.Add(new ValidationIssue(field.Attribute.Severity, target, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, property.propertyPath, "Collection field is not serialized as an array/list.", field.Attribute.FailMessage));
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.propertyType != SerializedPropertyType.ObjectReference)
                {
                    report.Add(new ValidationIssue(field.Attribute.Severity, target, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, element.propertyPath, $"Unsupported serialized collection element type: {element.propertyType}.", field.Attribute.FailMessage, i));
                    continue;
                }

                if (element.objectReferenceValue == null)
                    report.Add(new ValidationIssue(field.Attribute.Severity, target, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, element.propertyPath, "Reference element is not set.", field.Attribute.FailMessage, i));
            }
        }

        static ValidatedField[] GetValidatedFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out ValidatedField[] cachedFields))
                return cachedFields;

            var fields = new List<ValidatedField>();
            for (Type currentType = type; currentType != null && currentType != typeof(MonoBehaviour) && currentType != typeof(ScriptableObject); currentType = currentType.BaseType)
            {
                foreach (FieldInfo field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    ValidateReferenceSetAttribute attribute = field.GetCustomAttribute<ValidateReferenceSetAttribute>();
                    if (attribute == null)
                        continue;

                    if (!IsUnitySerializedField(field))
                        continue;

                    fields.Add(CreateValidatedField(field, attribute));
                }
            }

            cachedFields = fields.ToArray();
            FieldCache[type] = cachedFields;
            return cachedFields;
        }

        static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
                return false;

            if (field.IsDefined(typeof(NonSerializedAttribute), inherit: true))
                return false;

            return field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: true);
        }

        static ValidatedField CreateValidatedField(FieldInfo field, ValidateReferenceSetAttribute attribute)
        {
            Type fieldType = field.FieldType;
            bool isCollection = false;
            Type referenceType = fieldType;

            if (fieldType.IsArray)
            {
                isCollection = true;
                referenceType = fieldType.GetElementType();
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                isCollection = true;
                referenceType = fieldType.GetGenericArguments()[0];
            }

            if (referenceType != null && typeof(Object).IsAssignableFrom(referenceType))
            {
                return new ValidatedField(field, attribute, isCollection, isUnsupported: false);
            }

            return new ValidatedField(field, attribute, isCollection: false, isUnsupported: true);
        }

        static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return null;

            var names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
                names.Push(current.name);

            return string.Join("/", names);
        }

        static void LogReport(ValidationReport report)
        {
            foreach (ValidationIssue issue in report.Issues)
            {
                string message = issue.ToString();
                switch (issue.Severity)
                {
                    case ReferenceValidationSeverity.Info:
                        Debug.Log(message, issue.Context);
                        break;
                    case ReferenceValidationSeverity.Warning:
                        Debug.LogWarning(message, issue.Context);
                        break;
                    case ReferenceValidationSeverity.Fatal:
                        Debug.LogError(message, issue.Context);
                        break;
                    default:
                        Debug.LogWarning(message, issue.Context);
                        break;
                }
            }
        }

        readonly struct ValidatedField
        {
            public ValidatedField(FieldInfo field, ValidateReferenceSetAttribute attribute, bool isCollection, bool isUnsupported)
            {
                Field = field;
                Attribute = attribute;
                IsCollection = isCollection;
                IsUnsupported = isUnsupported;
            }

            public FieldInfo Field { get; }
            public ValidateReferenceSetAttribute Attribute { get; }
            public bool IsCollection { get; }
            public bool IsUnsupported { get; }
        }

        public sealed class ValidationReport
        {
            readonly List<ValidationIssue> issues = new List<ValidationIssue>();

            public IReadOnlyList<ValidationIssue> Issues => issues;
            public int FatalCount => issues.Count(issue => issue.Severity == ReferenceValidationSeverity.Fatal);
            public int SummaryIssueCount => issues.Count(issue => issue.Severity != ReferenceValidationSeverity.Info);

            public void Add(ValidationIssue issue)
            {
                issues.Add(issue);
            }
        }

        public sealed class ValidationIssue
        {
            public ValidationIssue(ReferenceValidationSeverity severity, Object context, string sourcePath, string hierarchyPath, Type targetType, string fieldName, string propertyPath, string message, string failMessage, int? collectionIndex = null)
            {
                Severity = severity;
                Context = context;
                SourcePath = sourcePath;
                HierarchyPath = hierarchyPath;
                TargetType = targetType;
                FieldName = fieldName;
                PropertyPath = propertyPath;
                Message = message;
                FailMessage = failMessage;
                CollectionIndex = collectionIndex;
            }

            public ReferenceValidationSeverity Severity { get; }
            public Object Context { get; }
            public string SourcePath { get; }
            public string HierarchyPath { get; }
            public Type TargetType { get; }
            public string FieldName { get; }
            public string PropertyPath { get; }
            public string Message { get; }
            public string FailMessage { get; }
            public int? CollectionIndex { get; }

            public override string ToString()
            {
                string hierarchy = string.IsNullOrEmpty(HierarchyPath) ? "<asset>" : HierarchyPath;
                string property = string.IsNullOrEmpty(PropertyPath) ? FieldName : PropertyPath;
                string index = CollectionIndex.HasValue ? $"[{CollectionIndex.Value}]" : string.Empty;

                string details = $"[{nameof(ValidateReferenceSetAttribute)}:{Severity}] {Message} Source='{SourcePath}', Object='{hierarchy}', Component='{TargetType.FullName}', Field='{FieldName}', Property='{property}{index}'.";
                return string.IsNullOrEmpty(FailMessage) ? details : $"{FailMessage}\n{details}";
            }
        }
    }
}
