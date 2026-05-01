# Inspectify Desktop Scanner

The official cross-platform hardware inspection tool for [Inspectify LapStore](https://inspectifylapstore.me) — Pakistan's first scan-verified laptop marketplace.

Runs on **Windows**, **macOS**, and **Linux**.

---

## What It Does

Scans your laptop's complete hardware and automatically creates a verified listing on Inspectify LapStore.

### Features
- **Hardware Scan** — CPU, RAM, storage, battery, GPU, display, BIOS, network
- **Sign In / Sign Up / Forgot Password** — full account flow
- **Ad Posting** — publish your scanned laptop directly to the marketplace
- **Photo Upload** — automatic compression to ≤100 KB before upload
- **Keyboard Test** — visual tester for every key
- **Touchpad Test** — coverage grid + click counter
- **PDF Export** — save the full scan as a polished PDF report
- **ML Price Prediction** (Windows only) — FastTree regression model trained on real listings
- **Heuristic Price Estimator** (all platforms) — fallback estimate based on hardware

---

## Easiest: Download Pre-Built Binary

Go to **[Releases](https://github.com/numl-f22-22271-hue/Inspectify-app/releases/latest)** and download:

- **Windows:** `InspectifyScanner-Windows.exe` — single file, double-click to run
- **macOS Apple Silicon:** `InspectifyScanner-macOS.dmg` — open, drag to Applications
- **macOS Intel:** `InspectifyScanner-macOS-Intel.zip`
- **Linux:** `InspectifyScanner-Linux.tar.gz` — extract and run

No .NET installation required — runtime is bundled.

---

## Build From Source

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Clone the repo
```bash
git clone https://github.com/numl-f22-22271-hue/Inspectify-app.git
cd Inspectify-app/PC\ inspect\ beta
```

### Run on Windows
```powershell
dotnet run -f net8.0-windows
```
Builds the WinForms version with full ML.NET price prediction.

### Run on macOS (Apple Silicon or Intel)
```bash
dotnet run -f net8.0
```
Builds the Avalonia version. Uses heuristic price estimator (ML.NET requires x64 only).

### Run on Linux
```bash
dotnet run -f net8.0
```
Same Avalonia version as macOS.

---

## Build a Single-File Distribution Binary

Each command below produces **one self-contained executable** with the .NET runtime bundled — users don't need to install anything.

### Windows (single .exe — ~80 MB)
```powershell
dotnet publish "PC inspect beta/PC inspect beta.csproj" -c Release -r win-x64 -f net8.0-windows --self-contained -o publish/win
```
Output: `publish/win/InspectifyScanner.exe` ← single file, just send this to users

### macOS Apple Silicon (M1/M2/M3/M4)
```bash
dotnet publish "PC inspect beta/PC inspect beta.csproj" -c Release -r osx-arm64 -f net8.0 --self-contained -o publish/mac-arm64
```
Output: `publish/mac-arm64/InspectifyScanner` ← single binary

### macOS Intel
```bash
dotnet publish "PC inspect beta/PC inspect beta.csproj" -c Release -r osx-x64 -f net8.0 --self-contained -o publish/mac-x64
```

### Linux x64
```bash
dotnet publish "PC inspect beta/PC inspect beta.csproj" -c Release -r linux-x64 -f net8.0 --self-contained -o publish/linux
```

---

## Automated Builds (GitHub Actions)

Push a version tag and GitHub will auto-build and publish a release for all platforms:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The `.github/workflows/build.yml` workflow will:
1. Build single-file Windows .exe on a Windows runner
2. Build .dmg for Apple Silicon + .zip for Intel Mac on a macOS runner
3. Build a Linux tar.gz on an Ubuntu runner
4. Create a GitHub Release with all four downloads attached

Users then go to your [Releases page](https://github.com/numl-f22-22271-hue/Inspectify-app/releases/latest) and download just the file they need — no zip extraction.

---

## Architecture

```
PC inspect beta/
├── Core/
│   ├── DiagnosticsEngine.cs        (Windows-only, WMI-based)
│   ├── PricePredictor.cs           (Windows-only, ML.NET)
│   ├── PriceEstimator.cs           (cross-platform heuristic)
│   ├── ReportExporter.cs           (cross-platform PDF via iText)
│   ├── DbHelper.cs                 (cross-platform MongoDB)
│   └── Platform/
│       ├── IHardwareScanner.cs     (cross-platform contract)
│       ├── PlatformDetector.cs     (auto-detects OS)
│       ├── WindowsHardwareScanner.cs   (uses WMI)
│       ├── MacOSHardwareScanner.cs     (uses sysctl, system_profiler, ioreg, pmset)
│       └── LinuxHardwareScanner.cs     (uses /proc, /sys, lspci, xrandr)
│
├── UI/
│   ├── Forms/                      (Windows WinForms — auto-excluded on Mac/Linux)
│   ├── Theme.cs                    (Windows-only)
│   ├── UIHelper.cs                 (Windows-only)
│   └── Avalonia/                   (Mac/Linux UI — auto-excluded on Windows)
│       ├── AvaloniaApp.cs
│       ├── MainWindow.cs           (scanner + post + tests + PDF)
│       ├── LoginWindow.cs
│       ├── SignUpWindow.cs
│       ├── ForgotPasswordWindow.cs
│       ├── AdPostWindow.cs
│       ├── KeyboardTestWindow.cs
│       └── TouchpadTestWindow.cs
│
├── Program.cs                      (auto-detects OS at startup)
└── PC inspect beta.csproj          (multi-target: net8.0-windows + net8.0)
```

---

## Platform Compatibility

| Feature | Windows | macOS | Linux |
|---------|---------|-------|-------|
| Hardware Scan | ✅ Full WMI | ✅ sysctl + system_profiler | ✅ /proc + lspci |
| Sign In/Up/Forgot | ✅ | ✅ | ✅ |
| Post Ad + Photos | ✅ | ✅ | ✅ |
| Keyboard Test | ✅ | ✅ | ✅ |
| Touchpad Test | ✅ | ✅ | ✅ |
| PDF Export | ✅ | ✅ | ✅ |
| MongoDB Upload | ✅ | ✅ | ✅ |
| ML Price Prediction | ✅ | ❌ (heuristic instead) | ❌ (heuristic instead) |

---

## Support

- **Website:** https://inspectifylapstore.me
- **Email:** inspectifylapstore@gmail.com
- **Phone:** +92 319 6172319

---

## License

© 2026 Inspectify LapStore. All rights reserved.
