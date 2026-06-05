# BuildValidation

BuildValidation is a JetXR Unity package for checking required object references before a player build is created.

The package adds a field attribute that marks a serialized Unity object reference as required. During a build, BuildValidation scans build content and reports every missing marked reference it finds. Fatal validation issues stop the build after the scan is complete.

## Installation

Add the package to the Unity project as an embedded package:

```text
Packages/style.jetxr.buildvalidation
```

The package id is:

```text
style.jetxr.buildvalidation
```

## Usage

Add the namespace to a runtime script:

```csharp
using JetXR.Unity.BuildValidation;
using UnityEngine;

public sealed class ExampleMapSettings : MonoBehaviour
{
    [ValidateReferenceSet]
    public Texture2D requiredMap;
}
```

When no severity is provided, the attribute uses `ReferenceValidationSeverity.Fatal`.

The attribute can be used on serialized fields that store Unity object references:

```csharp
[ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
[SerializeField] Material requiredMaterial;

[ValidateReferenceSet("Main audio clip is required for this asset.")]
public AudioClip mainClip;

[ValidateReferenceSet(ReferenceValidationSeverity.Info)]
public AudioClip[] clips;

[ValidateReferenceSet(ReferenceValidationSeverity.Warning)]
public List<GameObject> prefabs;
```

Fields must be serialized by Unity. Public fields and private fields with `[SerializeField]` are supported. Static fields and non-serialized fields are ignored.

## Manual Validation

Run validation without starting a build from:

```text
Tools/Validation/Validate Required References For Build
```

The same validation path is used for manual validation and build validation.

## Build Behavior

BuildValidation checks:

- enabled scenes from Build Settings;
- MonoBehaviour components in those scenes;
- prefabs included through build dependencies;
- ScriptableObject assets included through build dependencies;
- other serialized Unity object assets included through build dependencies;
- assets under `Resources`, even when they are not referenced by a scene.

For each missing reference, the Unity Console receives a message with the severity, asset or scene path, object path, component or asset type, field name, and serialized property path. Console messages use the relevant Unity object as context, so selecting the log entry can ping or select the related asset or object.

If a custom fail message is passed to the attribute, that message is written as the first line of the Console entry. The default validation details are written after it.

Severity controls the build result:

- `Info` logs a regular Console message and does not appear in the final validation summary.
- `Warning` logs a warning and does not fail the build.
- `Fatal` logs an error and fails the build after all validation issues have been reported.

Array and `List<T>` fields are checked per element. Empty arrays and lists are valid. Null elements are reported with their element index.
