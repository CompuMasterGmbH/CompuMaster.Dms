# README screenshots

The login and browser images were captured from the current Windows Forms UI on
2026-10-08 in English, including the shared login artwork and current browser
layout. They use illustrative data, not a live DMS account.

The login uses `https://webdav.server.mine/demo/`, username `demo`, and a masked
example password. Stored profiles were not read or written. The browser uses
in-memory sample folders and files; its sharing actions illustrate a provider
that supports sharing. No server requests or remote changes were made.

## WinUI sharing examples

`sharing/winui-sharing-teamwork.png` and `sharing/winui-sharing-nextcloud.png` are genuine renders of the production WinUI sharing dialog, captured in Light mode with an en-US locale and a 1100 × 720 pixel client area. Native OS borders are omitted. The images use process-local synthetic collections/folders, users, groups and links; no credentials, profiles or real servers are accessed, and no sharing changes are saved.

The Teamwork collection mirrors the Scopevisio/CenterDevice sharing contract: internal grants expose the provider's complete permission set, with a limited download link and a separate upload link. The Nextcloud folder shows an editable user grant, a read-only group grant, an expiring review link and an upload-only link. The captures do not establish real-server integration verification.

Regenerate from the repository root on Windows:

```powershell
dotnet build CompuMaster.Dms.Test.BrowserUI.WinUI -c CI_CD -p:DmsDocumentationScreenshots=true
$env:DMS_GUI_TEST_NOTICE = '1'
$env:DMS_GUI_TEST_ETA_SECONDS = '30'
$env:DMS_DOCUMENTATION_SCREENSHOTS = Join-Path $PWD 'artifacts/readme-sharing-screenshots'
& './CompuMaster.Dms.Test.BrowserUI.WinUI/bin/CI_CD/net8.0-windows10.0.19041.0/win-x64/CompuMaster.Dms.Test.BrowserUI.WinUI.exe' --workers=0 '--where=method == Export_sharing_setup_screenshots'
```

The exporter is compiled only with `DmsDocumentationScreenshots=true` and is explicitly selected. It uses the workstation countdown/ETA notice and exclusive GUI lease. Require two passed cases, zero failures and zero skipped cases. Inspect both generated PNGs before copying their exact filenames into `docs/screenshots/sharing/`; keep results and logs in the artifact directory. Rebuild the test host without `-p:DmsDocumentationScreenshots=true` afterwards.
