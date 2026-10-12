﻿# File-version contracts and cloud adapters

The provider-neutral workflow is additive and synchronous. No general async API expansion under #9 is introduced. Existing providers inherit unsupported defaults; existing clients, explicitly selected instances and package pins retain their behavior.

```vb
Dim history As List(Of DmsFileVersion) = provider.ListFileVersions(selectedFile)
Using destination As Stream = File.Create(localPath)
    provider.DownloadFileVersion(history(0), destination)
End Using
provider.RestoreFileVersion(history(0))
'Reload current-file metadata and history after successful restore/removal.
```

BaseDmsProvider exposes independent SupportsFileVersionHistory, SupportsFileVersionDownload, SupportsFileVersionRestore and SupportsFileVersionRemoval properties, false by default. ListFileVersions takes a selected DmsResourceItem; DownloadFileVersion takes a DmsFileVersion and caller-owned writable Stream; RestoreFileVersion and RemoveFileVersion take a listed DmsFileVersion and return no snapshot. A caller must reload current metadata and history after either mutation. Capability properties describe implemented workflows, not caller permission, per-resource authorization or guaranteed retained history. The server remains authoritative. Missing/denied/unavailable services are errors, never an empty/pruned history.

DmsFileVersion copies FileID, FileFullName and FileName from the selected file and retains opaque VersionID independently from labels or dates. LastModificationUtc, VersionDateUtc, ContentLength, AuthorID, AuthorDisplayName, Label, ContentType and ProviderSpecificHashOrETag are optional. VersionDateUtc represents creation/upload time separately from historical file modification time; the original nine-argument constructor remains intact and a separate full overload supplies this optional value. Cloud snapshots currently leave VersionDateUtc unknown. Unknown length/time is different from zero/epoch. The model has no current-version flag: do not infer current status from ordering or timestamps. Actions use snapshots from the same provider/account; the WebDAV adapter keeps an internal SDK snapshot bound to its client and original file/version identity. Reauthorization or a different provider rejects stale snapshots; restore/removal verify that the current path still identifies the same file. Replaced files fail even when history is empty.

Historical download preserves the caller's stream ownership even on failure, and may leave partial data. Browser callers should write to an owned temporary file, commit the destination only after success and remove partial files after failure. These new common synchronous methods do not promise active cancellation. SDK-native async/cancellation remains available to standalone CompuMaster.Ocs consumers; BrowserUI can use its existing worker-operation pattern and wait until an active call completes before closing.

## Cloud protocol support

| Provider | Listing/download | Restore | Individual removal |
| --- | --- | --- | --- |
| Nextcloud | DAV `versions/USER/versions/FILEID`, selected GET | MOVE to the special restore collection | Only when authenticated `files.version_deletion` is explicitly true |
| ownCloud Classic | DAV `meta/FILEID/v`, selected GET | Conditional COPY to the existing current file | Unsupported by this adapter |
| ownCloud Infinite Scale | DAV meta version namespace with opaque IDs; the 8.2 collection row identifies the metadata file and children remain below `/v/` | COPY restores the version namespace's resource ID; the 8.2 server does not enforce the submitted destination ETag condition | Unsupported; no Classic/Nextcloud inference |
| Generic WebDAV / NoDms / manual URL | Unsupported | Unsupported | Unsupported |

The adapter requires an observed server family and authenticated `files.versioning=true`. Authenticated `cloud/user` discovery retains the protocol ID separately from display/login names; explicit `/dav/files/USER/` addresses provide a fallback when user discovery is unavailable. Legacy `/remote.php/webdav` and `/webdav` folders map to the equivalent personal DAV address only with a confirmed protocol ID. Infinite Scale `/dav/spaces/RESOURCEID/` and `/remote.php/dav/spaces/RESOURCEID/` retain their opaque space ID and selected subfolder. Space version discovery is independent of classic sharing and does not enable an OCS sharing adapter. The SDK's `DavFileVersionEndpoint` preserves server prefixes and rejects unsafe or ambiguous mapping. Missing/disabled/non-Boolean version capabilities retain unsupported defaults. Read-only access can still list/download authorized history; denied restore/removal retains the original server status and is never replayed. See [the SDK protocol matrix](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService/blob/main/FILE_VERSIONS.md) and [OCS #11](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService/issues/11). Actual new server acceptance remains tracked separately from these isolated mapping tests.

## Exact local dependency and validation

Default builds now select the published CompuMaster.Ocs 2026.10.12.0 package (normalized NuGet version 2026.10.12) and compile the existing cloud-version adapter and its regressions. The verified release and actual registry package identify repository commit `086f0b3299c73585d30152cb896cced8fe08638d`; all three package DLLs match the release archive. OcsFileVersionsProject remains available as an advanced explicit source override. Other SDK package pins and CenterDevice/Teamwork compile gates remain unchanged. The original source checkpoint `ab3d66db0d0f14f027e965664c4ae406fd90b26c` / tested `6c40ead5a9a9198b9feb9b1015636ddc2cafdf34` belongs to the initial implementation evidence below, rather than the current default package.

Initial source-opt-in Windows evidence, before default activation:

- OCS: 33 isolated version cases on net8.0 and net48; all three SDK targets build without warnings/errors.
- DMS: 23 isolated contract/timestamp/adapter cases and 108 cases including existing WebDAV sharing, owner metadata and native-upload regressions on net8.0 and net48 with the exact SDK checkpoint; library netstandard2.0/net48/net6.0 builds without warnings/errors. Failed account authorization invalidates the previous version session before any network work, preventing old-account snapshots from remaining usable.
- At that initial checkpoint, the default package path built without cloud version capabilities. No real DMS server was contacted by those checks.

Default-package example: `dotnet test CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj -f net8.0 --filter "FullyQualifiedName~DmsFileVersionContractTest|FullyQualifiedName~WebDavFileVersionTest"`. The same focused tests run on net48. For advanced isolated SDK development, append `-p:OcsFileVersionsProject=<absolute SDK project path>` only after verifying the immutable local revision and clean source; no moving branch is selected implicitly. The published-package activation passed 663 isolated Provider cases on each Windows/net8.0 and Windows/net48 target, with four pre-existing ignored settings-persistence cases on each. All three Provider and both BrowserUI targets build without warnings/errors; no GUI or remote-server tests were run by these default-activation checks.

BrowserUI integration belongs to the coordinated DMS UI chat under [#100](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/100). Independent caller flags: history 256, restore 512, individual removal 1024; historical download uses existing download bit 8. History/restore/removal never imply upload or current-file deletion rights. The coordinated BrowserUI implementation and its isolated verification are described below; the cloud task keeps its original UI-reference branch unmerged.

Follow-up cloud mapping in merged DMS PR #105 covers legacy aliases, Infinite Scale spaces, endpoint-shaped subfolders, authenticated user identity and unsupported versioning. Its source-based evidence includes 124 combined isolated cloud-version/sharing/owner/native-upload cases on each Windows/net8.0 and Windows/net48 target against reviewed OCS PR #14 source. On the final published SDK commit, disposable run 38237528622 verifies Infinite Scale 8.2.1 and Nextcloud 34.0.3 legacy/read-only/disabled-versioning behavior, while focused shared run 38238171621 passed with verified owned cleanup. These SDK results do not establish combined DMS adapter or BrowserUI live acceptance. Upstream publication is verified and the default adapter is now activated; remaining DMS gates include independent activation review/merge, current post-merge verification, the combined real-provider matrix and BrowserUI acceptance. The focused activation uses the user-authorized skip-ci policy after essential local checks; shared-server runs require a fresh coordinated exclusive window.

## BrowserUI integration

The toolbar and file-context history entries require AllowViewFileVersionHistory=256, implemented history support, exactly one selected file and an idle browser. Historical downloads additionally require AllowDownloadFiles=8, restoration requires AllowRestoreFileVersions=512, and removal requires the independent AllowRemoveFileVersions=1024. Ordinary file deletion does not grant version removal. The flags and capabilities are rechecked before actions and after confirmations; the server enforces resource authorization.

The localized, resizable window retains opaque identities and unknown metadata, inherits the browser icon and remote-target context, confirms mutations, reloads history and refreshes current browser metadata after changes. Historical downloads respect the required local parent and linked-path restrictions, ask before replacing existing files, and publish a temporary local file only after successful transfer. Failures preserve existing destinations. Background synchronous calls keep the top-level form responsive; closing waits for an active call rather than claiming native cancellation. The status shows useful failure messages and causes, with complete exception details in a tooltip and the original exception retained for browser diagnostics.

## CenterDevice and Scopevisio Teamwork adapter

The shared adapter requires EnableCenterDeviceFileVersions=true and an explicitly selected CenterDeviceRestProject containing the new SDK APIs. Its immutable SDK checkpoint is 4732c21d377e0150bc259e79f1786e5d46bec6c6, merged through CenterDevice PRs #24-26. The default published package remains CompuMaster.CenterDevice.Rest 2026.10.09.0; default capability properties remain false until SDK publication and integration.

Both providers use the authentication-context user ID, which can differ from the document owner or CenterDevice display identity. History retains server order and version numbers; omitted details are an error instead of an empty list. The not-visible uploader placeholder is not a stable author ID, and document-author metadata is not guessed to be an authenticated display name. Version actions bind to the originating provider, SDK I/O context, authentication ID, document ID and version number. Foreign/reauthorized snapshots and removed versions are rejected. Restore creates a new current version. Removal retains at least one visible version. Ambiguous writes are never replayed automatically. Download disposes its SDK response stream while leaving the caller-owned destination open.

For an isolated source build, use private clones checked out at the immutable pins recorded here and pass:

```text
-p:EnableCenterDeviceFileVersions=true
-p:CenterDeviceRestProject=<pinned-centerdevice-clone>/CenterDevice.Rest/CenterDevice.Rest.csproj
-p:OcsFileVersionsProject=<pinned-ocs-clone>/Ocs/CompuMaster.Ocs.csproj
```

Direct CenterDevice initial DMS authorization remains a separate existing limitation; this adapter does not change that login contract or claim DMS consumption of the new SDK OAuth flow. The focused adapter/GUI evidence uses isolated backends. SDK Teamwork live evidence does not establish combined DMS adapter acceptance, other roles or direct OAuth acceptance. Combined DMS live acceptance, SDK publication/default activation and independent review/integration remain separate gates. Windows error 5 when attaching native focus tests to a protected foreground input thread is an environmental failure, not a passing focus check.

The model distinguishes VersionDateUtc (creation/upload time) from LastModificationUtc (historical file mtime). Existing constructor signatures remain available. CenterDevice supplies the version upload date; historical file mtime remains unknown instead of being inferred from that value. The history date column can show the version date, but download only restores a known LastModificationUtc. Removing the current CenterDevice version can change current file content, so the confirmation makes that consequence explicit and browser metadata is refreshed afterward.
