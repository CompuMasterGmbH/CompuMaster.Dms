# Deferred RTL GUI design (#66)

This is a conceptual plan only. PR #75 adds Arabic/Hebrew text while retaining
every existing GUI direction flag and layout. Full RTL implementation requires a
separate assignment; translated labels do not establish bidirectional acceptance.

Direction should follow the application's selected `CurrentUICulture.TextInfo`
without changing its process culture. Apply direction before handle creation where
possible. Recreate forms when changing UI culture; dynamic switching of open
dialogs is outside the current culture contract.

| Surface | Planned RTL behavior | Intentional exceptions / verification |
| --- | --- | --- |
| Browser form and split hierarchy/file panes | Mirror visual pane order, labels, docking and anchors. Audit Form, SplitContainer and each nested container separately. | Preserve provider tags, selected resource identity and logical panel/column indices. Verify resizing and toolbar overflow. |
| TreeView | Evaluate RightToLeft and RightToLeftLayout together; mirror expander/scrollbar placement. | Keep the nullable child-count/lazy-loading contract and cancellation behavior. File/folder/shared-state icons retain their meaning. |
| ListView | Mirror visual column order and text alignment using supported control settings. | Sorting, selection and subitem access continue to use logical columns; do not reinterpret an index as a translated caption. Mixed filenames retain their exact identity. |
| Toolbars and context menus | Follow RTL reading order and audit menu cascade direction and keyboard access. | Mirror only genuinely directional navigation arrows. Upload/download, file, folder, sharing and brand icons are semantic rather than horizontal navigation arrows. |
| User/group sharing dialogs | Mirror label/field arrangement and permission rows; align checkbox glyphs and text appropriately. | Preserve permission meanings, hidden principal IDs, validation and async busy/focus state. Tab order follows logical reading order rather than reversed numeric indices. |
| Link dialogs and properties | Mirror groups, labels and ordinary prose. Keep values readable independently. | URLs, paths, IDs, hashes and passwords remain exact and usable for selection/copy. Numeric spin arrows are vertical controls and are not flipped as horizontal icons. |
| Instance selection and custom input dialogs | Align captions, choices and confirmation controls to RTL conventions. | Preserve the chosen technical instance ID and original server-provided names; do not derive identity from the display text. |
| Upload progress | Mirror ordinary layout/reading order and audit status list alignment and button placement. | Source-byte counters, provider-confirmed completion, file order, cancellation and terminal states retain their semantics. Filenames and counts require explicit mixed-direction handling. |
| All four demo logins | Mirror ordinary labels, credential fields, start path, checkbox and button arrangement. | Keep brand images/titles identifiable; unchanged profile keys and credential storage. Do not read credentials solely for visual tests. |
| OS-controlled open/save/folder dialogs | Describe and verify OS behavior separately. | Application culture and mirrored custom forms cannot promise control over every native dialog's OS language or layout. |

## Mixed text and accessibility

Keep raw URLs, paths, GUIDs, account IDs and filenames unchanged in the data model,
protocols and clipboard. A later display-only directional-isolation layer may be
needed around embedded technical values. Do not persist invisible direction marks
inside resource identities or introduce them into copied values. Evaluate text
selection, truncation, caret behavior and accessible names with mixed Arabic/Hebrew,
Latin, Chinese, Hindi, digits, separators and punctuation.

Set accessibility names from localized resources where needed. Audit reading order,
initial focus, keyboard shortcuts, tab order and return/cancel behavior per dialog.
Test that focus and enabled state restore correctly after async success, failure,
cancellation and instance switching. Test resize and DPI changes during progress.

### Browser bottom-action keyboard order

For the existing LTR layout, forward Tab follows the visible bottom controls from
left to right: Create folder, Show files, Change instance (when available), then
the visible confirmation/cancel or Close actions. Shift+Tab reverses that order.
Hidden and disabled controls are skipped; a dynamically added instance button
must not jump ahead of the hierarchy/file panes. Mouse clicking continues to
activate the clicked control, without changing the selected resource identity.

Before RTL implementation, explicitly define the expected sequence for the
mirrored bottom bar: forward Tab should follow its visual RTL reading order,
subject to the agreed confirmation/cancel placement. Verify this as a separate
Arabic/Hebrew UX case alongside the LTR baseline, rather than reversing numeric
TabIndex values blindly. Cover optional instance controls, both dialog modes,
hidden/disabled actions, Shift+Tab, async focus restoration and keyboard cues.
This specification adds no RTL layout implementation in this chat.

## Observations from current-host text-only previews

Arabic progress text and Hebrew link labels render with shaped glyphs on the
current Windows host. Controls remain in their LTR positions; labels, status lists
and mixed-script filenames therefore still follow the existing layout. In the
Arabic progress sample, filename punctuation and numeric context visibly need
separate mixed-direction acceptance when mirrored layout is introduced. Hebrew
limit labels demonstrate that ordinary translated-text fit is separate from visual
group/field ordering. No direction flags or direction marks were introduced to
address these observations in this chat.

## Later verification

The separate RTL implementation should include Arabic/Hebrew and LTR regression
baselines on supported Windows targets, multiple native DPI/font configurations,
narrow/wide windows, long mixed names and exact clipboard round trips. Cover every
matrix row with automated behavior checks and representative visual evidence.
Use fake provider snapshots for layout; server-mutating checks retain the existing
exclusive test-window rules. Keep #52 open until full bidirectional acceptance is
actually established.
