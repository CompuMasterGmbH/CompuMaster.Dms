# CompuMaster.Dms.BrowserUI.Avalonia

Avalonia desktop browser, embeddable control, file/folder picker and sharing dialogs for the existing provider-independent DMS API. The functional reference is WinForms at commit `caeb185`. Implementation and acceptance are tracked in [#148](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/148), under [#115](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/115).

## Installation and host setup

The intended NuGet package ID is **CompuMaster.Dms.BrowserUI.Avalonia**. This new package is not published yet; use a locally packed feed until the reviewed release has been approved. The library targets `net8.0` without WinForms or System.Drawing dependencies.

Use an Avalonia desktop host with `<FluentTheme />` in its application styles. The browser installs its own Fluent DataGrid styles. Dependencies are `Avalonia 12.1.2`, `Avalonia.Controls.DataGrid 12.1.2` and the matching `CompuMaster.Dms.Providers` package. The demo adds `Avalonia.Desktop 12.1.2` and `Avalonia.Themes.Fluent 12.1.2`. Hosts should use matching Avalonia versions. Version 12.1.2 is pinned consistently because the free DataGrid is available at this version.

After creating a login profile or authorizing a provider:

```csharp
using CompuMaster.Dms.BrowserUI.Avalonia;

var browser = new DmsBrowser(authorizedProvider, new DmsBrowserOptions
{
    FormTitle = "Select a document",
    InitialFolder = "/Documents",
    SelectedFolder = "Projects",
    BrowseMode = BrowseModes.FoldersAndFiles,
    AllowedActions = FileOrFolderActions.AllowSelectOnly,
    DialogOperationMode = DialogOperationModes.ReturnSelectedItems
});
var selection = await browser.ShowDialog<DmsBrowserSelection?>(owner);
if (selection != null)
{
    var relativeDirectory = selection.SelectedFolder; // null means the configured root.
    var files = selection.Files;
}
```

The profile constructor authorizes asynchronously on opening. The provider constructor preserves an already selected instance. Embed `new DmsBrowserControl(provider, options)` in a host window; it initializes after attachment, and `InitializeAsync()` can be awaited explicitly. Handle `SelectionConfirmed`/`Cancelled`, read `Selection`, and call `CleanupTemporaryFiles()` when disposing the host. Use browser APIs and events on the Avalonia UI thread.

Action flags have the same names and numeric values as WinForms. Sharing additionally requires provider support. The provider retains final authority to reject unsupported server operations. Configure instance selection explicitly:

```csharp
options.RemoteInstance = new RemoteInstanceSelector(
    RemoteInstanceSelector.StartupInstance.SelectionDialog,
    RemoteInstanceSelector.ShowDefaultSelectionDialogAsync);
options.AllowedActions |= FileOrFolderActions.AllowSwitchDmsInstance;
```

Use `new RemoteInstanceSelector("instance-id")` for explicit startup selection, or the default constructor to preserve the provider instance. A custom asynchronous selection delegate is supported.

`LocalParentMustFolder` confines uploads/downloads to a local directory and descendants, resolving existing symbolic links. Local default directories are validated. Temporary read-only previews have a separate browser-owned lifecycle, matching WinForms; replace `OpenDownloadedFileItem` for a custom viewer. Cleanup affects only this browser's preview directories and reports failures.

## Functional parity

| WinForms workflow | Avalonia equivalent |
| --- | --- |
| Browser and selection result | `DmsBrowser`, `DmsBrowserSelection`, embedded `DmsBrowserControl` |
| Folders/collections, initial/relative selection | Navigation within the configured root; nearest existing ancestor for a missing initial child |
| Lazy child metadata | Known zero has no expansion placeholder; unknown/positive metadata load on expansion; lightweight listings and selected-resource detail resolution |
| File listing | Multiple selection; name, numeric size and modification sorting; duplicate names highlighted |
| Allowed actions | Same flag values, rechecked at mutation/transfer entry points |
| Create/delete/copy/rename/move | Toolbar/context menus, destination picker, name validation, overwrite/merge confirmation |
| Partial failures/stale resources | Refresh while preserving the originating exception; nearest surviving folder selection |
| Upload/download | Native storage pickers; multiple files and recursive folders, empty directories, local restrictions, overwrite/skip/cancel decisions with apply-to-all and resource identity |
| Transfer progress/recovery | Per-file states and byte progress, aggregate progress, rate/elapsed/ETA, cancellation, upload retry/continue and atomic local downloads |
| File-version history | Capability/permission-gated listing, historical download, restore and individual removal with independent rights and confirmation |
| Drag-and-drop | Local files/folders to the selected or pointed-at remote folder; internal remote moves; native outward copies through staged storage items |
| Open/preview | Read-only temporary download, replaceable shell-open delegate, repeatable cleanup |
| User/group sharing | Create/edit/delete, owner/current/existing-user exclusion, hidden shares, localized permissions |
| External links | Create/edit/delete/copy URL; password, expiry date/time, applicable byte/download/upload limits, usage counters |
| Provider differences | Internal Teamwork/CenterDevice and OCS permission adapters; provider rejection for unsupported operations |
| Properties | Path/parent/size, owner, version/archive metadata, locks, collisions, sharing/links, hashes, referenced IDs |
| Instance selection | Default/explicit/dialog startup, cancellation, optional switching and view reset |
| Async behavior | Provider async APIs, busy indicator, exclusive operations, close guard and opt-in diagnostics with original exceptions and safe default error messages |
| Localization/interaction | All linked WinForms language resources, English fallback, RTL layout, accessible controls, F5/F2/Delete/Escape, context menus |

Framework types and native chrome differ. Portable file icons replace Windows shell icon extraction. Storage pickers, clipboard and default-file launching come from the desktop host. Outward drag uses downloaded browser-owned copies and native Avalonia storage items instead of Windows OLE virtual streams. Preparing those copies can take time before the native drag begins; they remain tracked until browser cleanup. The dialog-based download workflow is available on every desktop target. Native shell/drag acceptance remains separate from isolated tests.

The WinForms BrowserUI contains eight GroupBoxes, all represented by labeled Avalonia GroupBox controls with the same localized headings and field grouping:

| Dialog | Groups |
| --- | --- |
| Sharing overview | Internal user/group shares; external link shares (2) |
| User/group sharing editor | General settings; permissions (2) |
| External link editor | General settings; permissions; limits; statistics (4) |

The standard and link editors remain scrollable at smaller window sizes. The main browser, instance selector, version history, transfer/conflict dialogs and dedicated demo login forms have no additional WinForms GroupBoxes to map. Paired Windows tests compare the actual reference group headings for the overview, user, group and link editors in English and German.

## Verification and platform status

```powershell
dotnet test CompuMaster.Dms.Test.BrowserUI.Avalonia -c CI_CD
dotnet run --project CompuMaster.Dms.TestDemo.Avalonia -c CI_CD
pwsh .github/scripts/test-avalonia-package.ps1 -Configuration CI_CD
```

There are also dedicated Avalonia applications for WebDAV, Nextcloud, ownCloud Classic and Scopevisio Teamwork, each opening its own sign-in window without provider arguments. The general demo remains offline by default. See [demo applications and launch commands](../CompuMaster.Dms.TestDemo.Avalonia/README.md). Server-mutating demo actions require coordinated exclusive test-server access.

The dedicated CI matrix builds, runs isolated headless tests and installs a freshly packed package into a separate consumer on Windows/Linux/macOS without shared remote accounts. Native Windows review is separate from headless tests. Native Linux/macOS file-picker/clipboard/shell acceptance and real-server BrowserUI acceptance remain explicit gates in #148; passing a build or headless test does not complete them.

Linux hosts need Avalonia/.NET native dependencies, including X11 and fontconfig. macOS uses Avalonia's backend without a MAUI workload. See [supported platforms](https://docs.avaloniaui.net/docs/supported-platforms) and [Linux deployment prerequisites](https://docs.avaloniaui.net/docs/deployment/linux).

## Operating-system file icons

The file list initially shows the existing application icon, then replaces it asynchronously with the operating system's file-type icon. Lookup is cached by normalized extension and display size, with bounded cache/concurrency. It never downloads a DMS file or creates a local placeholder. Shared files retain the same sharing overlay artwork/backplate used by WinForms. Folder/collection/root artwork is unchanged. Unsupported platforms and unavailable native backends retain the existing fallback.

- Windows uses [Shell file associations](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow) with `SHGFI_USEFILEATTRIBUTES` on a background STA thread. Native icon, bitmap and device-context handles are released after copying pixels.
- macOS uses [NSWorkspace content-type icons](https://developer.apple.com/documentation/appkit/nsworkspace/icon(for:)) through AppKit/UniformTypeIdentifiers on the native main thread, with the older file-type API as fallback. An autorelease pool bounds temporary Objective-C objects.
- Linux uses [GIO MIME-type lookup](https://docs.gtk.org/gio/func.content_type_guess.html) and the [freedesktop icon-theme rules](https://specifications.freedesktop.org/icon-theme/latest/), including user overrides, inherited themes, scaled sizes and `hicolor`. KDE/GTK settings and GNOME's configured theme are consulted. PNG icons are decoded directly; installed librsvg/Cairo render SVG icons. Missing optional libraries/theme assets fall back to the application artwork. These system libraries and theme icons are not bundled or redistributed in the package.

`CompuMaster.Dms.Test.FileIconProbe` checks five association types on the process main thread on each CI OS. It is separate from headless UI tests, so macOS actually exercises AppKit instead of silently using the fallback from a test worker thread. A successful probe is icon-backend coverage, not full native file-picker/clipboard/drag acceptance.

## Direct WinForms comparison

`CompuMaster.Dms.Test.BrowserUI.Parity` runs the actual WinForms browser against the same in-memory resources as Avalonia on Windows. It compares action availability, permissions, selection, browse-mode transitions, folder/file context menus, scoped collection restrictions and instance order/preselection. Native ToolStrip overflow packing is treated as layout; items remain available through the overflow menu. Current/selected instances appear first, followed by culture-aware display-name order and ordinal ID tie-breaking in both UIs.

These paired cases complement the workflow tests. They do not establish that every sharing/version/transfer dialog and every native interaction on all platforms has passed a complete side-by-side acceptance run. Those gates remain explicit in #148.

## Licensing and distribution

The component and Avalonia free framework/DataGrid/Fluent controls use MIT licensing. No Pro/XPF, paid controls, new publisher account or paid publishing service is required. NuGet distribution reuses the release workflow; permission for the new package ID must be checked before release. Publishing still requires explicit release approval after merge and successful required checks.

`THIRD-PARTY-NOTICES.txt` retains Avalonia's copyright and MIT permission notice. Keep it and the licenses/notices supplied with redistributed transitive packages, including desktop rendering/native assets. Dependencies are referenced rather than vendored; existing provider dependencies retain their terms. Images are linked unchanged from the existing BrowserUI assets. Existing WinForms/provider packages and APIs are unchanged.

WinForms remains the leading implementation. Carry future applicable features/fixes to Avalonia and track verification in their originating issue/PR.

Appearance supports System, Light and Dark modes plus optional scoped light/dark palettes. See [browser appearance](https://github.com/CompuMasterGmbH/CompuMaster.Dms/blob/main/docs/BROWSER_APPEARANCE.md) for examples, owned-dialog behavior and platform limitations.
