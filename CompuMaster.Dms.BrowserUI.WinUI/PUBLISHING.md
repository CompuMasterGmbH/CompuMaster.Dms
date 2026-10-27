# WinUI package publishing

The package ID and namespace are `CompuMaster.Dms.BrowserUI.WinUI`. It targets `net8.0-windows10.0.19041.0`, with Windows 10 version 1809 as the minimum supported runtime. Native acceptance is Windows-specific.

Prepare immutable packages from a clean, committed checkout:

```powershell
./.github/scripts/prepare-winui-nuget.ps1 -Version 2026.10.27.1
```

Normal release preparation stages matching Providers and WinUI packages. The published-provider override is restricted to prerelease bootstrap and requires all referenced provider APIs to exist in that published version. The first package-ID push used source 271a53188a5b209ae091678754b0cfdd66be6602, WinUI 2026.10.27.1-preview.1 and public Providers 2026.10.27; later received-sharing sources require the matching newer provider package and must not reuse that historical override. Preparation records source revisions/hashes, checks symbols/licenses/PRI/resources and runs an independent native consumer under the workstation GUI notice/mutex. It writes publishing command previews and never reads an API key, pushes packages or creates a release.

The user explicitly authorized the first prerelease push from the development workstation to create the new package ID. Use the exact prepared `.nupkg` beside its `.snupkg`, with the existing encrypted user NuGet configuration. Never print/decrypt the stored key or copy demo credentials. Verify gallery/public-feed availability and fresh installation after upload; a successful upload response alone is not public availability.

Production publishing uses a new numeric release tag on merged main through `PublishRelease.yml`. The release packages Providers, WinForms, Avalonia and WinUI, including their expected demo assets. Preserve AGENTS.md release/GUI-only local-verification rules and the user's explicit release authorization. Do not dispatch a production publish workflow from a branch name. Verify all package uploads, public-feed availability and all release assets before claiming publication is complete.
