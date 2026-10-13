# Testing

## Test levels

The normal Build and Test pipeline runs **complete cumulative Level 2 on Windows/.NET 8**, including all isolated provider regressions, the full normally enabled BrowserUI suite, and every configured remote-provider partition. Linux and macOS run **named representative samples**, and Windows/.NET 4.8 runs the isolated sample. All declared targets remain compiled. This coverage policy implements the user's explicit reduction of duplicate execution and replaces the older requirement to repeat every server test on every OS.

The [testing.ci-pipeline-attributes](testing.ci-pipeline-attributes) setting still controls local scripts. CI validates manual/repository values but always promotes normal Windows/net8 to Level 2; selecting `1` does not narrow its full suite. Read-only diagnosis remains a separate guarded Level 1 mode. Normal sample methods are chosen explicitly, not by treating the whole Level 1 suite as a small sample.

Each non-Windows remote sample contains login, listing and `BasicFileRoundTripAndCleanup`. Scopevisio adds both sync/async explicit session renewal cases; ownCloud and Nextcloud add the mandatory OCS public-link round trip. Generic WebDAV selects three cases, cloud partitions four, and Scopevisio five actual cases because renewal is parameterized. All fixture setup, failure behavior, owned-resource cleanup and server/account locks remain intact. No job timeout was shortened to interrupt cleanup.

Isolated samples retain provider-neutral existence/session contracts, Classic cloud-family metadata regression cases and the discovered server-partition audit. Native source validation retains full Windows/net8 fake-transport suites and named authorization/cancellation/mutation-safety samples elsewhere, including Windows/net48. The native live opt-in runs its complete specialized verification on Windows/net8 only; it still requires the explicit exclusive window, unchanged transfer size and cancellation assertions. Other OS samples do not establish full native transport acceptance.

Tests retain their Level 1/Level 2 classifications for local focused use. The runner measures execution and cleanup separately from compilation/queue waits. Full Windows runs include all Level 1 and extended copy/move/overwrite/failure/sharing/identity cases. Representative samples are not full cross-platform or complete API coverage. JUnit/TRX results and job names label the coverage; empty, wholly skipped or failed selections fail validation. Record actual executed/ignored counts and exact merged head for release evidence.

Native live opt-in requires `exclusive_window_until` with an explicit UTC offset and coordinated exclusion of other repositories/local sessions. Level 2 without native opt-in runs the extended remote matrix under its existing account locks and coordinated server access. Each native job retains its 185-minute reserve check after acquiring the account lock. The existing `run_native_scopevisio=true` dispatch remains available for native-only Level 2 verification, skipping the default-package remote matrix. `native_scopevisio_mode=upload_boundary` remains a Windows-only diagnostic, not full large-transfer acceptance. Large transfers remain configurable from 256 to 4096 MiB. Native dispatches default to `native_dependency_mode=packages`, which requires Teamwork 2026.10.8 and SDK/OpenScope/OCS 2026.10.7 assets; select `source` to verify the exact immutable release-source commits pinned in the workflows. Package and source evidence are labeled separately and are not interchangeable. Explicit sync/async Scopevisio session renewal runs in Level 1 to protect subsequent dependency updates; it preserves identity and does not force natural expiry or revoke shared credentials.

Release acceptance requires the successful final merged-head Windows/net8-full and other-platform-sample pipeline in a coordinated server window. Existing review, merge, approval and publication prerequisites remain. A read-only diagnosis or successful isolated build does not replace this gate.

Every remote test has exactly one category, `TestLevel1` or `TestLevel2`. For local focused selection, isolated tests without a level category belong to Level 1 and slower tests can carry `TestLevel2`. CI full profiles include both levels; CI sample profiles select named fixtures or methods independently of these local levels. For mixed fixtures, assign levels to individual methods rather than assigning one level to the fixture and another to a method. The isolated `TestPartitioningTest` validates NUnit's actual discovery tree, including inherited methods and fixture categories. Inspect either selection without accessing a server:

```powershell
./.github/scripts/test-remote-dms.ps1 -ServerCategory Nextcloud -TestLevel 1 -ListTests
./.github/scripts/test-remote-dms.ps1 -ServerCategory Nextcloud -TestLevel 2 -ListTests
./.github/scripts/test-remote-dms.ps1 -ServerCategory Nextcloud -TestLevel 2 -Coverage Sample -ListTests
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
