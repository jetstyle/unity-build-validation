// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System.Collections.Generic;
using JetXR.Unity.BuildValidation;
using JetXR.Unity.BuildValidation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace JetXR.Unity.BuildValidation.Tests.Editor
{
    public sealed class BuildReferenceValidatorTests
    {
        const string TestRoot = "Assets/Temp/BuildReferenceValidatorTests";
        const string ResourcesRoot = TestRoot + "/Resources";

        [SetUp]
        public void SetUp()
        {
            CreateFolderIfMissing("Assets", "Temp");
            CreateFolderIfMissing("Assets/Temp", "BuildReferenceValidatorTests");
            CreateFolderIfMissing(TestRoot, "Resources");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        public void ScriptableObjectWithFatalNullReferenceReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorTestScriptableObject>();
            string assetPath = $"{ResourcesRoot}/FatalObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorTestScriptableObject.requiredTexture)), Is.True);
        }

        [Test]
        public void PrefabMonoBehaviourWithFatalNullReferenceReportsFatal()
        {
            var gameObject = new GameObject("ValidationPrefab");
            gameObject.AddComponent<ValidatorTestBehaviour>();
            string prefabPath = $"{ResourcesRoot}/ValidationPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
            Object.DestroyImmediate(gameObject);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, prefabPath, nameof(ValidatorTestBehaviour.requiredTexture)), Is.True);
        }

        [Test]
        public void BuildSceneMonoBehaviourWithFatalNullReferenceReportsFatal()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameObject = new GameObject("SceneValidationObject");
            gameObject.AddComponent<ValidatorTestBehaviour>();
            string scenePath = $"{TestRoot}/ValidationScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsIssue(report, scenePath, nameof(ValidatorTestBehaviour.requiredTexture)), Is.True);
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void WarningReferenceDoesNotIncrementFatalCount()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorWarningScriptableObject>();
            string assetPath = $"{ResourcesRoot}/WarningObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorWarningScriptableObject.optionalTexture)), Is.True);
        }

        [Test]
        public void InfoReferenceDoesNotIncrementSummaryOrFatalCount()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInfoScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InfoObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(report.SummaryIssueCount, Is.Zero);
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorInfoScriptableObject.infoTexture)), Is.True);
        }

        [Test]
        public void ListReferenceReportsNullElementIndex()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorListScriptableObject>();
            asset.textures = new List<Texture2D> { null };
            string assetPath = $"{ResourcesRoot}/ListObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorListScriptableObject.textures), collectionIndex: 0), Is.True);
        }

        [Test]
        public void FilledReferencesPass()
        {
            var texture = new Texture2D(1, 1);
            string texturePath = $"{ResourcesRoot}/Texture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);

            var asset = ScriptableObject.CreateInstance<ValidatorTestScriptableObject>();
            asset.requiredTexture = texture;
            string assetPath = $"{ResourcesRoot}/FilledObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorTestScriptableObject.requiredTexture)), Is.False);
        }

        [Test]
        public void AttributeWithoutArgumentsDefaultsToFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorDefaultSeverityScriptableObject>();
            string assetPath = $"{ResourcesRoot}/DefaultSeverityObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorDefaultSeverityScriptableObject.requiredTexture)), Is.True);
        }

        [Test]
        public void FailMessageIsFirstLogLine()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorFailMessageScriptableObject>();
            string assetPath = $"{ResourcesRoot}/FailMessageObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindIssue(report, assetPath, nameof(ValidatorFailMessageScriptableObject.requiredTexture));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.ToString(), Does.StartWith(ValidatorFailMessageScriptableObject.Message + "\n"));
        }

        [Test]
        public void ValidateInvokePassDoesNotCreateIssue()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokePassScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokePassObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokePassScriptableObject.OnBuildValidate)), Is.False);
        }

        [Test]
        public void ValidateInvokeInfoDoesNotIncrementSummaryOrFatalCount()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeInfoScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeInfoObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(report.SummaryIssueCount, Is.Zero);
            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokeInfoScriptableObject.OnBuildValidate)), Is.True);
        }

        [Test]
        public void ValidateInvokeWarningIncrementsSummaryOnly()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeWarningScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeWarningObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(report.SummaryIssueCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokeWarningScriptableObject.OnBuildValidate)), Is.True);
        }

        [Test]
        public void ValidateInvokeFatalIncrementsSummaryAndFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeFatalScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeFatalObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(report.SummaryIssueCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokeFatalScriptableObject.OnBuildValidate)), Is.True);
        }

        [Test]
        public void ValidateInvokePrivateMethodIsCalled()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokePrivateScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokePrivateObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsMethodIssue(report, assetPath, "OnBuildValidate"), Is.True);
        }

        [Test]
        public void ValidateInvokeWrongSignatureReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeWrongSignatureScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeWrongSignatureObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindMethodIssue(report, assetPath, nameof(ValidatorInvokeWrongSignatureScriptableObject.OnBuildValidate));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
        }

        [Test]
        public void ValidateInvokeExceptionReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeExceptionScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeExceptionObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindMethodIssue(report, assetPath, nameof(ValidatorInvokeExceptionScriptableObject.OnBuildValidate));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
        }

        [Test]
        public void UnsupportedReferenceFieldTypeReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorUnsupportedFieldScriptableObject>();
            string assetPath = $"{ResourcesRoot}/UnsupportedFieldObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindIssue(report, assetPath, nameof(ValidatorUnsupportedFieldScriptableObject.unsupportedField));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
        }

        [Test]
        public void UnsupportedReferenceFieldTypeReportsOncePerField()
        {
            var firstAsset = ScriptableObject.CreateInstance<ValidatorUnsupportedFieldScriptableObject>();
            var secondAsset = ScriptableObject.CreateInstance<ValidatorUnsupportedFieldScriptableObject>();
            AssetDatabase.CreateAsset(firstAsset, $"{ResourcesRoot}/UnsupportedFieldFirst.asset");
            AssetDatabase.CreateAsset(secondAsset, $"{ResourcesRoot}/UnsupportedFieldSecond.asset");

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(CountFieldIssues(report, nameof(ValidatorUnsupportedFieldScriptableObject.unsupportedField)), Is.EqualTo(1));
        }

        [Test]
        public void ValidateInvokeWrongSignatureReportsOncePerMethod()
        {
            var firstAsset = ScriptableObject.CreateInstance<ValidatorInvokeWrongSignatureScriptableObject>();
            var secondAsset = ScriptableObject.CreateInstance<ValidatorInvokeWrongSignatureScriptableObject>();
            AssetDatabase.CreateAsset(firstAsset, $"{ResourcesRoot}/InvokeWrongSignatureFirst.asset");
            AssetDatabase.CreateAsset(secondAsset, $"{ResourcesRoot}/InvokeWrongSignatureSecond.asset");

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(CountMethodIssues(report, nameof(ValidatorInvokeWrongSignatureScriptableObject.OnBuildValidate)), Is.EqualTo(1));
        }

        [Test]
        public void StandaloneExposedReferenceDoesNotReportMissingReference()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{ResourcesRoot}/StandaloneExposedReference.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorExposedReferencePlayableAsset.targetTransform)), Is.False);
        }

        [Test]
        public void PlayableDirectorMissingExposedReferenceReportsIssue()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{TestRoot}/DirectorMissingExposedReference.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            director.playableAsset = asset;
            string scenePath = $"{TestRoot}/DirectorMissingExposedReference.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                BuildReferenceValidator.ValidationIssue issue = FindIssue(report, assetPath, nameof(ValidatorExposedReferencePlayableAsset.targetTransform));
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Context, Is.EqualTo(director));
                Assert.That(issue.ResolverSource, Does.Contain(scenePath));
                Assert.That(issue.ResolverSource, Does.Contain("Director"));
                Assert.That(issue.ToString(), Does.Contain("Resolver='"));
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void PlayableDirectorResolvedExposedReferencePasses()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            Transform target = new GameObject("Target").transform;
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{TestRoot}/DirectorResolvedExposedReference.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            director.playableAsset = asset;
            director.SetReferenceValue(asset.targetTransform.exposedName, target);
            string scenePath = $"{TestRoot}/DirectorResolvedExposedReference.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorExposedReferencePlayableAsset.targetTransform)), Is.False);
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void SamePlayableAssetIsValidatedPerPlayableDirectorResolver()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var firstDirectorObject = new GameObject("FirstDirector");
            var secondDirectorObject = new GameObject("SecondDirector");
            PlayableDirector firstDirector = firstDirectorObject.AddComponent<PlayableDirector>();
            PlayableDirector secondDirector = secondDirectorObject.AddComponent<PlayableDirector>();
            Transform target = new GameObject("Target").transform;
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{TestRoot}/SharedPlayableAsset.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            firstDirector.playableAsset = asset;
            secondDirector.playableAsset = asset;
            firstDirector.SetReferenceValue(asset.targetTransform.exposedName, target);
            string scenePath = $"{TestRoot}/SharedPlayableAsset.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(CountFieldIssues(report, nameof(ValidatorExposedReferencePlayableAsset.targetTransform)), Is.EqualTo(1));
                BuildReferenceValidator.ValidationIssue issue = FindIssue(report, assetPath, nameof(ValidatorExposedReferencePlayableAsset.targetTransform));
                Assert.That(issue.Context, Is.EqualTo(secondDirector));
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        static bool ContainsIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string fieldName, int? collectionIndex = null)
        {
            return FindIssue(report, sourcePath, fieldName, collectionIndex) != null;
        }

        static bool ContainsMethodIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string methodName)
        {
            return FindMethodIssue(report, sourcePath, methodName) != null;
        }

        static BuildReferenceValidator.ValidationIssue FindIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string fieldName, int? collectionIndex = null)
        {
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.SourcePath == sourcePath && issue.FieldName == fieldName && issue.CollectionIndex == collectionIndex)
                    return issue;
            }

            return null;
        }

        static BuildReferenceValidator.ValidationIssue FindMethodIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string methodName)
        {
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.SourcePath == sourcePath && issue.MethodName == methodName)
                    return issue;
            }

            return null;
        }

        static int CountFieldIssues(BuildReferenceValidator.ValidationReport report, string fieldName)
        {
            int count = 0;
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.FieldName == fieldName)
                    count++;
            }

            return count;
        }

        static int CountMethodIssues(BuildReferenceValidator.ValidationReport report, string methodName)
        {
            int count = 0;
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.MethodName == methodName)
                    count++;
            }

            return count;
        }

        static void CreateFolderIfMissing(string parentFolder, string folderName)
        {
            string path = $"{parentFolder}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parentFolder, folderName);
        }
    }

    public sealed class ValidatorTestBehaviour : MonoBehaviour
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorTestScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorWarningScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Warning)]
        public Texture2D optionalTexture;
    }

    public sealed class ValidatorInfoScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Info)]
        public Texture2D infoTexture;
    }

    public sealed class ValidatorListScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
        public List<Texture2D> textures;
    }

    public sealed class ValidatorDefaultSeverityScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorFailMessageScriptableObject : ScriptableObject
    {
        public const string Message = "Texture must be assigned before building.";

        [ValidateReferenceSet(Message)]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorInvokePassScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Pass();
        }
    }

    public sealed class ValidatorInvokeInfoScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Info("Informational validation message.");
        }
    }

    public sealed class ValidatorInvokeWarningScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Warning("Warning validation message.");
        }
    }

    public sealed class ValidatorInvokeFatalScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Fatal("Fatal validation message.");
        }
    }

    public sealed class ValidatorInvokePrivateScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Warning("Private validation method was called.");
        }
    }

    public sealed class ValidatorInvokeWrongSignatureScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public void OnBuildValidate()
        {
        }
    }

    public sealed class ValidatorInvokeExceptionScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            throw new System.InvalidOperationException("Validation exception.");
        }
    }

    public sealed class ValidatorUnsupportedFieldScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet]
        public string unsupportedField;
    }

    public sealed class ValidatorExposedReferencePlayableAsset : PlayableAsset
    {
        [ValidateReferenceSet]
        public ExposedReference<Transform> targetTransform;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Null;
        }
    }
}
