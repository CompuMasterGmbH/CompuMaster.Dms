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

The Nextcloud, ownCloud, and ownCloud Infinite Scale sharing tests that use fake clients are isolated tests and do not require a server lock. If a physical Nextcloud or another remote test system is added later, add a distinct category, matrix entry, secret set, and concurrency group for that server. Never reuse a lock for distinct servers, and never let two entries that mutate the same server use different locks.

## Resource-lock boundaries

Each remote-server concurrency group uses `queue: max`. GitHub Actions therefore runs one job at a time for that server and queues additional jobs instead of replacing older pending jobs. The lock covers preparation, the complete provider test run, and cleanup because it is applied to the whole job. Operating-system matrix jobs for the same server use the same lock, while jobs for different servers can run independently.

GitHub concurrency groups are scoped to one repository. They do not coordinate local test runs or workflows in other repositories. Before running remote integration tests locally, establish an exclusive window with this repository's CI and every other user of the server. If no shared cross-repository or local lock is available, run isolated tests only.
