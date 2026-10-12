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

Default CompuMaster.Ocs remains 2026.10.09.0. The new adapter compiles only when OcsFileVersionsProject explicitly selects the new SDK source. The recorded SDK checkpoint is merged main `ab3d66db0d0f14f027e965664c4ae406fd90b26c`, implementing [OCS PR #12](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService/pull/12) / OCS #11. Its tree is identical to locally tested source checkpoint `6c40ead5a9a9198b9feb9b1015636ddc2cafdf34`; combined checks also use the exact merged checkpoint. No CenterDevice/Teamwork feature-branch dependency is imported. An OCS review/merge does not make this API available to default NuGet consumers: upstream post-merge verification and explicitly approved publication must precede selecting an exact released package and removing the temporary source opt-in.

Local Windows evidence:

- OCS: 33 isolated version cases on net8.0 and net48; all three SDK targets build without warnings/errors.
- DMS: 23 isolated contract/timestamp/adapter cases and 108 cases including existing WebDAV sharing, owner metadata and native-upload regressions on net8.0 and net48 with the exact SDK checkpoint; library netstandard2.0/net48/net6.0 builds without warnings/errors. Failed account authorization invalidates the previous version session before any network work, preventing old-account snapshots from remaining usable.
- Default package path still builds and reports no cloud version capabilities. No real DMS server was contacted.

Example opt-in: `dotnet test CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj -f net8.0 --filter "FullyQualifiedName~DmsFileVersionContractTest|FullyQualifiedName~WebDavFileVersionTest" -p:OcsFileVersionsProject=<absolute SDK project path>`. The same focused tests run on net48. Verify the local SDK revision and clean source before using this override; no moving branch is selected implicitly.

BrowserUI integration belongs to the coordinated DMS UI chat under [#100](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/100). Independent caller flags: history 256, restore 512, individual removal 1024; historical download uses existing download bit 8. History/restore/removal never imply upload or current-file deletion rights. The coordinated BrowserUI implementation and its isolated verification are described below; the cloud task keeps its original UI-reference branch unmerged.

Follow-up cloud mapping in DMS PR #105 covers legacy aliases, Infinite Scale spaces, endpoint-shaped subfolders, authenticated user identity and unsupported versioning. 124 combined isolated cloud-version/sharing/owner/native-upload cases pass on each of Windows/net8.0 and Windows/net48 against the reviewed OCS PR #14 source. SDK disposable run 38235910133 separately verifies Infinite Scale 8.2.1 and Nextcloud 34.0.3 legacy/read-only/disabled-versioning behavior; its SDK evidence does not establish combined DMS adapter or BrowserUI live acceptance. Remaining delivery gates are merged post-merge verification, exact upstream package publication, default-package adapter activation, the combined DMS real-provider matrix and BrowserUI acceptance. CI is skipped for the focused mapping commits under the user's instruction after essential local verification; SDK publication and default activation are coordinated separately.

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
