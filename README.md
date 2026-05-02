# ElevenLabs SAPI5 TTS Engine

.NET Framework 4.8 COM-visible SAPI5 TTS engine that registers ElevenLabs voices as Windows Speech API voice tokens.

## Projects

- `ElevenLabsTtsEngine`: COM server implementing `ISpTTSEngine` and `ISpObjectWithToken`.
- `ElevenLabsTtsInstaller`: elevated console installer for COM registration and SAPI voice token refresh.

## Build

```powershell
dotnet build .\ElevenLabsTtsEngine.sln -c Release --configfile .\NuGet.config
```

## Install

Run from an elevated Administrator PowerShell:

```powershell
.\ElevenLabsTtsInstaller\bin\Release\net48\ElevenLabsTtsInstaller.exe install
```

The installer copies the runtime to `C:\Program Files\ElevenLabs SAPI TTS`, prompts for the user's ElevenLabs API key if one is not configured, stores it DPAPI-encrypted in `%APPDATA%\ElevenLabsTTS\config.json`, runs both 64-bit and 32-bit `RegAsm.exe`, then registers each ElevenLabs voice under both SAPI registry views.

Refresh voice tokens:

```powershell
.\ElevenLabsTtsInstaller\bin\Release\net48\ElevenLabsTtsInstaller.exe refresh
```

Uninstall:

```powershell
.\ElevenLabsTtsInstaller\bin\Release\net48\ElevenLabsTtsInstaller.exe uninstall
```

## Smoke Test

```powershell
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synth.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name }
```

The SAPI DDI interface definitions were checked against the local Windows SDK `sapiddk.idl` / `sapi.idl`. Note that `ISpTTSEngine` is `A74D7C8E-4CC5-4F2F-A6EB-804DEE18500E` in this SDK.
