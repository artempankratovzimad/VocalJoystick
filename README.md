# Vocal Joystick

This repository hosts the **Vocal Joystick** accessibility utility. The goal is to keep services lean while layering audio capture, recognition, input, and UI concerns.

## Solution layout
- `VocalJoystick.App` – WPF host with DI, wiring, and placeholder UX for modes/readouts.
- `VocalJoystick.Core` – domain models, enums, and service contracts.
- `VocalJoystick.Audio` – future audio capture helpers.
- `VocalJoystick.Recognition` – future feature extraction and recognition engines.
- `VocalJoystick.Input` – future Win32 mouse abstraction.
- `VocalJoystick.Infrastructure` – JSON persistence, logging, and stub implementations so the shell runs.
- `VocalJoystick.Tests` – unit-test project (MSTest) for core logic.

## Getting started
1. Restore packages: `dotnet restore VocalJoystick.sln`.
2. Build the solution: `dotnet build VocalJoystick.sln`.
3. Launch the app: `dotnet run --project VocalJoystick.App/VocalJoystick.App.csproj` (or open `VocalJoystick.sln` in Visual Studio).

All runtime files land in `%AppData%/VocalJoystick`, with logs in `Logs/` and JSON-backed persistence for profiles/settings.

## Next steps
1. Implement real audio capture via `NAudio` (`VocalJoystick.Audio`).
2. Sketch recognition pipeline in `VocalJoystick.Recognition` and hook the view model to live data.
3. Wire `VocalJoystick.Input` mouse controller to Win32 `SendInput` and add tests for command sequencing.
