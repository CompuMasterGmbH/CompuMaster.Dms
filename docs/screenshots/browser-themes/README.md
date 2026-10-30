# Browser appearance screenshots

These images are genuine component renders on Windows, generated from the shared `DocumentationScreenshotData` fixture. They contain no real files, accounts, credentials or server data. They show the standard Light/Dark modes without custom palettes.

- Browser client areas: 1100 × 720 pixels, `Documents` selected, four matching rows and `Project brief.pdf` selected. The default toolbar shows open, download, upload and refresh actions; additional actions remain in context menus. The footer has a right-aligned Close button and no theme selector or current-path label. Modification dates are sorted newest first, with the active header indicator visible.
- Detail client areas: 650 × 380 pixels, the same complete multiline file-details text in every framework.
- Locale: en-US. Fixed local modification dates: 2026-10-30, 10:00–10:45. Sizes and file order are shared.
- Native OS borders are omitted consistently. Framework-native fonts, controls, selection styling and formatting remain visible; images are not retouched.

## Regeneration

From the repository root on Windows, build both capture hosts:

```powershell
dotnet build CompuMaster.Dms.Test.BrowserUI.Parity -c CI_CD -p:DmsDocumentationScreenshots=true
dotnet build CompuMaster.Dms.Test.BrowserUI.WinUI -c CI_CD -p:DmsDocumentationScreenshots=true
$env:DMS_GUI_TEST_NOTICE = '1'
$env:DMS_GUI_TEST_ETA_SECONDS = '45'
$env:DMS_DOCUMENTATION_SCREENSHOTS = Join-Path $PWD 'artifacts/readme-screenshots'
```

Run sequentially to respect the workstation GUI lease. These exports are compiled only with `DmsDocumentationScreenshots=true` and explicitly selected; ordinary builds/CI do not discover them. They show the normal blue countdown/ETA notice and do not assert keyboard focus:

```powershell
dotnet test CompuMaster.Dms.Test.BrowserUI.Parity -c CI_CD --no-build --filter 'FullyQualifiedName~Export_documentation_screenshots'
& './CompuMaster.Dms.Test.BrowserUI.WinUI/bin/CI_CD/net8.0-windows10.0.19041.0/win-x64/CompuMaster.Dms.Test.BrowserUI.WinUI.exe' --workers=0 '--where=method == Export_documentation_screenshots'
```

Require two passed cases from each host, with no failed/skipped capture cases. Inspect all twelve generated PNGs before copying their exact filenames into this directory. Do not copy test-result XML/TRX or logs into the gallery. The WinForms/Avalonia host uses the actual browser and owned detail surfaces; the WinUI host uses the same production dialog factory and message textbox as the browser. Only window sizes, column widths, locale, fixture data and selection are controlled for comparison.

After exporting, rebuild both hosts without `-p:DmsDocumentationScreenshots=true` before running their regular suites.
