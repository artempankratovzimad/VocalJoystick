# AGENTS

## Mission for agents
- Vocal Joystick is a Windows-only accessibility suite built on .NET 8 + WPF; prioritize local audio recognition and low-latency cursor control before anything else.
- Treat this repo as a layered MVVM solution with separate projects for UI (`VocalJoystick.App`), shared types (`VocalJoystick.Core`), audio helpers (`VocalJoystick.Audio`), recognition logic (`VocalJoystick.Recognition`), Win32 mouse control (`VocalJoystick.Input`), persistence/infrastructure (`VocalJoystick.Infrastructure`), and the MSTest suite (`VocalJoystick.Tests`).
- Keep changes confined to the relevant project when fixing bugs or adding features; cross-project impacts (settings, recognition, input) should be explicitly documented in the PR description.
- Write tests whenever you touch recognition heuristics or persistence; the MSTest suite verifies feature extraction, directional recognition, profile flow, and settings defaults.
- Preserve the diagnostics experiences described in the README (AppData logs, configuration workflow, and diagnostics panel) whenever you change how voice/command state is surfaced.

## Build / Run / Test commands

### Restore & build
- `dotnet restore VocalJoystick.sln`
- `dotnet build VocalJoystick.sln`
- Use `--no-restore` or `--nologo` only when you explicitly chain multiple commands and have already restored.

### Run the app
- `dotnet run --project VocalJoystick.App/VocalJoystick.App.csproj`
  * Use Visual Studio when you need XAML debugging or designers; otherwise the CLI run targets the same WPF project.
  * The app writes settings/logs under `%AppData%/VocalJoystick/`, so ensure that folder is writable before running on a machine with restricted profiles.

### Tests
- `dotnet test VocalJoystick.sln` (coverage: feature extractor, directional recognition, persistence, click/pitch detection).
- For single MSTest cases, target the test project with a filter; e.g.
  `dotnet test VocalJoystick.Tests/VocalJoystick.Tests.csproj --filter FullyQualifiedName~VocalJoystick.Tests.AppSettingsTests.CreateDefault_UsesIdleModeAndDefaultSpeed`
  * `--filter Name~Something` also works when the FullyQualifiedName is too long.
- `dotnet test VocalJoystick.Tests/VocalJoystick.Tests.csproj --filter TestCategory=SmallerGroup` if you add categories. Keep each run fast by filtering to the relevant class or method.

### Lint / format
- `dotnet format VocalJoystick.sln` keeps C# formatting aligned with the implicit editorconfig from MSBuild (the generated editorconfig files in the `obj` folders are not authoritative; rely on the solution defaults).
- `dotnet format --verify-no-changes VocalJoystick.sln` before merging to ensure style checks would pass.
- No ESLint/StyleCop configs exist; if you add more analyzers, state that in this file and reference the config location.

## Diagnostics & persistence notes
- Active logs live at `%AppData%/VocalJoystick/Logs/log-YYYYMMDD.txt` (UTC timestamps, INFO/WARN/ERROR tags) and are written by the shared `ILogger` implementation during runtime.
- `AppSettings` and profiles persist under `%AppData%/VocalJoystick/`, so any integration test that writes to disk should use temporary folders (see `PersistenceTests.TestStorageLocation`).
- The VAD/pitch diagnostics panel mirrors `DirectionalRecognitionDebugState`, so any change to recognition outputs should update both the log message and the UI bindings (`DirectionVoiceDisplay`, `DirectionTemplateStatus`, etc.).

## Repository layout reminders
- `VocalJoystick.App` – WPF host with `MainWindowViewModel`, commands, diagnostics panel, and `FireAndForget` helper for background tasks.
- `VocalJoystick.Core` – shared enums, models, interfaces, and settings definitions consumed by app/recognition/input layers.
- `VocalJoystick.Audio` – audio buffer helpers and WASAPI wrappers (NAudio-based) used by capture services.
- `VocalJoystick.Recognition` – feature extraction, pitch detection, voice activity detection, directional/click recognition, and helper classes like `ShortClickRecognitionEngine`.
- `VocalJoystick.Input` – Win32 cursor control via `SendInput` abstractions; `Win32MouseController` exposes `MoveAsync`, `ClickAsync`, `DoubleClickAsync`.
- `VocalJoystick.Infrastructure` – JSON persistence (profiles/settings), logging helpers, sample recording, and storage location implementations.
- `VocalJoystick.Tests` – MSTest suite with `TestStorageLocation` utilities, async tests, and pattern-based method names (`Subject_State_ExpectedResult`).
- `VocalJoystick.sln` ties everything together; avoid shifting project references without updating the solution.

## Environment requirements
- Windows 10/11 with .NET 8 SDK/runtime, microphone(s), and `%AppData%/VocalJoystick/` writable rights.
- Diagnostics rely on real-time microphone access, so mock capture services in unit tests rather than hitting hardware.
- Keep `NAudio` dependencies at the latest compatible versions when updating packages; test on a Windows dev machine before releasing.

## Code style guidelines

### Using statements & namespaces
- Order `using` declarations with `System.*` first, followed by third-party namespaces (e.g. `NAudio.*`) and then solution namespaces alphabetically.
- Prefer file-scoped namespaces (`namespace VocalJoystick.App.ViewModels;`) rather than block namespaces.
- Keep unrelated `using`s out of files; rely on IDE tooling to surface unused ones before committing.
- `Global using` directives are not currently used; add them only if a type appears in most files and document the global import location.

### Formatting & blocks
- Indent with four spaces; avoid tabs.
- Always include braces for control blocks (`if`, `for`, `switch`) even when a single statement follows.
- Use expression-bodied members for short getters/setters/static helpers (e.g. `public void Reset() => ResetState();`).
- Preserve blank lines between logical sections (fields, constructors, public properties, private helpers) to keep large view models readable.
- Keep methods under ~120 lines. Split helper logic into private methods/records when a method handles multiple responsibilities (see `BuildClickTemplates` + `UpdateDirectionalTemplates`).

### Naming & types
- Types and members use `PascalCase`; private fields use `_camelCase` and are marked `readonly` when injected or constant.
- Interfaces start with `I` (`IAudioCaptureService`, `ILogger`), records start with `record` or `record class` when immutability makes sense (e.g. `DirectionalCandidate`).
- Async helpers always end with `Async` (`StartCaptureAsync`, `BuildMetadataAsync`) and return `Task`/`Task<T>` unless synchronous by design.
- Prefer `var` for local declarations where the right-hand side makes the type obvious (`var settings = AppSettings.CreateDefault();`) but use explicit types when clarity demands it (e.g. `CancellationTokenSource cts`).
- Avoid Hungarian notation or abbreviations; log-readable strings should describe their purpose so diagnostics remain understandable.

### Properties & state helpers
- View models inherit from `ViewModelBase`; use `SetProperty`/`OnPropertyChanged` helpers for backing fields.
- Properties that expose mutable state (e.g. `DirectionRecognitionConfidence`) should call `SetProperty` inside a `if` guard if additional work happens when the value changes.
- Expose read-only collections as `IReadOnlyList<T>` or `IReadOnlyDictionary<TK,TV>`; mutate via private fields and update via `OnPropertyChanged`.
- Use `ObservableCollection<T>` only for UI-bound lists whose contents change; the rest can be plain `IReadOnlyList<T>` or arrays.

### Error handling & logging
- Favor guard clauses and early returns to keep the happy path unindented (`if (_profileConfiguration is null) return;`).
- Validate inputs with `ArgumentNullException.ThrowIfNull(parameter);` when exposing public APIs.
- Log actionable errors via `_logger.LogError`/`LogWarning`/`LogInfo`; include concise messages and, when necessary, the caught exception (`_logger.LogError("Pitch detection failed", ex);`).
- Do not swallow exceptions silently; if you must (e.g. `OperationCanceledException` in loops), comment why or rely on the logger.
- UI code should translate failures into user-facing status messages via `SetStatusMessage`; include the severity string (`Info`, `Warning`, `Error`).

### Asynchrony & threading
- Always capture a `CancellationToken` parameter for long-running operations and pass it through to awaited calls.
- Use `ConfigureAwait(true)` inside UI-facing view models so continuations run on the `SynchronizationContext`; use `ConfigureAwait(false)` in shared libraries (`Infrastructure`, `Recognition`, `Input`).
- Do not expose `async void`; instead, wrap fire-and-forget work with a helper that logs unhandled exceptions (`FireAndForget`).
- `Task.Delay` uses `ConfigureAwait(false)` and accepts the cancellation token.
- When starting loops (movement loop, recognition flow), cancel them explicitly via `CancellationTokenSource` before disposing resources.

### Exception safety & invariants
- Throw `InvalidOperationException` when callers violate contract (e.g. recording when `_activeSession` already exists).
- Always clean up native resources (`WaveFileWriter`, `CancellationTokenSource`, event handler subscriptions) in `Dispose`/`finally` blocks.
- Wrap background flows in `try/catch` and surface failures through logging + status text rather than letting exceptions crash the app.

### Testing conventions
- MSTest is the only test framework in use; decorate suites with `[TestClass]` and specs with `[TestMethod]`.
- Prefer descriptive naming `Subject_State_Expectation` (see `Settings_SaveAndLoad_MatchesOriginal`).
- Use `async Task` for asynchronous tests and await repository/file operations with `CancellationToken.None` to keep the UI thread clean.
- Use `using var` for disposable test helpers (file system, storage location) and delete temporary folders in `Dispose`.
- Reuse shared setup logic (helper classes/records) rather than duplicating `ActionConfiguration` builders.

### Other norms
- Naming constants with `static readonly` (e.g. `MovementTickInterval`, `DirectionalActions`) keeps them inline with their use case.
- When iterating enums, use `Enum.GetValues<T>()` and, if needed, `ToDictionary`/`ToArray` to avoid magic numbers.
- Keep logging consistent by prefacing messages with the feature (e.g. "Direction recognition: ...") and include context for confidence/hysteresis states.
- Avoid global state; instead, pass dependencies via constructor injection (view model, recognition engine, recorder, mouse controller).

## Cursor / Copilot rules
- No `.cursor/rules/` or `.cursorrules` directory was present when this file was generated; no cursor-specific instructions to copy.
- No `.github/copilot-instructions.md` file exists; nothing special is required for GitHub Copilot or similar assistants.

## Next steps for agents
- Prefer `dotnet test VocalJoystick.Tests/VocalJoystick.Tests.csproj --filter <method>` when iterating on recognition heuristics to keep runtimes short.
- Always summarize AppData/log changes in your PR description so downstream services know to revisit their cleanup scripts.
- Before merging, run `dotnet format VocalJoystick.sln` and `dotnet test VocalJoystick.sln` to ensure formatting and tests stay green.
