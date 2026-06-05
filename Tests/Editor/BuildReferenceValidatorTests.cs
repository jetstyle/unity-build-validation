// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System.Collections.Generic;
using JetXR.Unity.BuildValidation;
using JetXR.Unity.BuildValidation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        static bool ContainsIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string fieldName, int? collectionIndex = null)
        {
            return FindIssue(report, sourcePath, fieldName, collectionIndex) != null;
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
}
