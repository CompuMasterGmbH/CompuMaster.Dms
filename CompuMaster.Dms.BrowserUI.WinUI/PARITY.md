# WinUI workflow parity and acceptance

Tracking: [#151](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/151), framework umbrella #115, Avalonia reference #148/#149. WinForms is the functional reference. Received-sharing provenance #150 is a separate enhancement for all supported interfaces.

| Area | Native WinUI implementation | Verification |
| --- | --- | --- |
| Navigation and selection | TreeView, lazy children without dummy rows, scoped roots, relative results, folder/file modes, sorting and multiselect. | Offline workflow tests and rendered native UI tests, including empty root captions. |
| Action availability | Toolbar, file/folder context menus, host flags and provider capabilities. | Seven comparisons against an actual running WinForms reference, covering the mapped toolbar and context actions. |
| Resource actions | File/folder create, copy, inline rename, move, delete and properties. | Shared workflow regression tests; native controls and permission gates. |
| Sharing | Internal users/groups and external links, search/edit/remove, permission dependencies, expiry/password and clipboard. | Eight localized native/WinForms group comparisons and native editor capability tests. |
| Versions | History and independent view/download/restore/remove permissions. | Version snapshot/permission and atomic historical download tests. |
| Transfers | Native Shell pickers, recursive plans, empty folders, conflicts, progress/cancellation, retries/recovery and atomic downloads. | Offline transfer regression tests; native file picker opened and cancelled on WKS08. |
| Windows integration | Association icons plus sharing overlay, original toolbar artwork, clipboard, authenticated in-process moves and outward staged copies. | Five native association probes and native visual review. Interactive Explorer drag/drop acceptance is not exhaustive. |
| Login and demos | General offline demo and four provider demos; common cache/profile/tenant business sources compiled into each UI. | Private-buffer cache/login tests; cached read-only logins for WebDAV, Nextcloud, ownCloud Classic and Scopevisio Teamwork on WKS08. |
| Instance selection | API display names, default-first ordering and tenant constraints. | Offline instance tests; live Scopevisio selection displayed Martin Herber first and opened that instance. |
| Native packaging | Compiled App.xaml, resource metadata, PRI merge, DataGrid styles, localized text, no WinForms/Avalonia runtime dependency. | Independent NuGet consumer with its own package cache, plus Windows CI checks. |

## Evidence and limits

The native suite currently contains 62 passing offline tests with zero failures or skips, using Microsoft.WindowsAppSDK 1.8.260921001 and CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.2. Exact revisions and publication evidence belong in #151 and the implementing PR rather than changing this file after every run.

Live review is read-only. It does not establish remote mutation, every provider/permission combination, exhaustive native drag/drop, ARM64 runtime acceptance, or compatibility with every supported Windows release. Native runtime acceptance was performed on x64 WKS08; the component uses the Windows minimum declared by its SDK/TFM. Existing GUI regression suites and publish verification are separate gates.

Never expose or clear the real demo credential cache. GUI tests and manual review must honor the workstation notice and mutex. Tests of credential persistence use private test-owned buffers.
