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

Representative browser, link/sharing dialogs, and progress views are rendered with
fake providers and suppressed Load handlers. This permits Unicode/layout review
without reading stored credentials or contacting a shared DMS. Set
`DMS_LOCALIZATION_PREVIEW_DIR` when running `LocalizedFormLayoutTest` to save the
rendered PNGs. Full native DPI/font coverage remains dependent on the host fonts
and display configuration; layout direction and ordinary mixed-script text are
tested, while complete bidirectional layout remains outside this implementation.

References: [.NET resource lookup](https://learn.microsoft.com/en-us/dotnet/core/extensions/retrieve-resources), [satellite assemblies](https://learn.microsoft.com/en-us/dotnet/core/extensions/create-satellite-assemblies), and [WinForms bidirectional control behavior](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/bi-directional-support-for-windows-forms-applications).
