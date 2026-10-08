# Testing

## Test levels

The central [testing.ci-pipeline-attributes](testing.ci-pipeline-attributes) file contains `TEST_LEVEL=1` and explains each level and NUnit assignment examples directly alongside the setting. Change that value to `2` for cumulative extended CI; no file rename is required. Manual workflow inputs default to `repository` and can explicitly override it with `1` or `2`. Invalid, missing, or duplicate settings fail before remote matrix jobs start.

With the default file setting, pull requests and pushes to `main` use **Level 1** on Windows, Linux, and macOS. Quick isolated provider/native source-chain regressions and Windows Browser UI tests remain enabled. Each configured remote provider runs authentication, listing, existence, and path checks, plus one small file lifecycle: upload, version replacement, copy, move, byte-for-byte download, and verified cleanup.

The Level 1 remote suite targets **about five minutes per provider/platform; up to eight minutes is acceptable**. The runner measures test execution including setup and cleanup separately from compilation and queue waits, and writes the duration to the job summary. It warns above eight minutes instead of killing an active request and losing cleanup. Job wall time additionally includes SDK installation, compilation, and reporting. Request admission/rate limits are unchanged.

**Cumulative Level 2** includes all Level 1 checks, extended isolated tests, and all existing extended default-package cases (nested copy/move, overwrite/failure variants, sharing/links, duplicate identities, provider-specific behavior). Manually run **Build and Test** at Level 2 for the extended provider matrix. Native Scopevisio live verification is a separate explicit opt-in using `run_native_scopevisio=true` with a coordinated exclusive window; it covers all three platforms. Native live tests prove real CRUD/identity/link operations, the unchanged 256 MiB hash round trip, and active download cancellation. They do not run in Level 1. A Level 2 file setting on a PR/push does not grant native server access; that live scope remains manual.

Native live opt-in requires `exclusive_window_until` with an explicit UTC offset and coordinated exclusion of other repositories/local sessions. Level 2 without native opt-in runs the extended remote matrix under its existing account locks and coordinated server access. Each native job retains its 185-minute reserve check after acquiring the account lock. The existing `run_native_scopevisio=true` dispatch remains available for native-only Level 2 verification, skipping the default-package remote matrix. `native_scopevisio_mode=upload_boundary` remains a Windows-only diagnostic, not full large-transfer acceptance. Large transfers remain configurable from 256 to 4096 MiB. Native dispatches default to `native_dependency_mode=packages`, which requires Teamwork 2026.10.8 and SDK/OpenScope/OCS 2026.10.7 assets; select `source` to verify the exact immutable release-source commits pinned in the workflows. Package and source evidence are labeled separately and are not interchangeable. Explicit sync/async Scopevisio session renewal runs in Level 1 to protect subsequent dependency updates; it preserves identity and does not force natural expiry or revoke shared credentials.

Run Level 2 when changing transport, transfers, sharing, or copy/move behavior, and before release approval. Green Level 1 is quick regression evidence; it does not replace Level 2 acceptance or the existing review/merge/publication prerequisites.

Every remote test has exactly one category, `TestLevel1` or `TestLevel2`. Isolated tests without a level category are included in Level 1; mark slower isolated methods/fixtures `TestLevel2` to run them only at Level 2 in both the default and native source workflows. For mixed fixtures, assign levels to individual methods rather than assigning one level to the fixture and another to a method. The isolated `TestPartitioningTest` validates NUnit's actual discovery tree, including inherited methods and fixture categories. Inspect either selection without accessing a server:

```powershell
./.github/scripts/test-remote-dms.ps1 -ServerCategory Nextcloud -TestLevel 1 -ListTests
./.github/scripts/test-remote-dms.ps1 -ServerCategory Nextcloud -TestLevel 2 -ListTests
```

The small lifecycle owns only `ZZZ_UnitTests_CM.Dms_Level1` and its three known files. Setup removes identified leftovers and verifies absence; cleanup removes children before the root, verifies absence, and preserves both execution and cleanup failures. Permanent roots and unrelated data are preserved.

At checkpoint `4b586e69fd80b1230bc8823c95195eb5ca940f1a`, Windows WebDAV-family suites took roughly 30 minutes each. Directory move variants alone took about nine minutes, directory copy about five to six, and file copy/move variants another eight. Separately requested native live cases took about four minutes per platform and were already excluded from ordinary PR runs. Those extended cases remain intact in Level 2.

## Test partitions

The Build and Test workflow separates tests that do not use a remote DMS from tests that mutate shared remote systems.

- Isolated tests must not access a remote DMS. The workflow selects them by excluding the `RemoteDms` category, so they run without a resource lock.
- Every remote integration-test fixture must have the `RemoteDms` category and one category that identifies its logical test partition.
- Each configured physical server has one entry in the generated `remote-integration-tests` matrix. The entry maps the category, credentials, and concurrency group.

`TestPartitioningTest` enforces the fixture categories. Adding a remote-provider fixture requires adding its logical partition category to that guard test and to the workflow matrix in the same change.

The currently configured remote systems are:

| Test partition | Physical test server | NUnit category | Concurrency group | GitHub secrets |
| --- | --- | --- | --- | --- |
| Scopevisio Teamwork | Scopevisio Teamwork | `ScopevisioTeamwork` | `dms-test-server-scopevisio-teamwork` | `TEST_SCOPEVISIOTEAMWORK_USERNAME`, `TEST_SCOPEVISIOTEAMWORK_CUSTOMERNO`, `TEST_SCOPEVISIOTEAMWORK_PASSWORD` |
| Generic WebDAV | WebDAV | `WebDav` | `dms-test-server-webdav` | `TEST_WEBDAV_SERVERURL`, `TEST_WEBDAV_USERNAME`, `TEST_WEBDAV_PASSWORD` |
| OwnCloud through WebDAV | OwnCloud | `OwnCloud` | `dms-test-server-owncloud` | `TEST_CMOWNCLOUD_SERVERURL`, `TEST_CMOWNCLOUD_USERNAME`, `TEST_CMOWNCLOUD_PASSWORD` |
| Nextcloud through WebDAV | Nextcloud | `Nextcloud` | `dms-test-server-nextcloud-webdav` | `TEST_CMNEXTCLOUD_SERVERURL`, `TEST_CMNEXTCLOUD_USERNAME`, `TEST_CMNEXTCLOUD_PASSWORD` |

The provider-specific sharing tests that use fake clients remain isolated tests and do not require a server lock. If another physical remote test system is added later, add a distinct category, matrix entry, secret set, and concurrency group for that server. Never reuse a lock for distinct servers, and never let two entries that mutate the same server use different locks.

The WebDAV matrix entries are optional. They are generated only when all three `TEST_WEBDAV_*` repository secrets are configured. If any value is missing, the workflow emits a notice, omits all three WebDAV operating-system jobs, and continues successfully with the remaining test servers. WebDAV credentials are never populated from the OwnCloud secret namespace.

## Local remote-test credentials

Ordinary builds and isolated tests do not need remote credentials. To run one of the remote WebDAV partitions locally, set the same provider-specific environment variables listed above and select exactly that partition category. The generic WebDAV partition reads `TEST_WEBDAV_*`; the OwnCloud partition reads `TEST_CMOWNCLOUD_*`; and the Nextcloud partition reads `TEST_CMNEXTCLOUD_*`. These namespaces do not fall back to one another. WebDAV and OwnCloud are independent resources with separate locks and credential sets.

`TEST_CMNEXTCLOUD_SERVERURL` may contain either the Nextcloud instance URL or the complete user WebDAV URL. An instance URL is resolved to `/remote.php/dav/files/{username}/`; an already complete `/remote.php/dav/files/.../` or legacy `/remote.php/webdav/` URL is used unchanged. Nextcloud recommends an app password when the account uses two-factor authentication or an external authentication provider.

Run remote tests locally only after establishing an exclusive window with CI and other users of that physical server, and confirming that no relevant CI run is queued/running. Select one partition and level explicitly, for example `./.github/scripts/test-remote-dms.ps1 -ServerCategory Nextcloud -TestLevel 1`. Restore dependencies first. A provider-only filter such as `TestCategory=Nextcloud` still selects the whole default-package suite, not just Level 1. Never run all remote partitions locally as a general verification step.

## Resource-lock boundaries

Each remote-server concurrency group uses `queue: max`. GitHub Actions therefore runs one job at a time for that server and queues additional jobs instead of replacing older pending jobs. The lock covers preparation, the complete provider test run, and cleanup because it is applied to the whole job. Operating-system matrix jobs for the same server use the same lock, while jobs for different servers can run independently.

GitHub concurrency groups are scoped to one repository. They do not coordinate local test runs or workflows in other repositories. Before running remote integration tests locally, establish an exclusive window with this repository's CI and every other user of the server. If no shared cross-repository or local lock is available, run isolated tests only.
