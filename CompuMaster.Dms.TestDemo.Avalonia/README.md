# Avalonia desktop demo applications

Each application starts Avalonia's native desktop lifetime, installs the Fluent theme and opens its own provider-specific Avalonia sign-in window. The existing WinForms demos remain available separately.

| Application | Startup |
| --- | --- |
| `CompuMaster.Dms.TestDemo.WebDav.Avalonia` | Generic WebDAV URL and credentials |
| `CompuMaster.Dms.TestDemo.Nextcloud.Avalonia` | Nextcloud instance URL or complete personal WebDAV URL |
| `CompuMaster.Dms.TestDemo.OwnCloudClassic.Avalonia` | ownCloud Classic personal WebDAV URL and credentials |
| `CompuMaster.Dms.TestDemo.ScopevisioTeamwork.Avalonia` | Scopevisio customer number and credentials, followed by instance selection where available |
| `CompuMaster.Dms.TestDemo.Avalonia` | Process-local offline browser by default |

Run a dedicated application without selecting a mode or passing provider arguments:

```powershell
dotnet run --project CompuMaster.Dms.TestDemo.WebDav.Avalonia -c CI_CD
dotnet run --project CompuMaster.Dms.TestDemo.Nextcloud.Avalonia -c CI_CD
dotnet run --project CompuMaster.Dms.TestDemo.OwnCloudClassic.Avalonia -c CI_CD
dotnet run --project CompuMaster.Dms.TestDemo.ScopevisioTeamwork.Avalonia -c CI_CD
```

Build all five applications with:

```powershell
pwsh .github/scripts/build-avalonia-demos.ps1 -Configuration CI_CD
```

On Windows, each dedicated executable is in its own `bin/CI_CD/net8.0/` directory, named after the project, for example `CompuMaster.Dms.TestDemo.Nextcloud.Avalonia.exe`. On Linux/macOS use the generated executable or `dotnet <application>.dll` from the same directory with .NET 8 installed. Avalonia's desktop native dependencies still apply; these applications do not use WinForms.

All dedicated applications also accept `--offline` to open a browser backed solely by the process-local demonstration provider:

```powershell
dotnet run --project CompuMaster.Dms.TestDemo.Nextcloud.Avalonia -c CI_CD -- --offline
```

Offline titles are explicitly marked. This mode needs no credentials and performs no server requests. The general demo keeps its existing `--webdav` and `--scopevisio` modes and also accepts `--nextcloud` and `--owncloud`.

The sign-in windows preserve the WinForms demo's fields, prefilled values and persistence behavior. The localized save-credentials checkbox explicitly describes the existing plain-text storage in the temporary directory. When selected, closing the form saves every input; when cleared, closing removes that demo's cached inputs. Successful sign-in retains the masked password for retries and persistence, as in WinForms. Each application shares its corresponding WinForms cache (same application namespace and historical ASCII/MD5 field keys); a nonblank `TEST_WEBDAV_*`, `TEST_CMNEXTCLOUD_*`, `TEST_CMOWNCLOUD_*` or `TEST_SCOPEVISIOTEAMWORK_*` environment value takes precedence. Environment values alone do not opt into saving to disk. Password values are never logged. Do not supply credentials as command-line arguments or commit them. Connection failures show a concise message without provider exception details.

Scopevisio also preserves the optional numeric organisation ID and expected Teamwork tenant ID. Explicit organisation selection bypasses the picker and cannot fall back to another organisation. A tenant guard alone is checked after interactive instance selection. Cancelling the picker opens no browser. A tenant mismatch rejects the selected context before browsing; explicit organisation or tenant restrictions disable instance switching. Invalid selectors prevent closing with persistence enabled and leave cached inputs unchanged.

Nextcloud instance URLs are expanded to the personal WebDAV endpoint using the entered username. Explicit personal WebDAV URLs retain their browsing root. Generic WebDAV and ownCloud Classic use the entered endpoint directly. Unsupported sharing/version operations remain subject to the provider's capabilities and remote authorization.

The projects share startup, sign-in and browser configuration sources through `AvaloniaDemo.props`, while retaining separate executable names and output directories. Existing localized demo-label resources are reused, including German and RTL languages.

CI builds every application on Windows/Linux/macOS and runs isolated headless tests for window creation, provider/profile routing, login handoff, cache compatibility, persistence, Scopevisio selectors, URL handling and offline startup. The isolated command uses `--filter "TestCategory!=RemoteDms"`; these checks do not prove live login or native platform acceptance.

Separate Windows CI jobs use each provider's existing server/account concurrency group and repository credentials to run the explicit `DemoLiveLoginTests` through the actual Avalonia login and browser initialization code. They authorize, select the configured context and read the starting directory without creating, changing or deleting remote resources. They do not copy or rewrite the credential cache. Missing CI configuration is reported as NOT EXECUTED, rather than a passed login. This is headless live-server coverage, not native interaction acceptance. Local live tests still require the coordinated access described in `AGENTS.md`; ordinary builds and isolated tests do not log in. The release workflow is prepared to attach one archive per application after the normal reviewed and explicitly approved release process.
