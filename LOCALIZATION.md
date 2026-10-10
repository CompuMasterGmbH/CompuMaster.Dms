# Localization contract

The provider library and BrowserUI use embedded `.resx` resources and ordinary .NET satellite assemblies. Neutral resources are English; `de`, `fr`, `es`, `zh-Hans`, `zh-Hant`, `ja`, `ar`, `he`, and `hi` satellites cover the same keys. Language packs add translations without a provider-specific culture option or a dependency on Windows Forms in the provider library.

The consuming application selects `CultureInfo.CurrentUICulture` for resource lookup and `CultureInfo.CurrentCulture` for user-visible date/number formatting before constructing forms or creating display snapshots. The component reads those cultures and does not replace the application's thread/process settings. Cultures flow across awaited operations through the normal .NET execution context. A regional culture uses its parent resource set and ultimately English; supported Chinese scripts are treated separately. Existing forms are not promised to retranslate while open.

Component-generated prompts, validation/authentication explanations, and generated labels such as the Scopevisio all-users group are localized. Server-provided names, identities, paths, protocol/serialization values, SDK response payloads, provider/product names, and XML/API documentation retain their meaning. Exceptions retain their classes, parameter/path properties, and original inner exception; their display messages follow UI culture. Technical allowed-action tokens remain stable and are translated when rendered by the UI.

## Translation conventions

- Use stable descriptive resource keys and complete indexed message templates. Translators can reorder `{0}`, `{1}`, and subsequent arguments without changing code or argument meanings.
- Preserve format specifiers, escaped braces, and intentional whitespace in property-label resources. Never assemble grammar by translating separate sentence fragments.
- Match the neutral key set and placeholder multiset for every supported satellite. Unsupported cultures fall back to English; a missing translation must not masquerade as a completed language pack.
- Keep menu/button accelerator keys usable within their dialog. Verify longer text, Unicode scripts, button/label width and height, ordinary wrapping, and multiple DPI/width settings.
- Verify resources from build and packed outputs for every supported framework. Resource-only satellites must accompany the library/demo output.
- Restore temporary culture changes in tests. Tests asserting the English diagnostic contract declare an English UI culture explicitly; separate tests cover German and later translations.

## Scope

The agreed languages are English, German, French, Spanish, simplified Chinese, traditional Chinese, Japanese, Arabic, Hebrew, and initially Hindi. All four demo applications are included as GUI demonstrators. New upload-progress and token-renewal messages use the same resource conventions.

The demos share their login/startup resource implementation; WebDAV, ownCloud Classic, Nextcloud, and Scopevisio each embed the resources under their own assembly namespace. Localizing a form's construction does not read/write credentials or change profile keys. Persisted credential behavior is unchanged. Stable branded demo-window titles and product/provider names remain recognizable across cultures, including the exact Nextcloud demo title used by visual-review tooling.

Arabic/Hebrew text resources are separate from full RTL GUI support. This chat preserves the existing layout direction. [The deferred RTL design](RTL_LAYOUT_PLAN.md) records the per-control mirroring matrix, intentional exceptions, mixed-text/clipboard identity, focus, accessibility, and later verification for #66. Implementing `RightToLeft` or `RightToLeftLayout` is deferred. A translated text pack does not claim full bidirectional GUI acceptance.

## Pack verification and glossary

Each additional language covers 213 UI, 153 provider, and 10 shared demo resource
keys, including upload progress and reauthorization. The initial machine-translated
templates were reviewed for resource/argument preservation and common operation
terminology. Human-edited primary action labels distinguish sending/uploading from
downloading, sharing from financial shares, type from a typing instruction, and
technical IDs from identification documents. The localized all-users principal
retains its technical ID. French uses *Envoyer / Télécharger / Partager*, Spanish
*Subir / Descargar / Compartir*, simplified Chinese *上传 / 下载 / 共享*, traditional
Chinese *上傳 / 下載 / 共用*, Japanese *アップロード / ダウンロード / 共有*, Arabic
*رفع / تنزيل / مشاركة*, Hebrew *העלאה / הורדה / שיתוף*, and Hindi
*अपलोड / डाउनलोड / साझाकरण*. Professional native-language copy review is not
claimed by automated resource and rendering tests.

Regional tests cover fr-FR/fr-CA, es-ES/es-MX, zh-CN/zh-SG → zh-Hans,
zh-TW/zh-HK/zh-MO → zh-Hant, ja-JP, ar-SA/ar-EG, he-IL, and hi-IN. Unsupported
ko-KR uses neutral English. Full physical satellite key sets and indexed placeholder
multisets are checked without allowing per-key fallback to hide missing entries.
Every demo embeds and loads its own resource namespace. Localized sign-in and exit
buttons retain spacing when their preferred widths grow.

## Control-fit acceptance (2026-10-08)

`LocalizedLayoutAcceptanceTest` verifies ten cultures (`en-US`, `de-DE`, `fr-FR`,
`es-ES`, `zh-CN`, `zh-TW`, `ja-JP`, `ar-SA`, `he-IL`, `hi-IN`) at 100%, 150%,
and 200% of the form's initial font size. The 30 cases exercise fourteen scenarios:
all four demo login forms, both browser dialog modes, link sharing, internal user
and group sharing, the sharing list, instance selection, upload/download progress, and the
custom text-input dialog. Resizable forms are also checked at their declared
minimum size and with additional width/height. All six upload states are checked
with long Unicode filenames and large byte counters.

The audit checks child bounds against parent bounds, sibling intersections,
preferred button/checkbox/label dimensions, group captions, and translated list
column widths using real native WinForms controls. It originally reproduced
overlapping login labels/inputs, link-field/toggle collisions, clipped headers,
insufficient control heights, and crowded progress rows. The corrected layouts
fit the tested cultures and sizes without those failures. They retain existing
control names, handlers, sharing metadata, login behavior, and layout direction.

The matrix passed on WKS08; a run saving rendered PNGs took 69 seconds. Selected
French, Spanish, Hindi, Arabic, Hebrew, and Japanese snapshots were also visually
reviewed, including enlarged fonts and long progress values. Rendering uses fake
providers and suppressed profile Load/Closing handlers, so it does not read saved
credentials or contact shared DMS servers. Set `DMS_LOCALIZATION_PREVIEW_DIR` when
running `LocalizedLayoutAcceptanceTest` to save diagnostic PNGs.

The Windows CI display additionally exposed bottom-bar overlap at 200% font
size. The browser now wraps action controls when the available display width
cannot accommodate one row. The same culture/font matrix also checks a
900-pixel maximum window width on larger developer displays.

Font-size stress is not a claim that every physical monitor DPI, installed font,
OS-owned dialog, tooltip implementation, or third-party message box has been
exhaustively verified. Full RTL mirroring remains deferred to #66. Earlier
focus-test failures occurred while Windows kept an external foreground window:
`Form.ActiveForm` was null despite native focus inside the test form. The focus
fixture now explicitly establishes its foreground preconditions; separate
window-switch cases continue to verify that production code does not steal focus.

On WKS08, `DevelopmentGuiTestSession` shows a nonactivating topmost three-second
warning before any test fixture and keeps an ETA notice visible until teardown.
Estimates use the slowest of the last five successful completed runs of the same
scope, scaled by work units, plus 25% and five seconds of margin. Local measurements
are stored in `gui-test-durations.tsv` beside the test executable; compilation and
the countdown are excluded. Failed/interrupted runs do not shorten the estimate.
Set `DMS_GUI_TEST_RUN_KEY` to identify a filtered/full suite and framework, and
optionally `DMS_GUI_TEST_WORK_UNITS` for repeatable workload units. Without history,
the fallback is 60 seconds (`DMS_GUI_TEST_FALLBACK_SECONDS` overrides it).
`DMS_GUI_TEST_ETA_SECONDS` takes precedence as an explicit override. An overrun
continues to show elapsed excess time until teardown. Other workstations opt in with
`DMS_GUI_TEST_NOTICE=1`. Native tests verify that showing/updating the notice
preserves the foreground window and that cleanup closes it.

References: [.NET resource lookup](https://learn.microsoft.com/en-us/dotnet/core/extensions/retrieve-resources), [satellite assemblies](https://learn.microsoft.com/en-us/dotnet/core/extensions/create-satellite-assemblies), and [WinForms bidirectional control behavior](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/bi-directional-support-for-windows-forms-applications).

## Native-monitor acceptance

`LocalizedMonitorDpiTest` is an opt-in physical-monitor fixture. It uses the primary
monitor and the nearest monitor to its right with a different Windows scale factor.
The hardware inventory and native window rectangles/DPI are recorded independently
of font-size stress. On WKS08 the offered displays are 1920 x 1080 at 100% (96 DPI)
and 3840 x 2160 at 125% (120 DPI). Other attached monitors are inventoried without
moving test windows onto them. The tests do not change display settings.

Each of the ten cultures is checked starting on either monitor, with two round
trips across the monitor boundary. The fourteen preview scenarios also
cover minimum/wider resizable forms, a 900-logical-pixel browser width, and the six
upload states with Unicode filenames and large counters. The audit verifies both
native and managed DPI, visible control/text fit, overlap, and window work-area
bounds. Profile Load/Closing handlers are suppressed and providers are fake.
This does not establish real server behavior, RTL mirroring, OS-owned dialogs,
tooltip placement, every physical DPI setting, or every installed font.

The standalone subset supplies its own Windows executable manifest and application
configuration for both runtimes; a generic .NET Framework test host does not prove
the demo application's DPI configuration. Build and run the framework variants
sequentially:

```powershell
dotnet build CompuMaster.Dms.Test.BrowserUI/PhysicalDpi/PhysicalDpi.vbproj -c CI_CD
dotnet run --project CompuMaster.Dms.Test.BrowserUI/PhysicalDpi/PhysicalDpi.vbproj -c CI_CD -f net8.0-windows --no-build
dotnet run --project CompuMaster.Dms.Test.BrowserUI/PhysicalDpi/PhysicalDpi.vbproj -c CI_CD -f net48 --no-build
```

Optional arguments select cultures, for example `-- en-US hi-IN`, and
`--no-screenshots` retains the audit log without PNGs. Each run writes a timestamped
`physical-dpi-results` directory beside its executable, records pass/fail totals,
and returns a failing exit code for setup, assertion, or notice-cleanup failures.
For focused diagnosis, `DMS_PHYSICAL_DPI_SCENARIOS=7,8,9,13` selects external-link,
user/group-sharing, and input dialogs. Omit this variable for the complete fourteen
scenarios. Scenario 12 is upload progress, 13 is the input dialog, and 14 is download
progress. Selected scenarios are included in the timing key and estimate.
The standard workstation countdown/ETA notice remains active throughout the run.
This runner supplies timing keys for each runtime, monitor resolution/scale, and screenshot mode, scales
the learned estimate by the selected culture cases, and logs estimated/actual
durations. A first run budgets 20 seconds per culture without PNGs or 45 with PNGs,
plus five seconds at up to 125%; the initial budget scales with native pixel area
for larger DPI settings. Historical full runs at 100%/125% took approximately 182 seconds
on .NET 8 and 187 seconds on .NET Framework, excluding the countdown; corresponding
estimates with margin are 233 and 239 seconds, rather than the former fixed 120.
The NUnit version in the usual BrowserUI project requires
`DMS_PHYSICAL_DPI_TESTS=1` and must use an interactive multi-monitor workstation.

The demos configure PerMonitorV2 before opening windows. The library does not
change its consuming application's DPI awareness. On .NET Framework, a form whose
initial native DPI differs from its managed DPI is reconciled through WinForms'
normal DPI notification using the actual Windows value. Layout updates wait for
the DPI transition to finish. Explicitly managed field bounds avoid a second
application of right anchoring, and login button rows are recomputed from current
content/client bounds rather than previously scaled positions.

At physical 200% DPI, the field layout also reserves the actual native checkbox
width rather than a fixed 24-pixel offset. Custom input dialogs compute available
prompt width and required height from their current scaled table padding/margins.
User/group-sharing layouts also own group-box positions and widths; redundant
right anchoring previously shifted whole groups outside the form on a return trip.
Sharing-list group widths are similarly recomputed from current client bounds,
preventing repeated monitor changes from shrinking their docked lists to zero.
The Show files toggle uses the focused #81 fix from immutable commit `b93c5eb`:
centered text, symmetric padding, explicit sizing and localized toggle-button fit.
The common audit checks its single-line caption width/height as well as centering,
including German "Dateien anzeigen", minimum windows and monitor round trips.
Native 200% snapshots additionally required extra checkbox text insets beyond the
initial #81 padding calculation. The audit now measures wrapped text against the
single-line height within the available text area; German snapshots are visibly
single-line at 100%, 125%, and 200% after this refinement.

## Workstation acceptance checkpoint (2026-10-09)

The offered 100%/125% configuration passed before any display-setting change.
One temporary change of the right 4K monitor to 200% exposed further native layout
failures, which were reproduced and corrected within that same window. The monitor
was restored to 125%; resolution and monitor positions were preserved.

| Physical configuration | .NET Framework 4.8 | .NET 8 |
| --- | --- | --- |
| 100% / 125%, final complete thirteen-scenario matrix | 20 passed | 20 passed |
| 100% / 200%, complete thirteen-scenario matrix | 20 passed | 20 passed |
| 100% / 200%, both browser modes after final caption-width refinement | 20 passed | 20 passed |

Each matrix covers all ten cultures, starts on either monitor and makes two round
trips. The final 100%/125% complete matrices include the caption-width refinement.
The 200% complete matrices preceded that last checkbox-only refinement; both
affected browser modes were then reverified in every culture on both runtimes.
German rendered snapshots were visually checked at all three physical scales.

Final general GUI verification covered 302 cases, including all 30 culture/font
cases and the timing-history regression. The last complete run passed 301/302;
one focus case lost its foreground setup prerequisite before the behavior check.
The focused rerun passed all 17 focus cases at the same source state. Earlier
sandbox-only foreground-access failures are not production regressions and are
retained separately. All runs used and cleaned up the workstation notice.

Measured final complete 100%/125% runs took 174 seconds on Framework and 166 on
.NET 8; the 200% runs took 261 and 181 seconds before the caption-only followup.
Experience-based estimates distinguish runtime, monitor configuration, selected
scenarios and screenshot mode. A 238-second estimate overran during the 261-second
Framework run; its recorded duration raises the next comparable estimate to 332
seconds. Failed runs do not update the timing history.

Physical 150%, remaining tooltip/message/OS-dialog coverage, other installed font
sets and RTL mirroring are not established by this checkpoint. No remote server
tests or package publication are included; provider code and SDK pins are unchanged.
