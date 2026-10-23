# SysScope

SysScope is a standalone Windows 10/11 x64 desktop app for system details and PC inventory. It reads the local computer by default, and it can query remote computers by name or IP. It sits alongside FolderScope, NetScope, and ADScope.

More tools: [coreydblackwell.com/apps](https://www.coreydblackwell.com/apps/).

Version 1.0.0. No installer. The published build is one self-contained executable.

## Requirements

- **To run:** Windows 10 or Windows 11, 64-bit. The published `SysScope.exe` includes the .NET runtime, so the target PC does not need .NET installed.
- **To build:** the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows, or the SDK on another OS with Windows targeting enabled (the project sets `EnableWindowsTargeting`).

## Run

On a Windows x64 PC, double-click `SysScope.exe`. Leave **Computer** blank and click **Connect** to read this PC. Type a computer name or IP address to read a remote machine. Check **Alternate credentials** and enter a user (for example `NORTHWIND\admin`) and password when the current account cannot query that machine.

```text
SysScope.exe
SysScope.exe --demo
```

`--demo` fills every tab with fictional Northwind machines so the window can be screenshotted without touching a real PC. Remote names that start with `OFFLINE-` or `BLOCKED-` / `DENIED-` show the same failure messages the live connectors produce.

## What it shows

- **Overview** — name, domain or workgroup, manufacturer, model, serial, BIOS, OS edition / version / build, install date, last boot and uptime, logged-on user, CPU, RAM, TPM, Secure Boot, BitLocker per volume, and IP/MAC per adapter.
- **Hardware** — CPU, memory modules, disks (SMART health from `MSFT_PhysicalDisk` when Windows will return it), volumes with free-space bars, GPUs, monitors (including serials from `WmiMonitorID`), network adapters, and laptop battery design versus full-charge capacity.
- **Software** — installed programs from the 64-bit, 32-bit, and current-user uninstall registry keys, plus Windows hotfixes from `Win32_QuickFixEngineering`.
- **Services** and **Startup** — read-only lists with a search box.
- **Inventory** — paste names or import a CSV, scan several computers at once, cancel a scan, and export the grid to CSV.

**File** exports the current computer as a styled HTML report or as JSON. Right-click a grid cell, or press Ctrl+C, to copy a value. The window is laid out for 100–175% DPI (Per-Monitor V2).

## Build

Requires the .NET 8 SDK.

```bash
dotnet test -c Release
dotnet publish src/SysScope.App/SysScope.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:EnableWindowsTargeting=true -o artifacts/publish
```

The executable is `artifacts/publish/SysScope.exe`. A zip of that executable plus `README.txt` is `SysScope-1.0.0-win-x64.zip`.

## Remote access

SysScope tries DCOM (WMI) first, then WinRM on HTTP 5985 and HTTPS 5986. When both are blocked it says which attempt failed and what to open:

- Windows Firewall: **Windows Management Instrumentation (DCOM-In)** and **WMI** rules, or **Windows Remote Management (HTTP-In)** on port 5985.
- DCOM: the account needs WMI **Remote Enable** on `root\cimv2` (and `root\cimv2\Security\MicrosoftVolumeEncryption` for BitLocker, `root\cimv2\Security\MicrosoftTpm` for TPM).
- WinRM: `winrm quickconfig` on the target. This app does not change TrustedHosts and does not bypass HTTPS certificate checks.

Local alternate credentials are ignored. The process runs as the signed-in user.

## Limits

- Remote software from `HKEY_CURRENT_USER` is the connecting account’s hive, not every profile on that PC.
- BitLocker, TPM, and `MSFT_PhysicalDisk` need extra rights. If Windows refuses the query, the field says it could not be read.
- Installed programs come from the uninstall registry keys, not the Microsoft Store package list. `Win32_Product` is not queried.
- `Win32_VideoController.AdapterRAM` is a 32-bit value, so very large GPU memory is left blank.
- Services and startup items are read-only. Startup enablement is shown only for Run keys that still exist.
- The single-file executable unpacks native libraries on first launch. That takes longer than later launches.
