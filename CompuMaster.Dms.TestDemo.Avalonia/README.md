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

The sign-in window offers a configurable remote starting directory. Passwords are masked, kept in memory and cleared from the input after successful authorization. These Avalonia demos do not read or overwrite the WinForms demos' persisted credentials. Enter actual credentials interactively; do not supply them as command-line arguments or commit them. Connection failures show a concise message without provider exception details.

Nextcloud instance URLs are expanded to the personal WebDAV endpoint using the entered username. Explicit personal WebDAV URLs retain their browsing root. Generic WebDAV and ownCloud Classic use the entered endpoint directly. Unsupported sharing/version operations remain subject to the provider's capabilities and remote authorization.

The projects share startup, sign-in and browser configuration sources through `AvaloniaDemo.props`, while retaining separate executable names and output directories. Existing localized demo-label resources are reused, including German and RTL languages.

CI builds every application on Windows/Linux/macOS and runs isolated headless tests for window creation, provider/profile routing, login handoff, URL handling and offline startup. These checks do not prove live login or native platform acceptance. Real-server testing requires coordinated exclusive access as described in `AGENTS.md`. The release workflow is prepared to attach one archive per application after the normal reviewed and explicitly approved release process.
