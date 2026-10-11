# File-version contracts and cloud adapters

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

DmsFileVersion copies FileID, FileFullName and FileName from the selected file and retains opaque VersionID independently from labels or dates. LastModificationUtc, ContentLength, AuthorID, AuthorDisplayName, Label, ContentType and ProviderSpecificHashOrETag are optional. Unknown length/time is different from zero/epoch. The model has no current-version flag: do not infer current status from ordering or timestamps. Actions use snapshots from the same provider/account; the WebDAV adapter keeps an internal SDK snapshot bound to its client and original file/version identity. Reauthorization or a different provider rejects stale snapshots; restore/removal verify that the current path still identifies the same file. Replaced files fail even when history is empty.

Historical download preserves the caller's stream ownership even on failure, and may leave partial data. Browser callers should write to an owned temporary file, commit the destination only after success and remove partial files after failure. These new common synchronous methods do not promise active cancellation. SDK-native async/cancellation remains available to standalone CompuMaster.Ocs consumers; BrowserUI can use its existing worker-operation pattern and wait until an active call completes before closing.

## Cloud protocol support

| Provider | Listing/download | Restore | Individual removal |
| --- | --- | --- | --- |
| Nextcloud | DAV `versions/USER/versions/FILEID`, selected GET | MOVE to the special restore collection | Only when authenticated `files.version_deletion` is explicitly true |
| ownCloud Classic | DAV `meta/FILEID/v`, selected GET | Conditional COPY to the existing current file | Unsupported by this adapter |
| ownCloud Infinite Scale | Official Web client uses the meta version namespace with opaque IDs | Conditional COPY to the resolved current DAV file | Unsupported/unknown; no Classic/Nextcloud inference |
| Generic WebDAV / NoDms / manual URL | Unsupported | Unsupported | Unsupported |

The adapter requires an observed server family and authenticated `files.versioning=true`. It obtains protocol-user identity from `/dav/files/USER/` rather than substituting a display/login name. Missing or unrecognized capability evidence retains unsupported defaults. Legacy `/remote.php/webdav` paths and Infinite Scale space-only URL forms are not currently opted in because this adapter cannot safely resolve the protocol identity/address there. The standalone SDK accepts explicit DAV root/protocol/current-file addresses; actual Infinite Scale compatibility is not live-verified. See [the SDK protocol matrix](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService/blob/main/FILE_VERSIONS.md) and [OCS #11](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService/issues/11).

## Exact local dependency and validation

Default CompuMaster.Ocs remains 2026.10.09.0. The new adapter compiles only when OcsFileVersionsProject explicitly selects the new SDK source. The recorded local SDK checkpoint is `6c40ead5a9a9198b9feb9b1015636ddc2cafdf34` on codex/cloud-file-versions-01a12cd3, implementing OCS #11. No CenterDevice/Teamwork feature-branch dependency is imported. An OCS review/merge does not make this API available to default NuGet consumers: upstream post-merge verification and explicitly approved publication must precede selecting an exact released package and removing the temporary source opt-in.

Local Windows evidence:

- OCS: 33 isolated version cases on net8.0 and net48; all three SDK targets build without warnings/errors.
- DMS: 20 isolated contract/adapter cases on net8.0 and net48 with the exact SDK checkpoint; library netstandard2.0/net48/net6.0 builds without warnings/errors.
- Default package path still builds and reports no cloud version capabilities. No real DMS server was contacted.

Example opt-in: `dotnet test CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj -f net8.0 --filter "FullyQualifiedName~DmsFileVersionContractTest|FullyQualifiedName~WebDavFileVersionTest" -p:OcsFileVersionsProject=<absolute SDK project path>`. The same focused tests run on net48. Verify the local SDK revision and clean source before using this override; no moving branch is selected implicitly.

BrowserUI integration belongs to the coordinated DMS UI chat under [#100](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/100). Independent caller flags: history 256, restore 512, proposed individual removal 1024; historical download uses existing download bit 8. History/restore/removal never imply upload or current-file deletion rights. A separate local UI-reference commit contains an initial localized dialog and tests, for review/reuse by that owning chat rather than competing UI merges.

Remaining acceptance: authorized exclusive ownCloud/Nextcloud live tests (including app-disabled, read-only and server retention semantics), Infinite Scale account/space mapping and live verification, upstream package publication, default-package adapter activation, and final BrowserUI integration. Local build/mock evidence does not claim any of these complete. CI is intentionally skipped for this preflight under the user's explicit instruction after essential local verification; no server mutation is inferred or performed.
