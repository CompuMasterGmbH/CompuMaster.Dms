# CompuMaster.Dms.BrowserUI.WinUI

Native WinUI 3 DMS browser controls and dialogs for Windows. Implementation and acceptance are tracked in issue #151. WinForms remains the workflow reference. See [PARITY.md](PARITY.md) for the workflow matrix and acceptance limits.

The package targets .NET 8 and Windows 10 version 1809 or later, using Windows App SDK 1.8.260921001 and the free Community Toolkit DataGrid 7.1.2. Consuming applications must initialize WinUI and install or bundle the corresponding Windows App SDK runtime. The standalone x64 demos use unpackaged self-contained Windows App SDK deployment. No Microsoft Store publisher registration or paid controls are required. ARM64 consumers can build their own host; native ARM64 acceptance is separate from the verified x64 path.

## Consuming application

Use a normal WinUI 3 application targeting `net8.0-windows10.0.19041.0` or a compatible newer Windows TFM. Reference the package and retain the application's compiled `App.xaml`, generated metadata provider and normal WinUI startup. Merge `XamlControlsResources` and the free DataGrid styles into the application resources:

```xml
<ResourceDictionary.MergedDictionaries>
  <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
  <ResourceDictionary Source="ms-appx:///CommunityToolkit.WinUI.UI.Controls.DataGrid/Themes/Generic.xaml" />
</ResourceDictionary.MergedDictionaries>
```

Open a browser for an authorized provider from the UI thread:

```csharp
var browser = new CompuMaster.Dms.BrowserUI.WinUI.DmsBrowser(provider,
    new CompuMaster.Dms.BrowserUI.WinUI.DmsBrowserOptions
    {
        InitialFolder = "Documents",
        AllowedActions = CompuMaster.Dms.BrowserUI.WinUI.FileOrFolderActions.AllowSelectOnly
    });
var selected = await browser.ShowDialogAsync(ownerWindow);
```

`ShowAsync()` opens a standalone window. `DmsBrowserControl` embeds in existing windows; call `AttachToWindow(ownerWindow)` before `InitializeAsync()`. The options/results, original-exception diagnostics and provider-neutral action flags follow the Avalonia/WinForms workflows. Grant only the actions the host intends to permit. Version view/download/restore/removal flags remain independent.

## Workflows and Windows integration

The browser provides scoped folder/collection navigation, relative selection, lazy expansion without visible placeholder nodes, folder/file modes, sortable columns, multi-file selection, contextual actions, inline folder rename, properties, user/group/link sharing and version history. It retains all eight grouped sharing sections from WinForms. Transfers include recursive directories, empty folders, overwrite/merge decisions, atomic local replacement, progress/cancellation and failed-upload recovery.

Native Shell dialogs preserve initial directories, multiple file/folder selection and window ownership. Clipboard operations use the Windows clipboard. Explorer drops upload through the same local-path and destination guards. Remote moves require the active in-process grant, matching provider/instance/root and revalidated destination identities. Outward drags provide staged copies; external drop effects never authorize deleting a remote DMS resource. Previews and drag staging remain tracked for cleanup; unresolved cleanup failures are exposed to hosts.

File-type icons use Windows Shell associations without downloading a DMS resource or creating a placeholder file. Shared files retain their document/type icon with a small sharing overlay. Toolbar/context-menu artwork and linked localized resources come from the existing BrowserUI. The component has no WinForms or Avalonia runtime dependency.

## Demos and verification

Build all five demos with `.github/scripts/build-winui-demos.ps1`. Each dedicated executable starts its provider-specific login form; `--offline` selects a process-local demonstration provider. The general demo starts offline and also accepts `--webdav`, `--nextcloud`, `--owncloud` or `--scopevisio`.

The login forms use the existing WinForms cache keys and environment precedence, including Scopevisio organization selection and tenant guards. Tests inject private buffers; never clear the developer's real credential cache as a test setup or cleanup step.

`.github/scripts/test-winui.ps1` runs native/offline NUnit tests, including actual WinForms comparisons, under the workstation notice/mutex. `.github/scripts/test-winui-package.ps1` packs and installs into a separate native app with no repository project references and an independent NuGet cache. The Windows-only `WinUI.yml` workflow runs the same build/tests/consumer. Offline checks and native rendering do not establish real-server mutation acceptance; see #151 for exact evidence and remaining gates.

## Licensing and publication

The DMS component and Community Toolkit DataGrid are MIT. Microsoft SDK/runtime binaries retain their Microsoft software license terms; preserve `WINDOWS-APP-SDK-LICENSE.txt`, `THIRD-PARTY-NOTICES.txt` and notices accompanying transitive packages. See `PUBLISHING.md` for bootstrap and normal release preparation.
