# Native WinUI DMS demos

The dedicated WinUI 3 applications mirror the corresponding WinForms and Avalonia login/browser workflows:

| Project | Provider |
|---|---|
| `CompuMaster.Dms.TestDemo.WinUI` | General offline demo; optional provider flags |
| `CompuMaster.Dms.TestDemo.WebDav.WinUI` | WebDAV |
| `CompuMaster.Dms.TestDemo.Nextcloud.WinUI` | Nextcloud |
| `CompuMaster.Dms.TestDemo.OwnCloudClassic.WinUI` | ownCloud Classic |
| `CompuMaster.Dms.TestDemo.ScopevisioTeamwork.WinUI` | Scopevisio Teamwork |

Build all projects on Windows:

```powershell
./.github/scripts/build-winui-demos.ps1 -Configuration CI_CD
dotnet run --project CompuMaster.Dms.TestDemo.Nextcloud.WinUI -c CI_CD
dotnet run --project CompuMaster.Dms.TestDemo.Nextcloud.WinUI -c CI_CD -- --offline
```

The demos target .NET 8, x64 Windows 10 version 1809 or later. The Windows App SDK runtime is bundled using unpackaged self-contained SDK deployment; the .NET 8 desktop/runtime prerequisites remain applicable. No MSIX install or Store publisher registration is needed. Other CPU architectures require an appropriate host build and separate native verification.

The general demo starts offline and accepts `--webdav`, `--nextcloud`, `--owncloud` or `--scopevisio`. Every dedicated demo accepts `--offline` for process-local fixture data. Offline window titles and instance names are explicitly labeled.

Live login forms preserve provider-specific fields, initial paths, Nextcloud URL handling, Scopevisio organization/tenant selection, environment-variable precedence and the WinForms cache keys. WinForms, Avalonia and WinUI compile the applicable common login/profile/cache rules from the same sources. Tests use private buffers; do not print, copy, clear or reset real cached credentials during verification.

The native runtime uses compiled application XAML and generated resource metadata. The shared browser library does not host WinForms or Avalonia. Its workflow, package setup, licenses and publication details are described in [the WinUI component](../CompuMaster.Dms.BrowserUI.WinUI/README.md). Scope and exact verification remain tracked in [#151](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/151), linked to framework umbrella [#115](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/115).
