# Avalonia NuGet publication preparation

The existing WinForms release path is the template. The package metadata, framework and namespaces are distinct:

| Item | WinForms | Avalonia |
| --- | --- | --- |
| NuGet package ID | CompuMaster.Dms.BrowserUI | CompuMaster.Dms.BrowserUI.Avalonia |
| CLR namespace / assembly | CompuMaster.Dms.BrowserUI | CompuMaster.Dms.BrowserUI.Avalonia |
| Target frameworks | net8.0-windows; net48 | net8.0 |
| Host UI | Windows Forms | Avalonia desktop host (Windows, macOS, Linux) |

The Avalonia package uses Jochen Wezel as author, CompuMaster GmbH as company, the MIT license, the existing package logo, its own README, third-party notices, XML documentation and Source Link/symbol packages. Its direct runtime dependencies are Avalonia 12.1.2, Avalonia.Controls.DataGrid 12.1.2 and CompuMaster.Dms.Providers. Desktop applications also configure Avalonia.Desktop and a theme such as Avalonia.Themes.Fluent, as described in README.md. The library does not depend on the WinForms BrowserUI package.

## Prepare on WKS08 without publishing

Commit the intended inputs, choose a new unused four-component NuGet version and run from the repository root, for example:

```powershell
./.github/scripts/prepare-avalonia-nuget.ps1 -Version 2026.10.27.0
```

This builds Release packages for both the providers and Avalonia with the same version prefix. It verifies the package contents/source revision and installs the exact prepared packages into an independent consumer with an isolated NuGet package directory. The consumer checks resources, German localization, DataGrid styles and dependency isolation without GUI or real-server access.

The new artifacts/avalonia-nuget-* directory contains:

- feed/: both .nupkg files and corresponding .snupkg files;
- preparation.json: exact metadata, dependencies, source commit, versions, packaged files and SHA-256 hashes;
- consumer/ and results/: the standalone consuming project/config and test evidence;
- push-preview.txt: explicit provider-first/Avalonia-second push commands, with an environment-variable placeholder instead of an API key. These commands are never executed by preparation.

NuGet normalizes a trailing zero version component: 2026.10.27.0 is normally represented as 2026.10.27 in package filenames/metadata. The actual normalized version is recorded in preparation.json. A source-branch preview is not a publishable release artifact; rebuild from the final merged main revision.

## Publication prerequisites and order

- Recheck that the chosen version is unused in NuGet, Git tags and GitHub releases; preparation does not reserve it.
- Merge the reviewed implementation PR into main and wait for its complete successful post-merge build-and-test workflow, including the required serialized provider/server coverage. Record any reduced coverage explicitly.
- Verify the publishing account/API-key scope permits the new CompuMaster.Dms.BrowserUI.Avalonia ID as well as CompuMaster.Dms.Providers. Permission to update the existing WinForms package does not establish permission to create this new ID. Never paste a key into source, a report or a command transcript.
- Obtain immediate explicit user approval before creating a release.
- Create the GitHub release with the approved numeric version tag. PublishRelease.yml packs/pushes providers, WinForms and Avalonia and attaches their binaries and the dedicated demos. Do not dispatch a production publish from a feature branch.
- Ensure the matching provider version is available before consumers restore the Avalonia package. The local preview deliberately stages the provider dependency too; pushing Avalonia alone would leave that dependency unavailable.
- Verify all NuGet packages, symbol publication and expected release assets before reporting publication success. The local push template is reference/recovery information, not a replacement for the repository's release process.

## Relevant limitations

User acceptance of the Avalonia implementation is recorded in issue #148 and PR #149. It does not replace pending CI/provider or cross-platform native acceptance. Received-sharing provenance is a separate existing WinForms/provider limitation tracked in issue #150; it is not introduced by this package. No preview changes credentials or clears the real demo disk cache.

Metadata and symbol-publishing references: [Microsoft package authoring guidance](https://learn.microsoft.com/nuget/create-packages/package-authoring-best-practices), [symbol packages](https://learn.microsoft.com/nuget/create-packages/symbol-packages-snupkg).
