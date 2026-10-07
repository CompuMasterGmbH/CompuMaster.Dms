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

`ChildDirectoryMetadataReportsEmptyNonemptyOrUnknownAcrossListingPaths` uses the same owned-fixture helper at Level 2 to compare an empty folder and a nonempty parent across sync/async individual/child listing paths and a direct named property response. It records a capability status rather than inventing zero when the server does not support the extension. The isolated metadata test checks both listing APIs and asserts exactly four requests for four listings, protecting against per-directory startup probes. Existing browser lazy-tree/async-navigation tests cover known false/true/count values, unknown placeholders, deferred expansion, and creation/deletion/refresh.

## Configured-server observations

[Full Level 2 run 37656986484](https://github.com/CompuMasterGmbH/CompuMaster.Dms/actions/runs/37656986484), functional source `8d4569e`, passed all twelve provider/OS combinations. Each WebDAV fixture passed owner/listing parity, nullable empty/nonempty metadata checks and two sequential 32 MiB uploads. This proves the assertions rather than a universal capability for every server version.

[Reporting run 37672925992](https://github.com/CompuMasterGmbH/CompuMaster.Dms/actions/runs/37672925992), source `c278748`, retains actual named-property status and nullable-value JSON without owner identities. The first completed fixture for each configured endpoint reports:

| Configured fixture / runner | File and folder cloud owner properties | `DAV:owner` | `nc:contained-folder-count` | Mapped parent / empty folder |
| --- | --- | --- | --- | --- |
| Generic WebDAV / Windows | Both 200; owner ID and display text available | 404 | 404 | Unknown / unknown |
| ownCloud / Ubuntu | Both 200; owner ID and display text available | 404 | 404 | Unknown / unknown |
| Nextcloud / macOS | Both 200; owner ID and display text available | 404 | 200 | Count 1, true / count 0, false |

Every listed fixture passed all 30 tests, including the three targeted metadata/upload cases. The configured generic WebDAV endpoint happens to support cloud owner extensions; generic protocol support does not guarantee them. Other reporting OS jobs remain queued/running at this checkpoint; the previous complete functional run passed all three OSes. These observations identify the configured endpoints rather than every server installation.

An independently other-owned recipient fixture and the affected Nextcloud demo's manual startup review remain separate evidence gaps. Known/unknown property rendering is verified with isolated GUI fixtures. No login identity is substituted for resource ownership, and a server without usable child counts retains lazy expansion.
