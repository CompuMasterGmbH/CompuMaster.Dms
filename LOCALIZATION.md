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
and 200% of the form's initial font size. The 30 cases exercise fifteen scenarios:
all four demo login forms, both browser dialog modes, link sharing, internal user
and group sharing, the sharing list, instance selection, upload/download progress, and the
custom text-input dialog, and the download conflict dialog with its apply-to-all
choice. Resizable forms are also checked at their declared
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
trips across the monitor boundary. The fifteen preview scenarios also
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
user/group-sharing, and input dialogs. Omit this variable for the complete fifteen
scenarios. Scenario 12 is upload progress, 13 is the input dialog, and 14 is download
progress, and 15 is the download conflict dialog. Selected scenarios are included
in the timing key and estimate.
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


## Exception parity and scoped browser follow-up (#52, #91, #92, #93)

The 2026-10-11 source audit covered application-generated exception text in provider and Data code, including synchronous and native asynchronous CenterDevice/Scopevisio paths and WebDAV paths inherited by Nextcloud/ownCloud. Equivalent operation failures should use the same resource, placeholders and original argument values. Protocol/API operation names, server diagnostics, resource identities and runtime/SDK messages are not translated as component-owned prose.

The confirmed independent defects are addressed in separate commits on one aggregate branch:

| Failure | Correction |
| --- | --- |
| Collection name interpreted as a nested path | One root-only guard and one localized resource for both entry points; preserve NotSupportedException and the existing slash-only parser. |
| CenterDevice directory/collection creation server failure | A common localized IOException factory retains the operation, path, response code and original cause. |
| Collection copy/rename constraints and folder-to-root constraints | Use the corresponding synchronous explanatory resource in the native asynchronous path. |
| Link update without an identifier | Use the same localized update diagnostic; deletion retains its distinct generic ID diagnostic. |
| CenterDevice link server rejection | Share creation error mapping; preserve the original Forbidden/BadRequest exception and optional server explanation. Updates also retain their original cause. |
| WebDAV unsuccessful HTTP download | Shared validation for raw, processed and progress variants, retaining remote 404 metadata and using localized IOException for other unsuccessful responses. |
| Synchronous WebDAV download task failure | Preserve the original transport/cancellation exception instead of adding AggregateException. |
| WebDAV precondition failure with an existing source | Async includes the same explanation and HTTP status as sync. |
| WebDAV single-item HTTP listing failure | Sync retains the HTTP status as an inner diagnostic, matching async without changing the existing outer type. |

The source review also distinguishes intentional differences: native cancellation has no corresponding synchronous cancellation token; asynchronous WebDAV downloads stage and replace files whereas synchronous downloads use File.WriteAllBytes; local filesystem types can therefore differ by primitive/runtime. Native metadata/selected-ID validation and uncertain-write compensation expose details specific to their actual execution path. These are not grounds for replacing useful diagnostic evidence with a less informative common string. SDK/runtime/server diagnostics and additional authentication boundaries retain their existing contracts; no blanket conversion of all provider exceptions is claimed.

### Scoped browser verification

A scoped browser root backed by a CenterDevice/Scopevisio collection previously offered collection sharing/link management. Collection management is now hidden and guarded before provider mutation, including direct command invocation. Ordinary child-folder creation remains available, uses the resource's actual FullName, and displays the created child beneath the visible root. Stale root nodes cannot silently fall back to the hidden provider root. Public initialRootFolder parameters and InitialFolder document the limitation; SelectedFolder remains an independent initial selection. The root-only limitation is mapped internally to the owning CenterDeviceDmsProviderBase adapter family because the current common capability API does not describe collection placement; other providers retain their own collection capabilities. Changing the configured root does not lift collection restrictions until the actual provider root is present in the displayed tree.

The audit also reproduced an explicit initialRootFolder="/" initialization bug: collections were filtered out as if the actual root were an ordinary resource-backed folder. Actual-root representations now follow normal root initialization. Root-relative navigation handles leading separators consistently.

DmsBrowserScopedCollectionTest opens native forms with an isolated provider, checks folders-only and files/folders modes, visible collection and nested-folder roots, slash-prefixed/actual-root inputs, selection and busy-state restoration, blocked direct mutations, no collection writes outside the visible tree, and unchanged child creation. Root and child-selection renderings were inspected. DevelopmentGuiTestSession displays the required nonactivating countdown/ETA notice. No real DMS server or saved credentials are used.

See FILESYSTEM_CONTRACTS.md for the deliberate synchronous download exception-type correction and local/remote exception distinction. The aggregate PR and issue checklists record exact final test evidence separately from CI, real-server checks, merge and publication.

### Final local checkpoint (2026-10-11)

The functional source at `f766fb8` passed the following isolated checks with the released dependency pins unchanged:

| Check | Result |
| --- | --- |
| Complete default-level provider suite, net8.0 | 592 passed. |
| Complete default-level provider suite, net48 | 592 passed. |
| Complete default-level provider suite, net8.0 with EnableNativeAsync=false | 341 passed. |
| Complete default-level BrowserUI suite, net8.0-windows | 392 passed; 20 optional/extended cases skipped. |
| Scoped browser regression subset, net48 | 16 passed. |
| Provider netstandard2.0/net48/net6.0 and BrowserUI net48/net8.0-windows builds | Zero warnings and errors. |

The default-level filter is `TestCategory!=RemoteDms&TestCategory!=TestLevel2`. The normal BrowserUI test project targets only net8.0-windows. The net48 result uses a temporary test host linking the same scoped-browser fixture, dispatcher and development-notice sources against the actual net48 BrowserUI output; it does not claim a complete net48 GUI-suite run. All interactive GUI runs displayed and cleaned up the workstation notice. Existing obsolete-constructor warnings occur in the GUI test project, not in the library builds.

No shared-server test, cross-platform CI, merge or publication was performed for this checkpoint. The aggregate draft PR's documentation tip uses `[skip ci]` until an exclusive shared-server window is coordinated as required by AGENTS.md. Required CI remains pending; the previous release's successful runs do not verify this patch.
## Message, tooltip and installed-font follow-up (2026-10-11)

`LocalizedMessageAcceptanceTest` checks all ten UI cultures and unsupported
`ko-KR` fallback with an independent English formatting culture. Browser failures
use localized component prose while host diagnostics retain the original exception
and inner exception. Unicode paths and literal format braces remain complete.
Upload retry hints are translated; diagnostic tooltips retain original technical
exception text. Native retry-hint popup sizes are checked against the current
work-area size.

The extended popup/font checks require `DMS_LOCALIZATION_EXTENDED_TESTS=1` and have
category `LocalizationExtended`. Without that switch all 41 extended cases are
ignored, including in CI; opt-in-off verification also passed. Physical-monitor
checks retain their independent opt-in. Neither fixture changes Windows settings,
uses saved profiles/credentials or contacts a DMS server. Countdown/ETA notices
and measured timing history remain active.

`LocalizedFontAcceptanceTest` reuses the layout audit for all fifteen current
preview scenarios, including upload/download recovery and download conflicts.
All ten cultures are checked with installed Segoe UI, Arial and Tahoma at 150% of
the initial font size, including minimum/wide/constrained browser sizes and all
transfer/recovery states. Missing fonts are skipped rather than silently substituted;
PNG names include the font family. This is representative installed-font coverage,
not native-language copy review or proof of every glyph/font installation.

The Framework host reproduced four existing 200%-font constrained-browser failures
(German, French, Spanish and Japanese): the instance action itself exceeded the
client width even after action rows wrapped. It now uses the available width and
reserves measured wrapped-caption height before row placement. Four new mixed-Unicode
regressions check bounds, height and unchanged instance text. No ID, callback,
event handler or tab order changes. Selected German/French Framework and
German/Tahoma, Hindi/Segoe UI and Arabic/Arial .NET 8 previews were inspected.

The optional `LocalizationAcceptance/LocalizationAcceptance.vbproj` links the same
fixture sources for both runtimes, outside the normal solution/CI path. Run
sequentially, with an appropriate timing key/estimate for the selected scope:

```powershell
$env:DMS_LOCALIZATION_EXTENDED_TESTS = '1'
dotnet test CompuMaster.Dms.Test.BrowserUI/LocalizationAcceptance/LocalizationAcceptance.vbproj -c CI_CD -f net48
dotnet test CompuMaster.Dms.Test.BrowserUI/LocalizationAcceptance/LocalizationAcceptance.vbproj -c CI_CD -f net8.0-windows
Remove-Item Env:DMS_LOCALIZATION_EXTENDED_TESTS
```

Final verification: the same-source Framework linked subset passed 97/97 in
76 seconds. The permanent dual-target project builds without warnings/errors;
its Framework default subset passed 26 message/long-instance cases with all
11 popup cases skipped. The final .NET 8 combined run passed all 116 localization,
instance and toolbar cases. Its extra 17 focus cases could not establish their
foreground prerequisite (`AttachThreadInput` returned access-denied error 5);
no current focus-behavior success is claimed. Seven final PNG cases also passed.
Retained TRX distinguishes setup limitations from reproduced layout failures.

Focused native-monitor checks of both browser modes (scenarios 5/6) passed 20/20
culture/start-position cases on each runtime at the unchanged physical 100%/125%
configuration, including two round trips. Framework took 38 seconds; .NET 8 took
36 seconds. This does not reverify all fifteen native scenarios or physical
150%/200%. OS-owned message-box/file-dialog text and placement, physical 150%,
other font installations and full RTL UX remain separate acceptance in #52/#66.
## Native dialog closeout verification (2026-10-12)

The closeout for #67, #68, #69 and #71 starts from exact main
`107cf50dc0e565a4395735aea1ff40b3b2bac044`. No library, provider, resource,
SDK dependency or public/protected API changes are included in this follow-up.

The extended acceptance subset actually executed and passed 97/97 cases on each
of net48 and net8.0-windows, with no skipped cases. This includes all ten cultures,
installed Segoe UI/Arial/Tahoma, fifteen layout scenarios, independent formatting
culture, unsupported-culture fallback and the eleven native retry-tooltip cases.
The current-main focus fixture passed 17/17; its foreground prerequisite was
established successfully. Provider localization checks passed 14/14 isolated cases.

`NativeLocalizedDialogAcceptanceTest` adds optional Windows-owned message, open,
save and folder dialog acceptance for all ten cultures, starting on either display.
It verifies actual modal visibility, owner-monitor placement, work-area bounds,
complete localized titles, mixed-Unicode message/filename preservation and measured
message height. OS-provided buttons/navigation follow the installed Windows language;
this does not promise to translate Windows itself with CurrentUICulture.
Native window DPI is recorded rather than assuming Windows MessageBox uses the
same DPI awareness as the application. Optional PrintWindow previews support visual
review. The ten-second watchdog closes only a dialog owned by the test's window;
callback failures are propagated outside the modal loop. Each run creates and
verifies removal of one unique, empty local temporary directory, preserving any
original failure if cleanup also fails. No DMS server or saved profile is used.

The fixture is linked into the dual-target acceptance project. Without
`DMS_LOCALIZATION_EXTENDED_TESTS=1`, its twenty native-dialog cases are explicitly skipped.
The twenty physical-tooltip cases additionally require `DMS_PHYSICAL_DPI_TESTS=1`.
Ordinary CI explicitly skips all forty optional cases.
The native executable can run these checks after each culture/monitor layout case
with `DMS_NATIVE_DIALOG_ACCEPTANCE=1`, preserving the real application's manifest
and DPI configuration on both frameworks. The optional native-dialog scope is
included in the notice timing key and initial estimate.

The workstation GUI window was explicitly coordinated with the concurrent DMS
chat. Launchers hold `Local\CompuMaster.Dms.WorkstationGuiTests` before countdown
and release it in finally; a busy lease fails before starting a test process.
The existing nonactivating notice, countdown, ETA/overrun display and cleanup remain
active. The fixture does not change Windows display scaling. The user separately
authorized the temporary physical 150% workstation configuration described below.

Coordinated post-merge server CI remains pending.
Publication requires a separately approved release after those required CI gates.
The 2026.10.11.0 release predates PR #99 and this follow-up. Full RTL remains
outside these language/demo tickets and is tracked in #66; exhaustive coverage
of all Windows/font installations is not claimed.
The first Framework native-message run retained 18/20 passing cases and two
Chinese primary-monitor measurement failures (28 native client pixels versus a
39-pixel managed Font/TextRenderer estimate). The native messages were visually
complete. The corrected audit uses the control's own HDC/HFONT, GetClientRect and
DrawTextW with DT_CALCRECT/DT_WORDBREAK/DT_NOPREFIX, avoiding conversion between
managed-font, physical-window and native-client units. The targeted zh-CN/zh-TW
run passed all four culture/start-monitor cases and retained complete message
previews. Failure previews are now captured before assertions. This corrects the
test oracle; no production dialog or translation change is inferred.
The final native-measurement .NET 8 matrix passed 20/20 culture/start-monitor
cases in 206.2 seconds at physical 100%/96 DPI and 125%/120 DPI. It checked all
fifteen scenarios, two monitor round trips, minimum/wide/constrained sizes and
all four native dialog types per case (80 observed dialogs). The earlier snapshot
matrix also passed 20/20; representative native Hindi/Chinese/Spanish and
mixed-Unicode font/instance previews were inspected. The corrected final Framework
matrix also passed 20/20 in 197.5 seconds; the
earlier 18/20 test-oracle result is retained.

The approved physical 100%/150% (96/144 DPI) matrices passed all twenty
culture/start-monitor cases on each runtime: Framework in 257.7 seconds and
.NET 8 in 226.3 seconds. Each matrix checks all fifteen scenarios, both round
trips, three window sizes and eighty observed native modal dialogs. Representative
150% Hindi login and French sharing previews were inspected. The right-hand
3840x2160 monitor was restored to its original 125%; all four monitors' scaling,
resolution and position matched the inventory taken before the temporary change.
No registry writes, saved credentials, remote DMS resources or SDK pins were used.

`DMS_NATIVE_TOOLTIP_ONLY=1` selects only the twenty physical-monitor retry-tooltip
cases in the native executable. It retains the application manifest/DPI settings,
notice/countdown, measured ETA and cleanup. The audit checks actual native popup
creation, exact localized text, positive size within the monitor work area and
matching WinForms/native owner DPI. A completed failed-transfer fixture supplies
the retry hint. ShowAlways is enabled only on this fixture because foreground
ownership after a nonactivating notice is not a tooltip-fit prerequisite.

The final tooltip-only matrices passed 20/20 cases on each runtime at both
100%/125% and 100%/150% (80/80 additional native popups in total). On the right
monitor, both native and WinForms owner DPI were 120 or 144 as configured.
The first inactive-host attempt retained 19/20 and the foreground-prerequisite
attempt retained 0/20; these fixture prerequisites were corrected before the
final four successful matrices. The final ordinary resource/demo/default-gate
run passed 53 cases with all forty optional native cases explicitly skipped.
The GUI lease was returned after verified display restoration and notice cleanup.
