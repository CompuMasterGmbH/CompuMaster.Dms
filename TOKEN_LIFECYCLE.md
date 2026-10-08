# Session token lifecycle

## Audited dependency baseline

The DMS provider references Teamwork `2026.10.08.0` and OpenScope/CenterDevice
`2026.10.07.0` (NuGet normalizes trailing zero components), rather than an older
SDK checkout:

- [OpenScope source a72dde4](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.OpenApi/blob/a72dde455591cef65425b953bc6a6d56d6b03baf/src/CompuMaster.Scopevisio.OpenApi/OpenScopeApiClient.Async.cs).
- [Teamwork source c54976d](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork/blob/c54976da244a23e3c2d9ccff037798cda75a3d2e/Scopevisio.Teamwork/Scopevisio.CenterDeviceApi/TeamworkOAuthInfoProvider.Session.cs).
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

Teamwork 2026.10.07.0's legacy synchronous handler merely rebuilt authorization
from the existing token, and its synchronous information provider cached that
authorization. Teamwork 2026.10.08.0 fixes both entry points in the SDK: synchronous
and asynchronous callers now share the session engine and observe rotated tokens.

## DMS integration

The internal DMS session decorator calls the SDK's matching synchronous and
asynchronous information/renewal entry points and retains localized DMS
reauthorization errors. The temporary routing of synchronous calls through the
SDK's public asynchronous methods has been removed. The SDK owns shared refresh
admission; DMS adds no second refresh algorithm or business-operation retry loop.
The normal public provider API, account/application context,
customer/organization selection, certificate behavior and initial authorization
remain unchanged. All regular REST subclients receive the same session adapter.
The installed internal I/O implementation is a provider detail, not a public
contract promising a particular SDK concrete type.

[Teamwork issue #7](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork/issues/7)
and [Teamwork PR #8](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork/pull/8)
track the owning SDK fix and its independent publication. Integration order is
immutable combined SDK/DMS source verification, Teamwork merge/post-merge CI and
publication, then exact released-package DMS consumption and verification.
DMS-specific localized reauthorization mapping remains in DMS. OpenScope and
CenterDevice versions, SDK-owned refresh admission and transport behavior are
otherwise unchanged.

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
