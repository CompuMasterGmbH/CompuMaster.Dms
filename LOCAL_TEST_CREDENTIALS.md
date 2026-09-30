# Local test credentials

The demo applications can persist login data in provider-specific files below the current user's temporary directory. The matching local integration-test fixture reads the same files when its environment variables are not set. Credentials are never stored in the repository.

| Demo application | Local test partition | Environment variables | Credential buffer |
| --- | --- | --- | --- |
| Scopevisio Teamwork | `ScopevisioTeamworkProviderTest` | `TEST_SCOPEVISIOTEAMWORK_USERNAME`, `TEST_SCOPEVISIOTEAMWORK_CUSTOMERNO`, `TEST_SCOPEVISIOTEAMWORK_PASSWORD` | `Scopevisio.Teamwork.Test` |
| Generic WebDAV | Future generic WebDAV partition | `TEST_WEBDAV_SERVERURL`, `TEST_WEBDAV_USERNAME`, `TEST_WEBDAV_PASSWORD` | `WebDav.Test` |
| ownCloud Classic | `WebDavProviderTest` / `OwnCloudWebDav` | `TEST_CMOWNCLOUD_SERVERURL`, `TEST_CMOWNCLOUD_USERNAME`, `TEST_CMOWNCLOUD_PASSWORD` | `OwnCloudWebDav.Test` |
| Nextcloud | `NextcloudWebDavProviderTest` / `NextcloudWebDav` | `TEST_CMNEXTCLOUD_SERVERURL`, `TEST_CMNEXTCLOUD_USERNAME`, `TEST_CMNEXTCLOUD_PASSWORD` | `NextcloudWebDav.Test` |

The namespaces are intentionally independent. Generic `TEST_WEBDAV_*` values are not used as a fallback for ownCloud or Nextcloud.

To persist credentials, start the matching demo, enter the connection details, select the option to persist login credentials, and close the login window. Passwords are masked in the UI but the existing local buffer mechanism stores values as plain text in the user's temporary directory. Clear the persistence option and close the demo to remove that demo's buffered values.

Ordinary builds and isolated tests do not require these credentials. Before running a remote integration-test partition locally, establish an exclusive window with CI and every other user of that physical server. Select only the intended provider fixture or category; do not run all remote test fixtures as general local verification.
