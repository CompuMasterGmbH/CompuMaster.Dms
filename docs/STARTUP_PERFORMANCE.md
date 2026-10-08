# Browser startup measurements

The browser's initial tree and selected file list share entry responses for the selected directory during that initial operation. Ancestor directories retain directory-only requests; their file lists are not prefetched. `BaseDmsProvider.ListEntriesAsync` combines the existing directory/file entry methods, preserving optimized derived-provider behavior. WebDAV retrieves both kinds from one depth-one PROPFIND and one optional OCS sharing lookup. Folder-only browsing and providers without root-file support retain directory-only root listings. Later navigation and refreshes request fresh entries; there is no persistent response cache.

To measure a demo, set `DMS_PERFORMANCE_LOG` to an absolute CSV file path in its process environment before starting it. Leaving the variable unset disables logging. The output has four columns without a header: UTC timestamp, process ID, fixed phase name and elapsed milliseconds. It contains no server URL, account name, credentials, headers, resource path, filename or response body. Output failures do not change normal provider or GUI behavior.

Phases overlap and must not be added together:

- `BrowserStartup`: the browser Load handler, from initial loading through tree/file presentation and focus restoration.
- `Authorization`: the WebDAV credential probe and optional OCS discovery together.
- `OcsDiscovery`: the discovery operation, including its capability, sharing and sharee checks. Generic WebDAV can complete this phase without any OCS requests.
- `Propfind`: each async property lookup, including parsing and the named-property compatibility retry when required.
- `OcsShares`: each configured sharing lookup. This excludes time queued before the synchronous fallback executes.
- `EntryConversion`: conversion of one directory response to DMS resources, including owner and sharing mapping.
- `FileRendering`: local sorting, file icon lookup, list presentation and column sizing.
- `HttpQueue`: each WebDAV transport attempt's rate/concurrency wait.
- `HttpTransport`: each WebDAV HTTP attempt through response headers, including the credential probe; body parsing is included in its enclosing PROPFIND phase.
- `OcsHttpTransport`: each direct OCS JSON request, including its response body.
- `OcsShareeDiscovery`: the sharee discovery call through the upstream OCS client.
- `OcsConfiguration`: the configuration discovery call through the upstream OCS client.

Count `HttpTransport` records for WebDAV attempts, `OcsHttpTransport` for direct OCS JSON requests, and `OcsShareeDiscovery`/`OcsConfiguration` for upstream discovery operations. These are separate transports; `Propfind` and `OcsShares` counts represent higher-level operations. An HTTP client's internal authentication challenge or redirect can involve additional exchanges that these application-level counters do not expose.

Compare the same selected path, account, endpoint, file-panel visibility and process lifecycle. WebDAV demo authorization currently happens inside the visible browser's startup. Scopevisio demo authorization occurs before it shows the browser, so visible-window timings alone do not compare equal work. GUI measurements on the development workstation use the required nonactivating countdown/ETA notice.

## Development-workstation observations (2026-10-08)

The actual saved Nextcloud/ownCloud demo forms and the user-populated generic WebDAV form were measured on WKS08. A separate build of the pre-change browser from `a4f40c9`, with timing instrumentation only, supplied the comparison. All used the same current provider dependencies. These are individual live observations with variable server/network latency, not a statistically controlled speed guarantee.

| Demo | Previous browser startup | Combined browser startup | Combined authorization | Combined file rendering |
| --- | ---: | ---: | ---: | ---: |
| Nextcloud | 6.32 s | 5.95 s | 3.55 s | 45 ms |
| ownCloud Classic | 2.08 s | 2.73 s | 1.61 s | 12 ms |
| Generic WebDAV | Not yet measured | 1.79 s | 1.12 s | 27 ms |

Both previous cloud runs performed two depth-one listings and two corresponding sharing lookups; every combined run performed one of each. The generic demo endpoint also exposed OCS sharing. WebDAV transport queue waits were below 2 ms in these observations. Most remaining time is in authorization/discovery and server responses; local rendering is a small fraction. The ownCloud timing demonstrates why reduced request counts alone must not be presented as a guaranteed wall-clock improvement for every individual run.
