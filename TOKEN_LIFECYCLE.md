# Session token lifecycle

## Audited dependency baseline

This audit uses the packages referenced by the DMS provider, all version
`2026.10.07.0` (NuGet normalizes this to `2026.10.7`), rather than an older SDK
checkout:

- [OpenScope source a72dde4](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.OpenApi/blob/a72dde455591cef65425b953bc6a6d56d6b03baf/src/CompuMaster.Scopevisio.OpenApi/OpenScopeApiClient.Async.cs).
- [Teamwork source 74c0eee](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork/blob/74c0eee89c69eb593ff2908076e7564cd0c9d7f5/Scopevisio.Teamwork/Scopevisio.CenterDeviceApi/TeamworkOAuthInfoProvider.Async.cs).
- [CenterDevice source 7bb84e8](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/blob/7bb84e8d7572a226bc17d038f8c2c60948eeb4b4/CenterDevice.Rest/Rest/Clients/CenterDeviceRestClient.Async.cs).

OpenScope supports asynchronous password authorization and refresh-token grants.
It installs a replacement token only after a successful response and serializes
authorization operations per client. Teamwork shares account lookup and refresh
admission per OpenScope token owner. Concurrent failures of the same old access
token reuse the replacement instead of sending another refresh. Cancellation or
failed renewal leaves the previous token installed and releases admission for a
later attempt. The SDK does not track a local expiry deadline or proactively renew:
its supported policy is reactive renewal after an explicit expired-token response.

The CenterDevice request pipeline recognizes HTTP 401 with the exact bodies
`Unknown or expired token` or `Tenant has expired`, invokes the configured handler,
and attempts the rejected request once with the replacement. A repeated rejection
ends the request. Transport failures and uncertain write outcomes do not enter this
renewal/replay path. Conservative shared transport retry policy remains unchanged.

The legacy synchronous Teamwork handler merely rebuilt authorization from the
existing token, and its synchronous information provider cached that authorization.
Consequently, asynchronous SDK renewal alone did not repair synchronous DMS use.

## DMS integration

The temporary internal session adapter uses the existing SDK asynchronous information and
renewal operations for both synchronous and asynchronous calls. Synchronous callers
block through `GetAwaiter().GetResult()` without aggregate wrapping; asynchronous
callers retain cancellation. No second token refresh algorithm or business-operation
retry loop is added. The normal public provider API, account/application context,
customer/organization selection, certificate behavior and initial authorization
remain unchanged. All regular REST subclients receive the same session adapter.
The installed internal I/O implementation is a provider detail, not a public
contract promising a particular SDK concrete type.

Reusable synchronous SDK renewal belongs in Teamwork. [Teamwork issue #7](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork/issues/7)
tracks unifying its sync/native session behavior and then removing this DMS bridge.
Integration order is a focused Teamwork change, immutable combined SDK/DMS tests,
separately authorized publication, exact released-package consumption, and bridge
replacement. DMS-specific localized reauthorization mapping remains in DMS. This
PR neither changes nor publishes upstream libraries; its exact released baseline
is recorded above. SDK-owned refresh admission and transport behavior remain reused.

A missing refresh credential, HTTP 401 from the refresh endpoint, an OAuth
`invalid_grant` response, or authorization still rejected after renewal produces
`DmsUserAuthenticationException` with a localized instruction to sign in again.
The original renewal exception remains the inner exception where available.
Transient service/transport failures retain their original failure contract and
do not force a login. HTTP 403 on a business operation remains an authorization
failure rather than being interpreted as token revocation. No server response body,
password or token is copied into the new user-facing diagnostics. Applications
should avoid logging raw token objects, request bodies or complete SDK exception
payloads, even when retaining inner exceptions for programmatic diagnosis.

Identity belongs to the authorized session and configured account. Token refresh
does not select another organization or modify credentials. Concurrent external
mutation of SDK tokens, credentials or organization selection is unsupported;
create distinct provider instances for distinct sessions. The regular SDK refresh
response is expected to retain its user/tenant/organization context.

## Storage and future direct CenterDevice provider

Tokens stay in memory by default. The provider library and demos add no token
files or registry persistence. Existing demo credential settings are outside this
focused change. A consuming application may supply its own secure session storage
when integrating a supported SDK authorization flow. Persisted refresh tokens must
be scoped to the correct application/account/user, protected by an OS secret store,
updated atomically after rotation, and removed on logout/forget/revocation. Do not
persist access tokens merely to restore a session when renewal suffices. See
[OAuth security guidance](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14).

Direct CenterDevice initial authorization remains excluded (#53). Its SDK supplies
`OAuthRestClient.RefreshTokenAsync`, the legacy refresh method, and
`IAsyncRestClientErrorHandler`/`IAsyncOAuthInfoProvider` integration points.
A future provider must first obtain browser consent and an issued refresh token;
refresh cannot replace this initial authorization. It must supply one token owner
per session, serialize renewal of a rejected token, install rotated credentials only
after success, distinguish permanent OAuth rejection from transient transport
failure, and expose a clear reauthorization exception. Its persistent store remains
an application decision. The current DMS adapter implements this only for the
working Scopevisio authorization flow; no direct-provider operation is claimed.

## Verification limits

Isolated tests execute the released SDK with fake HTTP for reuse, token rotation,
concurrent rejected-token refresh, synchronous access, invalid/revoked credentials,
transient errors, cancellation, failure recovery and actionable body-safe messages.
Existing native authorization tests continue to cover initial state installation
and cancellation preserving an earlier authorized client. These tests do not prove
real server token expiry or revoke a shared test account. Ordinary live DMS tests
exercise the installed session client through the existing serialized CI path.
Actual long-session expiry/revocation requires a separately coordinated exclusive
server window; it must not be inferred from a successful build or fake transport.
