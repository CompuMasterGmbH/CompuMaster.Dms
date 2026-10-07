# Upload progress

`UploadFileWithProgressAsync` is an additive provider-independent API. Existing
upload overloads retain their signatures, defaults and provider dispatch. Both
local paths and stream factories accept `IProgress<DmsTransferProgress>`; `null`
disables reporting. A stream returned by the factory belongs to the provider.
Callbacks must not throw. Use `Progress<T>` on the UI context for UI consumers.

`BytesTransferred` means source bytes consumed by the transport, excluding
protocol overhead. It is not a server acknowledgement. `TotalBytes` and the byte
counter are nullable; unknown differs from a known zero. Seekable streams report
the length remaining at their initial position; rewinding does not double-count
source progress. Nonseekable streams report consumed bytes with an unknown total.
If a transport skips source ranges before reading, consumed-byte telemetry becomes
unknown rather than treating skipped bytes as transferred. Seeking to the end for
metadata does not itself consume bytes, and a zero-length read never signals EOF.
Snapshots use 64-bit counters and stream telemetry is throttled to 100 ms or
256 KiB increments. Finalization is reported after the source reaches its end;
`Completed` is reported only after the original provider upload task succeeds.
Failures and cancellation never report successful completion.

WebDAV and the native Scopevisio/CenterDevice transport support source-stream
counts for local files. The base local-file implementation intentionally dispatches
to the existing file override of custom providers and reports unknown counters.
Existing synchronous fallbacks retain their active-cancellation limitations.
Requesting cancellation can stop queued work, but cannot undo an already accepted
write or necessarily interrupt an active synchronous call.

Browser UI shows the file name, batch position, per-file counters when available,
and a separate provider-confirmation phase. Overall byte progress becomes
determinate only after every file's provider counter has a known total; until then
it shows an indeterminate indicator and the confirmed file count. It never derives
byte percentages from file counts. Zero-length sources remain in finalization until
provider confirmation. The file list distinguishes completed, failed, cancelled
and unstarted files. Batches remain sequential and stop at the first failure or
cancellation. No uncertain write is replayed and no rollback is implied.

The destination listing refreshes after partial failure or cancellation. A refresh
failure is retained on the original exception rather than replacing it. The browser
prevents conflicting actions and restores its enabled/cursor state on every exit.
Closing the progress window requests cancellation while the active operation is
pending; the terminal summary can be closed afterwards. Stale progress callbacks
cannot replace terminal file states or touch a disposed dialog.

Verification includes isolated delayed-provider, source-stream, large-counter,
unknown/zero-total, rewind, failure/cancellation and UI-restoration tests. Locked
Level 2 WebDAV fixtures exercise two sequential 32 MiB file-backed uploads. The
existing exclusively coordinated native Scopevisio 256–4096 MiB live test now
checks progress with its hash/download/cancellation lifecycle. These live tests
are not run locally without exclusive access; CI results must be distinguished
from deterministic fake-transport evidence.
