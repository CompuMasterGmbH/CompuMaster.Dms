# Testing

## Test partitions

The Build and Test workflow separates tests that do not use a remote DMS from tests that mutate shared remote systems.

- Isolated tests must not access a remote DMS. The workflow selects them by excluding the `RemoteDms` category, so they run without a resource lock.
- Every remote integration-test fixture must have the `RemoteDms` category and one category that identifies its physical test server.
- Each physical server has one entry in the `remote-integration-tests` matrix. The entry maps the server category, credentials, and concurrency group.

`TestPartitioningTest` enforces the fixture categories. Adding a remote-provider fixture requires adding its physical-server category to that guard test and to the workflow matrix in the same change.

The currently configured remote systems are:

| Physical test server | NUnit category | Concurrency group | GitHub secrets |
| --- | --- | --- | --- |
| Scopevisio Teamwork | `ScopevisioTeamwork` | `dms-test-server-scopevisio-teamwork` | `TEST_SCOPEVISIOTEAMWORK_USERNAME`, `TEST_SCOPEVISIOTEAMWORK_CUSTOMERNO`, `TEST_SCOPEVISIOTEAMWORK_PASSWORD` |
| OwnCloud through WebDAV | `OwnCloudWebDav` | `dms-test-server-owncloud-webdav` | `TEST_CMOWNCLOUD_SERVERURL`, `TEST_CMOWNCLOUD_USERNAME`, `TEST_CMOWNCLOUD_PASSWORD` |
| Nextcloud through WebDAV | `NextcloudWebDav` | `dms-test-server-nextcloud-webdav` | `TEST_CMNEXTCLOUD_SERVERURL`, `TEST_CMNEXTCLOUD_USERNAME`, `TEST_CMNEXTCLOUD_PASSWORD` |

The provider-specific sharing tests that use fake clients remain isolated tests and do not require a server lock. If another physical remote test system is added later, add a distinct category, matrix entry, secret set, and concurrency group for that server. Never reuse a lock for distinct servers, and never let two entries that mutate the same server use different locks.

## Local remote-test credentials

Ordinary builds and isolated tests do not need remote credentials. To run one of the remote WebDAV partitions locally, set the same provider-specific environment variables listed above and select exactly that server category. The Nextcloud partition reads `TEST_CMNEXTCLOUD_SERVERURL`, `TEST_CMNEXTCLOUD_USERNAME`, and `TEST_CMNEXTCLOUD_PASSWORD`; the OwnCloud partition reads the corresponding `TEST_CMOWNCLOUD_*` variables. The `TEST_WEBDAV_*` namespace is intentionally not used by either partition and remains available for a future generic WebDAV test server.

`TEST_CMNEXTCLOUD_SERVERURL` may contain either the Nextcloud instance URL or the complete user WebDAV URL. An instance URL is resolved to `/remote.php/dav/files/{username}/`; an already complete `/remote.php/dav/files/.../` or legacy `/remote.php/webdav/` URL is used unchanged. Nextcloud recommends an app password when the account uses two-factor authentication or an external authentication provider.

Run remote tests locally only after establishing an exclusive window with CI and other users of that physical server. Select one partition explicitly, for example `dotnet test CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj --framework net8.0 --filter "TestCategory=NextcloudWebDav"`. Never run all remote partitions locally as a general verification step.

## Resource-lock boundaries

Each remote-server concurrency group uses `queue: max`. GitHub Actions therefore runs one job at a time for that server and queues additional jobs instead of replacing older pending jobs. The lock covers preparation, the complete provider test run, and cleanup because it is applied to the whole job. Operating-system matrix jobs for the same server use the same lock, while jobs for different servers can run independently.

GitHub concurrency groups are scoped to one repository. They do not coordinate local test runs or workflows in other repositories. Before running remote integration tests locally, establish an exclusive window with this repository's CI and every other user of the server. If no shared cross-repository or local lock is available, run isolated tests only.
