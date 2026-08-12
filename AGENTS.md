# Repository Guidance for Agents

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
