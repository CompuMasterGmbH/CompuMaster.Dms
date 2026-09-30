# Repository Working Rules

## Communication and scope

- Write GitHub-facing text, including issues, comments, pull request descriptions, release notes, and commit messages, in English. Communicate with the user in their language.
- Keep changes focused on the requested issue. Avoid unrelated API changes, features, and broad documentation cleanups.

## Issue tracking

- Keep existing issue checkboxes up to date as implementation and verification progress. Check items only when the described work is actually complete.
- Before closing an issue, review its description, acceptance criteria, and all task checkboxes. Ensure their state agrees with the delivered code and test evidence; explain any remaining or deferred work instead of marking it complete.
- For new issues, capture actionable tasks and acceptance criteria using Markdown checkboxes (`- [ ]`).

## API design

- Design the public API around simple, provider-independent DMS workflows. Consuming developers should be able to use the component productively without knowing provider-specific concepts or implementation details.
- Keep provider-specific behavior inside provider implementations wherever possible. Do not clutter the common API with provider-specific flags, types, overloads, or configuration options.
- Expose differences only when consumers genuinely need to make a decision. Prefer clear common capability contracts and sensible defaults; keep unavoidable advanced provider-specific extensions separate from the normal usage path.
- Preserve source and binary compatibility where possible, including existing default behavior. Do not require consumers to adopt provider-specific knowledge to keep existing workflows working.
- Do not remove or rename public or protected API members without an explicit request. When replacing a member, retain an obsolete wrapper delegating to the new implementation with the previous default behavior where possible.
- Use terminology consistent with the existing API.
- Document provider capabilities and limitations through the appropriate contracts. Reject unsupported operations clearly, and preserve the original exception as an inner exception when wrapping failures.

## XML documentation

- Document every new public API member, public enum and enum value, and protected member intended for derived provider implementations.
- Use concise English summaries with complete sentences, descriptive third-person wording, and a final period. Put parameter semantics, return values, behavior contracts, limitations, and exceptions in the appropriate XML elements.
- Add explicit `<inheritdoc/>` to overrides when inherited documentation applies. Prefer inherited documentation with targeted additions for similar overloads instead of duplicating large blocks.
- Document provider-specific limitations where relevant without introducing unnecessary provider details into the common API.
- Keep broad cleanup of historical documentation separate from focused changes.

## Tests

- Unit and regression tests reproducing the expected behavior are important. For bug fixes, reproduce the failure in an automated test where practical and verify the corrected behavior.
- Add durable tests for new behavior, including differences in provider capabilities and supported operations.
- Distinguish isolated unit tests from integration tests that access real DMS servers, even when the latter are named unit tests in the project or workflow.
- GitHub Actions has test-server credentials configured. Local testing on WKS08 may require the user to enter credentials again. Never print credentials or commit them.
- Do not assume a successful build or isolated tests verify actual server behavior. Report which checks ran and any remaining integration-test limitations.

## Test resource lifecycle

- Before dynamically creating test files, folders, or collections, ensure the resources to be created do not already exist. Remove leftovers from interrupted earlier runs within the explicitly owned test scope, then verify absence before creating fresh resources.
- Preserve permanent test paths configured as the usual starting point in DMS profiles. Never delete these roots or unrelated data; cleanup must target only resources owned by the tests. For root-level collections, identify the exact test-owned collections rather than clearing the server root.
- If absence cannot be established or stale resources cannot be removed, fail setup with a clear diagnostic instead of reusing uncertain state or continuing into misleading test failures.
- Perform complete cleanup at the end of every test, including when assertions or partial setup fail. Use appropriate teardown/finally handling, track partially created resources, and remove children before their parent resources.
- Make cleanup repeatable and tolerant of resources already being absent. Verify removal and report cleanup failures without hiding the original test failure. Setup cleanup is a recovery measure, not a substitute for end-of-test cleanup.
- Hold exclusive test-server access throughout preparation, execution, and cleanup, as required below.

## Exclusive test-server access

- Shared DMS test servers are exclusive resources. Never run multiple test jobs that modify the same server concurrently, including across chats, worktrees, GitHub Actions runs, operating-system matrix jobs, and local execution.
- Before starting server-mutating tests, establish exclusive access for the entire run, including setup and cleanup. A one-time check that no CI job is running does not prevent a new job from starting.
- Prefer the existing serialized GitHub Actions test path. BuildAndTest.yml currently uses the job concurrency group `teamwork_test_server` and matrix `max-parallel: 1`. Preserve this protection and use the same group for other workflows accessing these servers in this repository.
- GitHub concurrency does not lock out local runs or jobs in other repositories. Local integration tests require a coordinated exclusive window with CI and other sessions, or a shared lock honored by every runner. If neither is established, run isolated tests only and defer server-mutating tests.
- Do not run the entire provider test suite casually to verify a local-only change. Select isolated tests explicitly when shared server access is unnecessary.

## Releases

- Create releases only from the primary integration branch after the changes have been included in a pull request, reviewed as required, and merged. Wait for the successful post-merge build-and-test workflow before releasing.
- If release prerequisites are incomplete, prepare or update the pull request and report the remaining prerequisites. Creating a release in this repository triggers publication.
- Derive release notes from the complete diff since the previous release. Identify the affected package using its exact NuGet package ID where available, or name the affected project or repository infrastructure.
- Prefix API-breaking release-note entries with `BREAKING CHANGE: `. Group multiple such entries in a dedicated Breaking Changes section.

## Branch cleanup

- After merge, delete feature branches locally and remotely only after all associated required pipelines have succeeded, including post-merge and any requested release or deployment workflows.
- Retain branches while workflows are queued, running, awaiting approval, failed, or uncertain. Verify the branch tip is integrated and that no uncommitted work or unmerged commits will be lost before cleanup.
- Coordinate cleanup with other chats and worktrees. Do not switch another active session's checkout or remove worktrees or their files without authorization.

## File encoding and line endings

- Save text files as UTF-8 with BOM and CRLF line endings, as configured in `.editorconfig`.
- Keep any `.gitattributes` rules intact and treat binary formats as binary. Do not apply text normalization to images, archives, or other binary assets.
- Keep broad encoding or line-ending normalization in a separate mechanical change whenever possible; avoid unrelated file churn in feature fixes.

## Agreed issue direction

- #8: Prefer optional provider metadata indicating whether/how many child folders or collections exist. Use it to show expansion controls without fetching children; fetch actual children when expanded. Preserve the distinction between unknown metadata and a known zero count, with a lazy-loading fallback for unknown values. Cover these cases with tests.
- #9: Async API expansion is deferred pending a later explicit go-ahead.
- #5: Preserve compatibility for existing clients and explicitly selected instances. Investigate current default-instance behavior and an additive way to expose available instances. The public API/UI design is not yet settled.

## Remote DMS integration tests

- Scopevisio Teamwork and WebDAV integration tests use shared remote DMS test systems and shared `ZZZ_UnitTests_*` directory structures.
- Never run remote DMS integration tests locally while a GitHub Actions test workflow for this repository is queued or running. Local and CI tests can otherwise modify or remove the same remote directories and cause nondeterministic failures.
- Before starting remote DMS integration tests locally, check all relevant GitHub Actions runs, not only the current branch or pull request.
- Keep GitHub Actions jobs that access the shared remote DMS systems serialized through the repository's existing concurrency configuration. Do not enable parallel execution for these tests.
- Tests that create remote files or directories must remove remnants from interrupted previous runs before execution and clean up their own resources in a `Finally` block whenever possible.
- Prefer uniquely named resources below the established `ZZZ_UnitTests_*` test directories. Use provider-specific tests when a DMS capability differs; for example, WebDAV does not support two distinct files with the same name in one directory.

## Releases and publishing

- Creating a GitHub release is a public deployment action: `.github/workflows/PublishRelease.yml` publishes `CompuMaster.Dms.Providers` and `CompuMaster.Dms.BrowserUI` to NuGet.org and attaches compiled Provider, BrowserUI, Scopevisio demo, and WebDAV demo archives to the GitHub release.
- Create a release only after the related pull request has been merged into `main` and the complete serialized GitHub Actions test matrix, including all Scopevisio Teamwork and WebDAV integration tests on every configured operating system, has completed successfully.
- Require explicit user approval immediately before creating the GitHub release. Do not infer release authorization from approval to commit, push, or merge code.
- Use a new, unused release tag that is also a valid NuGet version without a leading `v`, for example `2026.08.12.0`. Verify existing GitHub releases, Git tags, and NuGet package versions before selecting it.
- Trigger publishing by creating the GitHub release so that the `release: created` workflow event supplies the version through `github.ref_name`.
- Do not use the workflow's `workflow_dispatch` trigger for a production release from a branch: the branch name would be used as `VersionPrefix` and may not be a valid or intended NuGet package version.
- After creating a release, monitor the complete publish workflow and verify both NuGet packages and all expected GitHub release assets before reporting the deployment as successful.
