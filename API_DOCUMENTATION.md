# Library API documentation

The XML documentation pass covers the final provider and BrowserUI surface,
including upload progress and session-renewal integration. Public/protected types,
explicit constructors, methods, properties, fields, delegates, events, enums and
enum values were inventoried. Overrides use explicit `inheritdoc` where the inherited
contract applies; provider-specific limitations remain on the relevant provider or
capability contract. XML documentation remains English in every locale.

The compiled surface was compared with generated XML member IDs: 792 provider
entries and 96 BrowserUI entries have a summary or appropriate inherited contract.
Compiler-generated accessors, delegate implementation methods and nine implicit
parameterless provider constructors are represented by their property/delegate/type
contracts rather than introducing source constructors or runtime changes solely for
documentation. Private helpers and internal transport adapters are outside this
public/protected inventory.

The pass adds principal, sharing/link, credential, factory, exception, browser mode
and action descriptions. Existing empty parameter descriptions on public/protected
contracts are completed, and empty property return stubs are removed. Local and
remote paths remain distinct in exception contracts. The legacy credential text
helpers explicitly state that their base implementations do not encrypt/decrypt
their input. Direct CenterDevice login remains incomplete and is not advertised as
a working authorization flow.

Documentation generation is enabled for every supported library target:

| Package | Targets inspected |
| --- | --- |
| `CompuMaster.Dms.Providers` | netstandard2.0, net48, net6.0 |
| `CompuMaster.Dms.BrowserUI` | net48, net8.0-windows |

All five target builds completed without library warnings. Local pack validation
used version `0.0.0-local`, without publication. Every framework directory contains
the library XML documentation and all nine non-English resource satellites
(`de`, `fr`, `es`, `zh-Hans`, `zh-Hant`, `ja`, `ar`, `he`, `hi`). NuGet represents the
Windows target as `net8.0-windows7.0` inside the BrowserUI package.

The documentation-only VB edits were checked after removing comment/blank lines:
runtime statements exactly match the preceding functional commits. Existing API
names, signatures, enum values and default behavior are retained.

Related detailed contracts: [filesystem failures](FILESYSTEM_CONTRACTS.md),
[WebDAV metadata](WEBDAV_METADATA.md), [localization](LOCALIZATION.md),
[upload progress](UPLOAD_PROGRESS.md), and [token lifecycle](TOKEN_LIFECYCLE.md).
