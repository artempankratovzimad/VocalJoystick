# Vocal Joystick

Vocal Joystick is a Windows-only accessibility utility that lets users control the cursor through sustained vocal sounds. It uses a layered MVVM architecture powered by .NET 8, WPF, NAudio for audio capture/recording, and Win32 `SendInput` for mouse control. The focus is on local recognition (no cloud or speech-to-text) with clear diagnostics and lightweight configuration.

## Requirements
- Windows 10 or Windows 11
- .NET 8 runtime / SDK
- Microphone (for configuration and recognition)
- `%AppData%/VocalJoystick/` must be writable for persistence and logs

## Solution layout
- `VocalJoystick.App` – WPF host, commands, settings UI, and diagnostics panel.
- `VocalJoystick.Core` – enums, models, recognition contracts, and settings definitions.
- `VocalJoystick.Audio` – helpers for capturing audio buffers via NAudio (recording pipeline).
- `VocalJoystick.Recognition` – feature extraction, voice activity/pitch detection, directional recognition, and click detection.
- `VocalJoystick.Input` – Win32 mouse controller that drives relative movement plus clicks/double-clicks.
- `VocalJoystick.Infrastructure` – JSON persistence, logging, stub services, and the sample recorder.
- `VocalJoystick.Tests` – MSTest suite covering core settings, recognition, and profile flow.

## Build
1. `dotnet restore VocalJoystick.sln`
2. `dotnet build VocalJoystick.sln`

## Run
1. `dotnet run --project VocalJoystick.App/VocalJoystick.App.csproj` or open `VocalJoystick.sln` in Visual Studio and run the app project.
2. Allow microphone access when prompted; logs/JSON settings will be stored under `%AppData%/VocalJoystick/`.

## Configuration workflow
1. Click **Configuration Mode** to start capturing audio samples without moving the cursor.
2. Record 5–12 vowel samples for every directional action (MoveUp, MoveDown, MoveLeft, MoveRight) plus the click/double-click actions. The action tiles indicate recording status, last sample timestamp, and template stats.
3. As you record, the **Training debug** status (below the directional sample row) shows how many samples are captured for each direction and whether the MFCC/formant template is ready. Samples go through validation (voiced flag + RMS) before they contribute to the directional template.
4. Stop or delete samples as needed. The configuration status list will show when directional templates are pending/ready, and the bottom status message warns if directional templates are missing.
5. Once each direction has a template, click **Working Mode**. The app refuses to enable working mode until all four vowel templates exist so that the mouse moves only when the directional recognition pipeline is stable.
6. Use the **Movement speed** slider to control pixels-per-second when a direction is active (default: 320 px/s).
7. Use **Stop** to safely halt capture, movement loops, and reset the command state.

## Directional vowel recognition
- The training pipeline now extracts MFCCs, spectral centroid/spread, voiced ratios, power, and F1/F2 formants in addition to the existing RMS/pitch hooks. Directional candidates are scored via a classifier that compares real-time feature vectors against per-action prototypes built from the recorded samples, with pitch used only as an aux signal.
- Directional templates are persisted alongside the profile (profile version 2) so the training service can rehydrate sample counts on startup. Recorded samples now keep directional feature vectors in their metadata, enabling template recalculation if you delete samples later.
- Templates are built only after the minimum sample count (5 samples), and older samples are trimmed when you exceed 12 per action. The training service surfaces validation feedback (voiced/RMS) and exposes the template readiness per direction in the diagnostics line that follows the configuration heading.
- Temporal smoothing/hysteresis continues to guard activation: candidates must meet the configured confidence + hold threshold and remain active until the confidence falls below the hysteresis margin.

## Logging & diagnostics
- Logs live in `%AppData%/VocalJoystick/Logs/log-YYYYMMDD.txt` (UTC timestamps, INFO/WARN/ERROR tags).
- Direction recognition logs now include the trained template status, candidate confidence, and when the new vowel-based classifier flips directions, which helps trace confidence/hysteresis behavior and hold durations.
- The bottom-status banner still describes the current mode plus warnings about missing templates, but you can also inspect training/debug text for sample counts.

## Debug mode
The diagnostics panel at the bottom of the main window mirrors debug-mode output for direction recognition:
- **Direction** – current candidate direction (MoveUp/Down/Left/Right or “—”).
- **Status** – recognition status string such as “Candidate pending” or “Active”.
- **Confidence** – normalized confidence and direction hold progress.
- **Hold** – accumulated hold seconds against the configured hold threshold.
- **Voice**, **Pitch**, **RMS** – real-time audio features used for scoring.
- **Templates** – indicates whether directional templates are loaded.

This panel is color coded for warnings/errors via the status text below the mode controls. Status messages log to the file logger as Info/Warning/Error for auditability.

## Logging & diagnostics
- Logs live in `%AppData%/VocalJoystick/Logs/log-YYYYMMDD.txt` (UTC timestamps, INFO/WARN/ERROR tags).
- Direction recognition logs fire when the active direction changes, which helps trace confidence/hysteresis behavior.
- The bottom-status banner also describes the currently selected mode, warnings about missing templates, and errors encountered during capture.

## Testing
- `dotnet test VocalJoystick.sln` runs the MSTest suite, covering feature extraction, recognition heuristics, settings defaults, profile migration, and the new training/classifier logic (formants, MFCCs, directional sampling, temporal activation).

## Known limitations
- Recognition relies on pitch/energy heuristics; it does not use neural networks or external services.
- Microphone capture is a straightforward WASAPI stream; environmental noise or very short vowels may still drift templates.
- The configuration mode expects the user to record each action manually; there is no auto-profiling yet.

## Future improvements
1. Add smarter template verification (sample quality checks, visualization of spectral features).
2. Bring in real-time VST-style diagnostics or spectral overlays to vet recordings.
3. Introduce keyboard shortcuts or global hotkeys for mode switching.
4. Expand logging to optional telemetry/traces (still staying local) and support session exports.
