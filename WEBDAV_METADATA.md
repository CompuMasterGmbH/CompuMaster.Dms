# WebDAV resource metadata

Resource ownership and immediate child-directory metadata are optional response capabilities. The provider requests named DAV properties plus `oc:owner-id`, `oc:owner-display-name`, `DAV:owner`, and `nc:contained-folder-count` in the listing request. It does not infer ownership from the authenticated user or from a lock's creator. Synchronous and asynchronous individual/child listings use the same mapping and the same 400/501 fallback to an ordinary PROPFIND.

| Server family | Owner information | Immediate child folders |
| --- | --- | --- |
| Generic WebDAV | Base WebDAV does not guarantee an owner. The ACL extension's `DAV:owner` can supply a principal reference; servers may also support cloud extension properties. Unsupported/forbidden/absent values remain unknown. | No universal WebDAV count is guaranteed. Use an explicitly reported supported extension if present; otherwise retain unknown metadata. |
| ownCloud Classic | Prefer successful `oc:owner-id` and `oc:owner-display-name` properties. An ID without a name remains a known owner; no extra user-directory request is required. | Verify the particular endpoint/version. Do not assume the Nextcloud count extension is available. |
| Nextcloud | Prefer the documented successful cloud owner properties for files and folders. Shared resources retain their actual resource owner, which may differ from the authenticated account. | Request `nc:contained-folder-count` explicitly. Only a successful nonnegative exact count becomes `ChildDirectoryCount`; malformed, forbidden, absent, or out-of-range values remain unknown. |

`ExtendedInfosOwner` supplies resource identity and any available display name. A display name alone can be shown without inventing a user ID. An unsupported owner renders blank in the properties view. Resource names, IDs, principal URLs, and server response contents are preserved as supplied; they are not translated or written to capability diagnostics.

`HasChildDirectories` and `ChildDirectoryCount` distinguish known empty, known nonempty, and unknown. Known-empty nodes need no expansion placeholder or child request. Unknown nodes retain a placeholder and load children only when expanded; an initial "+" on an actually empty folder can therefore be correct for an endpoint without usable metadata. No per-directory startup probe is added to force an answer. After actual children are loaded, creation/deletion/refresh update the browser's known snapshot.

## Verification

`ResourceOwnerMetadataMatchesTheServerAndAsyncListings` runs at remote Level 2 under the existing provider/account CI lock. It owns only an identified fixture beneath the usual test root, verifies clean setup and complete teardown, compares named response data with resource mapping, and checks sync/async individual/child parity. Diagnostics contain only property names/status codes and item types. The existing cloud public-link round trip also verifies that assigning a share retains owner identity.

Isolated owner tests cover ID-only/display-only/ACL owner data, property failures, fallback requests, lock-owner separation, and async metadata parity. BrowserUI tests cover both file and folder owner display, including unknown owners. Real-server property availability, shared-recipient fixtures, and UI review are recorded separately from isolated test success; missing recipient fixtures do not establish verification of another user's resource.

References:

- [WebDAV ACL resource owner](https://www.rfc-editor.org/rfc/rfc3744.html#section-5.1).
- [Nextcloud named properties and folder operations](https://docs.nextcloud.com/server/stable/developer_manual/client_apis/WebDAV/basic.html).
- [ownCloud documented resource properties](https://doc.owncloud.com/server/10.15/developer_manual/webdav_api/search.html).
