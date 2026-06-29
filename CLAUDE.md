This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

GoBDify builds a tamper-evident hash chain over a folder (local or cloud-synced) and notarizes each step with an RFC3161 timestamp from an independent TSA, to prove documents haven't changed since notarization — a core requirement of the German **GoBD** (and Swiss GeBüV / Austrian BAO). The produced artifacts are plain standards: `.sha256` files verify with `sha256sum -c`, `.tst` tokens with `openssl ts -verify`.

The UI and most user-facing text are German. Match that language in code comments, console output, and commit messages.

## Build & test

```bash
dotnet build GoBDify.sln                  # builds all four projects (MAUI GUI only on Windows)
dotnet test --filter Category!=Network    # offline unit tests, ~2 s — use this by default
dotnet test --filter Category=Network     # live round-trip against every configured TSA
dotnet test                               # all
dotnet test --filter "FullyQualifiedName~AuditAsync"   # single test by name
```

Network tests are marked with xunit `[Trait("Category", "Network")]` and skip cleanly offline (`Xunit.SkippableFact`). Always exclude them unless you specifically intend to hit real timestamp servers.

Run the CLI directly during development:

```bash
dotnet run --project GoBDify.Cli -- <folder> --audit
```

### Release

`.\publizieren.bat` (Windows only) builds the signed MSIX (Windows x64+arm64), the CLI for 6 targets (win/linux/osx × x64/arm64), and the `.appinstaller` files, dropping everything under `dist\`. It is **code-signing and FTP-upload aware** — do not run it as part of normal development.

## Architecture

Four projects in `GoBDify.sln`. All business logic lives in **GoBDify.Core** (`net8.0`, no UI/MAUI dependency); the GUI and CLI are thin front-ends over it.

- **GoBDify.Core** — the engine. UI-agnostic, the only place to change chain/timestamp behavior.
- **GoBDify** — .NET MAUI desktop app (currently Windows-only target; macOS/Android/iOS/GTK are planned). Flyout shell, `WorkspaceService` registered as a DI singleton holds the current folder + a shared `ChainProcessor`.
- **GoBDify.Cli** — `AssemblyName=gobdify`. `Program.cs` → `CliRunner` arg-parses and prints; exit codes: `0` ok, `2` arg error, `3` config error, `4` failure, `5` audit issues found (designed for CI).
- **GoBDify.Tests** — xunit, references Core only.

### The chain model (GoBDify.Core/ChainProcessor.cs)

A folder's chain is a sequence of `timestampNNNNN.sha256` files (5-digit, zero-padded). Each `.sha256` lists `<hash> *<filename>` lines (sha256sum format, with `#` comment header) and has one or more sibling RFC3161 token files:

- Single-TSA mode: `timestampNNNNN.sha256.tst`
- **Paranoia mode**: three independent TSAs, suffixed `_a` / `_b` / `_c` → `timestampNNNNN_a.sha256.tst` etc. `AppSettings.Validate()` enforces *exactly three* selected TSAs in this mode.
- A legacy single-token form (`TstLegacyRe`) is still recognized on read.

Each new `.sha256` also hashes the previous one's referenced files, so the sequence forms a blockchain-like linkage. Filename patterns are the three compiled regexes at the top of `ChainProcessor`; `IsChainArtifact()` is the single source of truth for "is this a chain file vs. a user document".

Two entry points:
- `AuditAsync` — read-only. Pass 1 parses every `.sha256` skeleton and locates `.tst` siblings *without hashing*; pass 2 hashes each referenced file and verifies tokens, emitting per-file `ChainEvent`s. Files in the folder not referenced and not chain artifacts are reported as `NewFiles`.
- `AuditAndTimestampAsync` — audits, then (if there are new files and `LastChainNumber < 99999`) writes the next `.sha256`, requests timestamp(s) via `RequestManyAsync`, and writes the `.tst`(s).

### Progress vs. events

The processor reports two parallel streams, both optional: `IProgress<ProgressInfo>` (coarse fraction + message, used by the CLI) and `IProgress<ChainEvent>` (fine-grained discovery/hash/verify records in `ChainEvents.cs`, used by the GUI to drive the live tree). When changing the processing flow, keep both streams meaningful.

### Timestamping (GoBDify.Core/TimestampingService.cs)

Wraps `System.Security.Cryptography.Pkcs` (`Rfc3161TimestampRequest/Token`). `RequestAsync` POSTs an RFC3161 query with a random nonce and verifies the response signature against the hash before returning. `RequestManyAsync` fans out to multiple authorities in parallel (used for paranoia mode). `Verify` re-checks an existing `.tst` against a hash. The seven preconfigured TSAs live in `TimestampAuthorities.cs`, keyed by lowercase `id`.

### Settings (GoBDify.Core/AppSettings.cs)

Persisted as JSON at `%AppData%/GoBDify/settings.json` (`AppSettingsStore`). Serialization uses a **source-generated `JsonSerializerContext`** (`AppSettingsJsonContext`) because the CLI is published trimmed/AOT — register any new serialized type there, don't rely on reflection-based JSON. Both the GUI and CLI share this same file and `AppSettings` shape (selected TSAs, paranoia flag, recent folders, window geometry).

## Versioning

`<Version>` is hand-bumped in **each** of the four `.csproj` files (kept in sync manually) and is the single source of truth — the MAUI build pokes it into `Package.appxmanifest` via the `SyncAppxManifestVersion` target, and `publizieren.bat` greps it out of the CLI csproj. When releasing, bump all of them together.

## Projekt-Konventionen für KI-Assistenz

### Git-Commit-Attribution

In Commit-Messages, die unter KI-Assistenz entstehen, wird der Trailer
`AI-Assisted-By:` verwendet — nicht `Co-Authored-By:`. Begründung:
„Autor"/„Co-Autor" tragen Konnotationen von Verantwortung, Persistenz und
moralischem Subjektstatus, die für eine inferenz-zeitige Modell-Instanz
nicht zutreffen. `AI-Assisted-By:` markiert die Provenienz ohne diese
Überbehauptung.

Format:

```
AI-Assisted-By: Claude <Modell> <noreply@anthropic.com>
```

Beispiel:

```
Fix: Foobar in der Bar-Klasse korrigiert

Bla bla bla.

AI-Assisted-By: Claude Opus 4.8 <noreply@anthropic.com>
```
