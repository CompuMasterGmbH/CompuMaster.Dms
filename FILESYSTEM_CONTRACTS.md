# Local file-system exception contracts

The DMS API preserves the exceptions supplied by .NET and the provider SDK. A logical file-system conflict does not imply one universal exception type: operating system, runtime, permissions, and file system all affect native error mapping. Consumers of local-file APIs should handle `System.IO.IOException` and `UnauthorizedAccessException` as appropriate to their recovery policy. `System.IO.DirectoryNotFoundException`, `System.IO.FileNotFoundException`, and `PathTooLongException` derive from `IOException`; the similarly named `CompuMaster.Dms.Data` exceptions describe remote DMS resources.

## Audited paths

| Public/protected path | Local operations and contract |
| --- | --- |
| `BaseDmsProvider.UploadFile` / `UploadFileAsync` local-file overloads, including optional remote-parent creation | Open/read the local source; missing source, missing parent, invalid/too-long path, access denial, sharing conflicts, and read failures can escape. Stream-factory overloads also preserve exceptions from the caller's factory/stream. Byte-array overloads do not themselves require a local file. |
| `BaseDmsProvider.DownloadFile` / `DownloadFileAsync`, including selected-item overloads | Create/write the destination and optionally set its timestamp; required local parents are not automatically created by the provider. Path conflicts and access/I/O failures can escape. The item overload retains the selected file's identity where supported. |
| WebDAV raw synchronous download and `DownloadProcessedFile`; protected `WriteResponseStreamToDisk` | `File.WriteAllBytes` creates or overwrites the destination. A directory occupying that path causes an I/O or access-denied exception. Partial local data may remain after a write failure. |
| WebDAV raw/processed asynchronous downloads | A unique adjacent staging file is reserved with `FileMode.CreateNew`. Finalization moves the stage to an absent file or replaces an existing file, then applies the optional raw-download timestamp. A directory at the final destination can cause a move/replace conflict. Failed finalization cleans up the stage; cleanup and timestamp updates can themselves fail. Existing file contents are preserved when transfer fails before final replacement. |
| CenterDevice/Scopevisio synchronous and native async downloads | Delegate local writing to the released SDK, then apply the optional timestamp. Existing overwrite/partial-destination behavior is retained; the provider does not normalize SDK/file-system exceptions. |
| BrowserUI upload/download commands | The optional configured local default folder may be created before the file picker. Local directory-creation failures are caught by the GUI's command error handling. Underlying transfers retain the provider contracts above. |
| Remote folder/collection creation, remote copy/move/delete | Operate on DMS resources, not local files. Their DMS/provider errors must not be conflated with native local-file errors. The DMS library exposes no general local `CreateFile` operation. |

Empty/invalid local paths may cause `ArgumentException` or `NotSupportedException` on runtimes that reject that format. Exclusive creation, replacement, locked files, read-only locations, and disk exhaustion can expose additional `IOException` subclasses. Asynchronous methods expose the failure when their returned task is awaited. No exception normalization or change to overwrite behavior is introduced by this audit.

## Cross-platform evidence

`LocalFileSystemContractTest` exercises the actual raw/processed WebDAV APIs with successful fake HTTP responses and owned local fixtures. It checks synchronous/asynchronous directory-at-target conflicts, preservation of the directory, staging cleanup, and missing local parents. Its retained JSON records only platform/framework/operation/exception type, never server identities or paths. Isolated CI runs the same tests on Windows, Linux, and macOS; exact observed types are evidence for those runners, not a universal promise for every file system.

[Run 37672925992](https://github.com/CompuMasterGmbH/CompuMaster.Dms/actions/runs/37672925992), source `c278748e0bb31f9db5aa979c6d51fbdd5513fb8d`, retained these .NET 8 directory-at-target observations in each OS's isolated test artifact:

| Runner | Synchronous raw/processed writes | Asynchronous raw/processed finalization |
| --- | --- | --- |
| Windows | `System.UnauthorizedAccessException` | `System.IO.IOException` |
| Ubuntu | `System.UnauthorizedAccessException` | `System.IO.IOException` |
| macOS | `System.UnauthorizedAccessException` | `System.IO.IOException` |

All six contract tests passed on each runner, including the missing-parent cases. The synchronous Unix result demonstrates why a generic `FileStream(CreateNew)` example must not be substituted for evidence from the actual DMS path.

The motivating `FileStream(CreateNew)` example can map to `UnauthorizedAccessException` on Windows and `IOException` on Unix. This is not proof that every DMS download path reaches that exact primitive: asynchronous WebDAV reserves an adjacent stage and conflicts at finalization instead. The public contracts therefore document the applicable path conflict rather than copying an unrelated application's exception assertion.
