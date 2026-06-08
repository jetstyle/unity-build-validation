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
        static readonly Dictionary<Type, ValidatedMethod[]> MethodCache = new Dictionary<Type, ValidatedMethod[]>();

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
            var scannedObjects = new HashSet<string>();
            var reportedInvalidMembers = new HashSet<string>();
            string[] buildScenes = GetEnabledBuildScenes();
            string[] resources = GetResourcesAssetPaths();
            string[] dependencies = GetDependencies(buildScenes.Concat(resources));

            ReportInvalidBuildTypeValidators(report, reportedInvalidMembers);

            foreach (string scenePath in buildScenes)
                ValidateScene(scenePath, report, scannedObjects, reportedInvalidMembers);

            foreach (string assetPath in dependencies.Concat(resources).Distinct())
                ValidateAsset(assetPath, report, scannedObjects, reportedInvalidMembers);

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

        static void ValidateScene(string scenePath, ValidationReport report, HashSet<string> scannedObjects, HashSet<string> reportedInvalidMembers)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;

            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    ValidateGameObjectHierarchy(root, scenePath, report, scannedObjects, reportedInvalidMembers);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        static void ValidateAsset(string assetPath, ValidationReport report, HashSet<string> scannedObjects, HashSet<string> reportedInvalidMembers)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            Type mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (mainType == typeof(SceneAsset) || mainType == typeof(MonoScript))
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
                ValidateGameObjectHierarchy(prefab, assetPath, report, scannedObjects, reportedInvalidMembers);

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset == null || asset is GameObject || asset is Component || asset is MonoScript)
                    continue;

                ValidateObject(asset, assetPath, null, report, scannedObjects, reportedInvalidMembers);
            }
        }

        static void ValidateGameObjectHierarchy(GameObject root, string sourcePath, ValidationReport report, HashSet<string> scannedObjects, HashSet<string> reportedInvalidMembers)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                string hierarchyPath = GetHierarchyPath(transform);
                ValidateObject(transform.gameObject, sourcePath, hierarchyPath, report, scannedObjects, reportedInvalidMembers);

                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component == null)
                        continue;

                    ValidateObject(component, sourcePath, hierarchyPath, report, scannedObjects, reportedInvalidMembers);
                }
            }
        }

        static void ValidateObject(Object target, string sourcePath, string hierarchyPath, ValidationReport report, HashSet<string> scannedObjects, HashSet<string> reportedInvalidMembers)
        {
            if (target == null)
                return;

            if (!scannedObjects.Add(target.GetInstanceID().ToString()))
                return;

            ValidatedField[] fields = GetValidatedFields(target.GetType());
            ValidatedMethod[] methods = GetValidatedMethods(target.GetType());
            Object issueContext = target;

            if (fields.Length > 0)
            {
                var serializedObject = new UnityEditor.SerializedObject(target);

                foreach (ValidatedField field in fields)
                {
                    if (field.IsUnsupported)
                    {
                        if (TryMarkInvalidMemberReported(reportedInvalidMembers, field.Field))
                            report.Add(new ValidationIssue(ReferenceValidationSeverity.Fatal, issueContext, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, null, $"Field is marked for validation but has unsupported field type '{field.Field.FieldType.FullName}'. Use UnityEngine.Object references or arrays/List<T> of UnityEngine.Object references.", field.Attribute.FailMessage));
                        continue;
                    }

                    SerializedProperty property = serializedObject.FindProperty(field.Field.Name);
                    if (property == null)
                    {
                        report.Add(new ValidationIssue(field.Attribute.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, null, "Field is marked for validation but is not serialized by Unity.", field.Attribute.FailMessage));
                        continue;
                    }

                    if (field.IsCollection)
                        ValidateCollectionProperty(property, field, target, issueContext, sourcePath, hierarchyPath, report);
                    else
                        ValidateSingleReferenceProperty(property, field, target, issueContext, sourcePath, hierarchyPath, report);
                }
            }

            foreach (ValidatedMethod method in methods)
            {
                ValidateInvokedMethod(method, target, issueContext, sourcePath, hierarchyPath, report, reportedInvalidMembers);
            }

            ValidateBuildTypeValidators(target, issueContext, sourcePath, hierarchyPath, report);
        }

        static void ValidateSingleReferenceProperty(SerializedProperty property, ValidatedField field, Object target, Object issueContext, string sourcePath, string hierarchyPath, ValidationReport report)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                report.Add(new ValidationIssue(field.Attribute.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, property.propertyPath, $"Unsupported serialized property type: {property.propertyType}.", field.Attribute.FailMessage));
                return;
            }

            if (property.objectReferenceValue == null)
                report.Add(new ValidationIssue(field.Attribute.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, property.propertyPath, "Reference is not set.", field.Attribute.FailMessage));
        }

        static void ValidateCollectionProperty(SerializedProperty property, ValidatedField field, Object target, Object issueContext, string sourcePath, string hierarchyPath, ValidationReport report)
        {
            if (!property.isArray)
            {
                report.Add(new ValidationIssue(field.Attribute.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, property.propertyPath, "Collection field is not serialized as an array/list.", field.Attribute.FailMessage));
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.propertyType != SerializedPropertyType.ObjectReference)
                {
                    report.Add(new ValidationIssue(field.Attribute.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, element.propertyPath, $"Unsupported serialized collection element type: {element.propertyType}.", field.Attribute.FailMessage, i));
                    continue;
                }

                if (element.objectReferenceValue == null)
                    report.Add(new ValidationIssue(field.Attribute.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), field.Field.Name, element.propertyPath, "Reference element is not set.", field.Attribute.FailMessage, i));
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

                    if (IsDirectExposedReferenceField(field.FieldType))
                        continue;

                    fields.Add(CreateValidatedField(field, attribute));
                }
            }

            cachedFields = fields.ToArray();
            FieldCache[type] = cachedFields;
            return cachedFields;
        }

        static ValidatedMethod[] GetValidatedMethods(Type type)
        {
            if (MethodCache.TryGetValue(type, out ValidatedMethod[] cachedMethods))
                return cachedMethods;

            var methods = new List<ValidatedMethod>();
            for (Type currentType = type; currentType != null && currentType != typeof(MonoBehaviour) && currentType != typeof(ScriptableObject); currentType = currentType.BaseType)
            {
                foreach (MethodInfo method in currentType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    ValidateInvokeAttribute attribute = method.GetCustomAttribute<ValidateInvokeAttribute>();
                    if (attribute == null)
                        continue;

                    methods.Add(CreateValidatedMethod(method));
                }
            }

            cachedMethods = methods.ToArray();
            MethodCache[type] = cachedMethods;
            return cachedMethods;
        }

        static ValidatedMethod CreateValidatedMethod(MethodInfo method)
        {
            string invalidReason = null;

            if (method.IsStatic)
                invalidReason = "Method is static.";
            else if (method.GetParameters().Length != 0)
                invalidReason = "Method must not declare parameters.";
            else if (method.ReturnType != typeof(BuildValidationResult))
                invalidReason = $"Method must return {typeof(BuildValidationResult).FullName}.";

            return new ValidatedMethod(method, invalidReason);
        }

        static void ValidateInvokedMethod(ValidatedMethod method, Object target, Object issueContext, string sourcePath, string hierarchyPath, ValidationReport report, HashSet<string> reportedInvalidMembers)
        {
            if (method.IsInvalid)
            {
                if (TryMarkInvalidMemberReported(reportedInvalidMembers, method.Method))
                    report.Add(new ValidationIssue(ReferenceValidationSeverity.Fatal, issueContext, sourcePath, hierarchyPath, target.GetType(), null, null, $"Invalid {nameof(ValidateInvokeAttribute)} method signature. {method.InvalidReason}", null, null, method.Method.Name));
                return;
            }

            BuildValidationResult result;
            try
            {
                result = (BuildValidationResult)method.Method.Invoke(target, null);
            }
            catch (TargetInvocationException exception)
            {
                Exception innerException = exception.InnerException ?? exception;
                report.Add(new ValidationIssue(ReferenceValidationSeverity.Fatal, issueContext, sourcePath, hierarchyPath, target.GetType(), null, null, $"Validation method threw {innerException.GetType().FullName}: {innerException.Message}", null, null, method.Method.Name));
                return;
            }
            catch (Exception exception)
            {
                report.Add(new ValidationIssue(ReferenceValidationSeverity.Fatal, issueContext, sourcePath, hierarchyPath, target.GetType(), null, null, $"Validation method could not be invoked: {exception.GetType().FullName}: {exception.Message}", null, null, method.Method.Name));
                return;
            }

            if (result.Passed)
                return;

            string message = string.IsNullOrEmpty(result.Message) ? "Validation method reported an issue." : result.Message;
            report.Add(new ValidationIssue(result.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), null, null, message, null, null, method.Method.Name));
        }

        static void ValidateBuildTypeValidators(Object target, Object issueContext, string sourcePath, string hierarchyPath, ValidationReport report)
        {
            foreach (BuildTypeValidatorDescriptor descriptor in BuildTypeValidatorRegistry.GetEnabledValidatorsFor(target.GetType()))
            {
                var context = new BuildTypeValidationContext(sourcePath, hierarchyPath, null, null, issueContext, (severity, message, failMessage) =>
                {
                    report.Add(new ValidationIssue(severity, issueContext, sourcePath, hierarchyPath, target.GetType(), null, null, message, failMessage, validatorName: descriptor.ValidatorType.FullName));
                });

                BuildValidationResult result;
                try
                {
                    result = descriptor.CreateInstance().Validate(target, context);
                }
                catch (Exception exception)
                {
                    report.Add(new ValidationIssue(ReferenceValidationSeverity.Fatal, issueContext, sourcePath, hierarchyPath, target.GetType(), null, null, $"Build type validator threw {exception.GetType().FullName}: {exception.Message}", null, validatorName: descriptor.ValidatorType.FullName));
                    continue;
                }

                if (result.Passed)
                    continue;

                string message = string.IsNullOrEmpty(result.Message) ? "Build type validator reported an issue." : result.Message;
                report.Add(new ValidationIssue(result.Severity, issueContext, sourcePath, hierarchyPath, target.GetType(), null, null, message, null, validatorName: descriptor.ValidatorType.FullName));
            }
        }

        static void ReportInvalidBuildTypeValidators(ValidationReport report, HashSet<string> reportedInvalidMembers)
        {
            foreach (BuildTypeValidatorDescriptor descriptor in BuildTypeValidatorRegistry.Descriptors)
            {
                if (descriptor.IsValid || !reportedInvalidMembers.Add(descriptor.Id))
                    continue;

                report.Add(new ValidationIssue(ReferenceValidationSeverity.Fatal, null, "<build type validator discovery>", null, descriptor.ValidatorType, null, null, $"Invalid {nameof(BuildTypeValidatorAttribute)} definition. {descriptor.ValidationError}", null, validatorName: descriptor.ValidatorType.FullName));
            }
        }

        static bool TryMarkInvalidMemberReported(HashSet<string> reportedInvalidMembers, MemberInfo member)
        {
            string key = $"{member.DeclaringType?.AssemblyQualifiedName}.{member.MetadataToken}";
            return reportedInvalidMembers.Add(key);
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

        static bool IsDirectExposedReferenceField(Type fieldType)
        {
            return fieldType.IsGenericType && fieldType.GetGenericTypeDefinition().FullName == "UnityEngine.ExposedReference`1";
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

        readonly struct ValidatedMethod
        {
            public ValidatedMethod(MethodInfo method, string invalidReason)
            {
                Method = method;
                InvalidReason = invalidReason;
            }

            public MethodInfo Method { get; }
            public string InvalidReason { get; }
            public bool IsInvalid => !string.IsNullOrEmpty(InvalidReason);
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
            public ValidationIssue(ReferenceValidationSeverity severity, Object context, string sourcePath, string hierarchyPath, Type targetType, string fieldName, string propertyPath, string message, string failMessage, int? collectionIndex = null, string methodName = null, string resolverSource = null, string validatorName = null)
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
                MethodName = methodName;
                ResolverSource = resolverSource;
                ValidatorName = validatorName;
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
            public string MethodName { get; }
            public string ResolverSource { get; }
            public string ValidatorName { get; }

            public override string ToString()
            {
                string hierarchy = string.IsNullOrEmpty(HierarchyPath) ? "<asset>" : HierarchyPath;
                string property = string.IsNullOrEmpty(PropertyPath) ? FieldName : PropertyPath;
                string index = CollectionIndex.HasValue ? $"[{CollectionIndex.Value}]" : string.Empty;
                string subject;
                string attributeName;
                if (!string.IsNullOrEmpty(ValidatorName))
                {
                    subject = $"Validator='{ValidatorName}'";
                    attributeName = nameof(BuildTypeValidatorAttribute);
                }
                else if (!string.IsNullOrEmpty(MethodName))
                {
                    subject = $"Method='{MethodName}'";
                    attributeName = nameof(ValidateInvokeAttribute);
                }
                else
                {
                    subject = $"Field='{FieldName}', Property='{property}{index}'";
                    attributeName = nameof(ValidateReferenceSetAttribute);
                }

                string resolver = string.IsNullOrEmpty(ResolverSource) ? string.Empty : $", Resolver='{ResolverSource}'";
                string details = $"[{attributeName}:{Severity}] {Message} Source='{SourcePath}', Object='{hierarchy}', Component='{TargetType.FullName}', {subject}{resolver}.";
                return string.IsNullOrEmpty(FailMessage) ? details : $"{FailMessage}\n{details}";
            }
        }
    }
}
