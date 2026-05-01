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

## How to Build & Run

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

## Build for Distribution

### Windows installer (single .exe)
```powershell
dotnet publish -c Release -r win-x64 -f net8.0-windows --self-contained -o publish/win
```
Output: `publish/win/PC inspect beta.exe`

### macOS Apple Silicon (M1/M2/M3/M4)
```bash
dotnet publish -c Release -r osx-arm64 -f net8.0 --self-contained -o publish/mac-arm64
```

### macOS Intel
```bash
dotnet publish -c Release -r osx-x64 -f net8.0 --self-contained -o publish/mac-x64
```

### Linux x64
```bash
dotnet publish -c Release -r linux-x64 -f net8.0 --self-contained -o publish/linux
```

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
