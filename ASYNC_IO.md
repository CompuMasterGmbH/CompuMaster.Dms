# Asynchronous DMS I/O and request policy

## Current client support

| Backend | Client used here | Native asynchronous transport | Current provider surface |
| --- | --- | --- | --- |
| WebDAV, including ownCloud and Nextcloud | [`WebDav.Client` 2.9.0](https://github.com/skazantsev/WebDavClient) | Its request methods return `Task` and request parameters accept `CancellationToken`. | Async listing, upload from a local file, download to a local file, copy, move, create folder, and delete. Downloads stage into a temporary file so a failed transfer preserves an existing local target. The synchronous API remains available. Browser UI file transfers and copy/move actions await the native methods for this provider. |
| Scopevisio Teamwork | `CompuMaster.CenterDevice.Rest` 2026.1.2.100 through `CompuMaster.Scopevisio.Teamwork` | The `CenterDevice.IO` and REST client methods exposed by this version are synchronous. | Existing synchronous API and legacy `CopyAsync` worker-thread fallback remain available. `SupportsAsynchronousIo` is `False`; newly introduced async operations report `NotSupportedException`. |
| Direct CenterDevice | `CompuMaster.CenterDevice.Rest` 2026.1.2.100 | Same library limitation; its package README also says direct CenterDevice authentication is not supported. | No native async operations. |

The CenterDevice client needs async methods at its HTTP boundary before Teamwork can offer native async browsing, file and folder writes, cancellation of in-flight requests, or a transport-wide shared limiter. Wrapping synchronous methods in `Task.Run` would keep a UI thread free but would not provide these guarantees. The existing `CopyAsync` fallback is retained for compatibility and is not advertised as native asynchronous I/O.

## Published limits and applicability

The [Scopevisio API overview](https://help.scopevisio.com/en/collections/1118607-api) and [OpenScope REST reference](https://appload.scopevisio.com/static/swagger/index.html) describe the OpenScope API and Teamwork bridge, but the reviewed public material did not establish a numeric concurrency ceiling, request rate, or account/tenant/IP scope for Teamwork. OpenScope token calls and Teamwork document calls must not be assumed to have identical limits. The [CenterDevice documentation link referenced by this provider](https://public.centerdevice.de/02bf3cfd-06c6-4d43-9cd4-3c18aab0020a) was not accessible during this review, so no CenterDevice numeric limit is claimed. No authenticated server response headers were collected because doing so would require access to shared test systems.

HTTP [429](https://www.rfc-editor.org/info/rfc6585/) can include `Retry-After`; [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html#section-10.2.3) defines its seconds and HTTP-date forms. These are general HTTP rules, not evidence that either DMS backend issues a particular status or applies a particular quota.

The WebDAV transport therefore uses a conservative per-origin, process-wide allowance of **2 concurrent requests** and **30 starts per rolling minute**. These are local safety defaults, not vendor limits. Different processes still require coordination if they share a constrained account. A streaming response holds its concurrency slot until its body is consumed or disposed. Cancellation works while waiting for either allowance. Read requests (`GET`, `HEAD`, `PROPFIND`) may be retried at most three times for 408, 429, 500, 502, 503, and 504, or a transport `HttpRequestException`. Further attempts stop when 30 seconds have elapsed since the initial attempt; the first request and writes are not shortened by this retry budget. `Retry-After` takes precedence over exponential backoff with jitter. A response is returned intact when the next retry delay would exceed the budget. Writes (`PUT`, `POST`, `MKCOL`, `COPY`, `MOVE`, `DELETE`) are not replayed because an ambiguous timeout could duplicate or reorder a change. Authentication and validation errors are not retried.

## Verification and remaining work

Isolated tests use a fake HTTP handler and clock to cover a shared concurrency gate, streaming response lifetime, cancellation while queued, rate windows, `Retry-After`, exhausted retries, permanent errors, and writes without retries. WebDAV async error mapping is tested with a fake 404 response. Browser UI resource-action tests run without a server. The provider library builds for `netstandard2.0`, `net48`, and `net6.0`. Remote integration tests have not been run without an exclusive server window.

Native Teamwork async I/O, a limiter that covers its synchronous and future async requests, remaining Browser UI operations such as folder expansion and delete, and live integration verification remain open. The Teamwork work requires coordinated changes to the CenterDevice/Teamwork client; integration verification requires serialized server access.
