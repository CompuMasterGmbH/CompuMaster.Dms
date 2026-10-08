# CompuMaster.Dms
DMS Browser component for Scopevisio Teamwork, CenterDevice, WebDAV (e.g. NextCloud, OwnCloud)

[![Github Release](https://img.shields.io/github/release/CompuMasterGmbH/CompuMaster.Dms.svg?maxAge=2592000&label=GitHub%20Release)](https://github.com/CompuMasterGmbH/CompuMaster.Dms/releases) 
[![NuGet CompuMaster.Dms](https://img.shields.io/nuget/v/CompuMaster.Dms.Providers.svg?maxAge=2592000&label=NuGet%20CM.Dms.Providers)](https://www.nuget.org/packages/CompuMaster.Dms.Providers) 
[![NuGet CompuMaster.Dms.BrowserUI](https://img.shields.io/nuget/v/CompuMaster.Dms.BrowserUI.svg?maxAge=2592000&label=NuGet%20CM.Dms.BrowserUI)](https://www.nuget.org/packages/CompuMaster.Dms.BrowserUI) 

## CompuMaster.Dms ecosystem

These separately versioned repositories form the CompuMaster.Dms ecosystem; they are not Git submodules. Repository names, local directory names, and NuGet package IDs can differ.

| Component | Repository | Responsibility |
|---|---|---|
| CompuMaster.Dms | [CompuMaster.Dms](https://github.com/CompuMasterGmbH/CompuMaster.Dms) | Provider-independent DMS workflows, provider adapters, and BrowserUI. |
| CompuMaster.Ocs | [CompuMaster.OpenCollaborationService](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService) | ownCloud/Nextcloud OCS protocol, sharing, and account/group operations. |
| CompuMaster.Scopevisio.OpenApi | [CompuMaster.Scopevisio.OpenApi](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.OpenApi) | Scopevisio OpenScope REST API and authorization. |
| CompuMaster.Scopevisio.Teamwork | [CompuMaster.Scopevisio.Teamwork](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork) | Teamwork integration connecting OpenScope authorization with CenterDevice clients. |
| CompuMaster.CenterDevice | [CompuMaster.CenterDevice.IO](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO) | CenterDevice REST and file-system SDK; the DMS package dependency is CompuMaster.CenterDevice.Rest. |

DMS uses WebDAV for ownCloud/Nextcloud file operations and OCS for supported sharing operations. Teamwork builds on OpenScope and CenterDevice clients. Actual package versions and optional source references are defined by the project files on the branch being tested; an unmerged upstream change is not automatically available in DMS.

For cross-repository work, evaluate the affected libraries before adding a DMS-only workaround. Keep reusable protocol, authentication, transport, and SDK behavior in its owning library, while DMS retains the common workflow/capability model and UI mapping. Preserve standalone library consumers, synchronous APIs, identity semantics, and existing defaults.

Track each upstream change in an issue in its owning repository, link its implementing PR there, and reference that issue as a dependency in the affected DMS issue. Keep reciprocal links, exact commit/package versions, integration order, and verification status current. Distinguish implemented, tested, merged, published, and consumed states; do not close a dependency merely because an upstream branch is green.

Shared remote test systems require coordinated exclusive access across repositories and local sessions, including setup and cleanup. Identically named GitHub Actions concurrency groups in different repositories do not provide a shared lock. See [AGENTS.md](AGENTS.md) for working rules.

## Simple download/installation using NuGet
```powershell
Install-Package CompuMaster.Dms.Providers
```
respectively
```powershell
Install-Package CompuMaster.Dms.BrowserUI
```
Also see: https://www.nuget.org/packages/CompuMaster.Dms.Providers and https://www.nuget.org/packages/CompuMaster.Dms.BrowserUI

## Modules Overview

There are following main modules for your use:
* CompuMaster.Dms.Providers – The base library (compatible with .NET Standard/Core/Framework) for your own implementations to access your DMS systems (with build-in support for WebDAV (e.g. NextCloud, OwnCloud) and Scopevisio Teamwork (a flavored CenterDevice implementation)
* CompuMaster.Dms.BrowserUI – An implementation of all required forms and dialogs with System.Windows.Forms (requires .NET Framework 4.8 or .NET 5.0-Windows). The UI follows `CurrentUICulture`, provides neutral English resources and a German translation, and falls back to English for unsupported languages. Use it to
  * download and upload files 
  * setup user sharings and link sharings (if supported by the underlying provider)
  * provide several levels of allowed actions depending on required action context (manage folder structure only without viewing files, view and edit folder structure and files, or view everything without editing, etc.)
* CompuMaster.Dms.TestDemo.WebDav – A demo application to show functionality of CompuMaster.Dms.BrowserUI components with a WebDAV server (based on System.Windows.Forms which requires .NET Framework 4.8 or .NET 5.0-Windows)
* CompuMaster.Dms.TestDemo.OwnCloudClassic – A dedicated ownCloud Classic demo using its own local credential store.
* CompuMaster.Dms.TestDemo.Nextcloud – A dedicated Nextcloud demo using its own local credential store and accepting either the instance URL or a complete WebDAV URL.
* CompuMaster.Dms.TestDemo.ScopevisioTeamwork – A demo application to show functionality of CompuMaster.Dms.BrowserUI components with Scopevisio Teamwork (based on System.Windows.Forms which requires .NET Framework 4.8 or .NET 5.0-Windows)

All four demo logins use a shared document-management illustration. Applications can supply their own or provider-specific artwork through the additive `LoginForm(formIcon, loginImage)` constructor or the `LoginImage` property. See [demo login artwork](CompuMaster.Dms.TestDemo.WebDav/DemoAssets/README.md) for image ownership, reset behavior and asset provenance.

### Asynchronous browsing

Use `ListDirectoryEntriesAsync` and `ListFileEntriesAsync` to populate a browser tree and file list. Native Scopevisio/CenterDevice listings retain resource identities, paths, sizes, timestamps, child metadata and sharing indicators, including download links, upload-only links and visible/hidden user/group shares. They use the existing resource conversion and batched upload-link lookup without first resolving every principal name and link's detailed settings. Retrieve full details for the selected resource through an item or identifier lookup before displaying properties or managing shares. Existing full-snapshot `ListAll...Async` methods retain their behavior; other providers and existing derived implementations use the compatible virtual-method fallback.

BrowserUI uses these entry listings for navigation and resolves selected details asynchronously, retaining the selected resource's path and parent context even for duplicate names. Pending operations keep the window active with a wait cursor and lock its content/action controls until completion; success, failure and cancellation restore their previous states. API request allowances remain in force.

### WebDAV sharing capabilities

The WebDAV provider keeps generic WebDAV servers provider-neutral. For recognized Nextcloud and ownCloud personal-file endpoints, it probes the Open Collaboration Services (OCS) API and enables user, group, and public-link sharing only when that probe succeeds. OCS permission bits are mapped without silently changing their meaning; view and download remain coupled because the classic OCS bit field cannot represent modern Nextcloud download restrictions separately. Server policies such as maximum link-name length remain server-validated because they differ between products and deployments.

Sharing discovery requires both a successful OCS share-list request and the authenticated `cloud/capabilities` response. The adapter honors `api_enabled`, ownCloud's `can_share` and `public.can_create_public_link`, the current Nextcloud `group.enabled` or legacy `group_sharing`, and `public.enabled`/`public.upload`. Missing or malformed capability data does not enable optional operations. A failed discovery leaves ordinary WebDAV browsing available. Product identification alone never grants capabilities. Password, expiration, recipient and per-resource restrictions remain subject to server validation.

OCS user IDs remain operation identifiers. Display names use the sharee display name or label, with the common ID fallback. The current OCS client does not expose a distinct login-name field for these responses; `LoginName` therefore stays unknown rather than copying the ID or guessing from additional information. Existing WebDAV owner metadata takes precedence over supplemental share-owner metadata.

ownCloud Infinite Scale personal-file compatibility endpoints are only candidates for the same capability probe; they have not been verified against a real Infinite Scale server. Space endpoints (`/dav/spaces/<space-id>`) remain regular WebDAV endpoints because their sharing model uses LibreGraph roles and item identifiers; a future LibreGraph adapter can add that capability without changing the common DMS sharing API.

The isolated sharing tests cover policy differences and identity mapping. The ownCloud Classic and Nextcloud CI fixtures additionally require successful sharing discovery and exercise public-link create/read/update/delete with verified test-owned directory cleanup. User/group mutations are covered in isolation, not yet against explicitly configured remote recipient accounts. See the upstream capability contracts for [Nextcloud](https://github.com/nextcloud/server/blob/master/apps/files_sharing/lib/Capabilities.php) and [ownCloud Classic](https://github.com/owncloud/core/blob/master/apps/files_sharing/lib/Capabilities.php).

Development and remote integration-test guidance is documented in [TESTING.md](TESTING.md).
See [Local test credentials](LOCAL_TEST_CREDENTIALS.md) for the mapping between demo applications, environment variables, and local integration-test credential stores.

## WebDAV resource owners

`WebDavDmsProvider` reads owner metadata returned for each file or folder. Generic WebDAV servers may provide an owner principal through the optional WebDAV ACL `DAV:owner` property. ownCloud Classic and Nextcloud may provide `oc:owner-id` and `oc:owner-display-name`; other servers may expose these properties too. Availability depends on the server and resource, including whether an item is shared. The provider requests the properties by name because some servers omit them from `allprop` responses even when asked through `include`. It prefers the account properties when available. It leaves the owner blank when the server does not return usable owner metadata, including when a requested property is missing or forbidden. A known owner ID without a display name is shown as the ID. This does not require user-directory or sharing support.

## Screenshots

### Login form, customized for WebDAV
![image](https://user-images.githubusercontent.com/3033827/126822624-fd9a0b0b-6762-41d9-a1c6-7bb285fdebdd.png)

### Browser dialog window
![image](https://user-images.githubusercontent.com/3033827/126822834-e87c9897-1978-4f84-9fae-52e276daaf6d.png)

### Extended file properties windows
![image](https://user-images.githubusercontent.com/3033827/126822920-08a09683-a884-484b-a72d-724a7acfd41a.png)

### Sharing setup
Dialogs for sharing setup for internal users (user accounts) or external users (web links) depend on DMS provider
