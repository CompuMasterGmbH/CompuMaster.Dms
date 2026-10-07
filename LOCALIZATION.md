# Localization contract

The provider library and BrowserUI use embedded `.resx` resources and ordinary .NET satellite assemblies. Neutral resources are English; German resources cover the same keys. Language packs add translations without a provider-specific culture option or a dependency on Windows Forms in the provider library.

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

Arabic/Hebrew text resources are separate from full RTL GUI support. This chat preserves the existing layout direction. Issue #66 records the later mirroring/control-order/directional-icon/mixed-text design; implementing `RightToLeft` or `RightToLeftLayout` is deferred. A translated text pack does not claim full bidirectional GUI acceptance.

References: [.NET resource lookup](https://learn.microsoft.com/en-us/dotnet/core/extensions/retrieve-resources), [satellite assemblies](https://learn.microsoft.com/en-us/dotnet/core/extensions/create-satellite-assemblies), and [WinForms bidirectional control behavior](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/bi-directional-support-for-windows-forms-applications).
