SysScope 1.0.0
PC inventory and system details for local and remote machines

Run
  Double-click SysScope.exe. No install is required.
  Leave Computer blank and click Connect to read this PC.
  Type a computer name or IP to read a remote PC.
  Check Alternate credentials when the current account is not enough
  (user form: DOMAIN\user).

Tabs
  Overview    Identity, OS, boot, TPM, Secure Boot, BitLocker, addresses
  Hardware    CPU, memory, disks, volumes, GPUs, monitors, network, battery
  Software    Installed programs (64-bit, 32-bit, and current user) and hotfixes
  Services    Read-only service list
  Startup     Read-only startup items
  Inventory   Paste names or import a CSV, Scan, then Export CSV

Export
  Export HTML and Export JSON save a report for the computer on screen.
  Inventory, Export CSV writes one row per machine (UTF-8, Excel-friendly).
  Right-click a cell, or press Ctrl+C, to copy a value.

Remote computers
  SysScope tries WMI over DCOM, then WinRM (HTTP 5985, then HTTPS 5986).
  If both fail, the message names the block and what to allow:
  firewall rules for WMI/DCOM or WinRM, WMI Remote Enable, and WinRM
  quickconfig. This program does not change TrustedHosts.

Demo
  SysScope.exe --demo
  Fills the window with fictional data. Nothing is read from the PC.

Notes
  Services and startup items cannot be changed from here.
  BitLocker, TPM, and disk health appear only when Windows allows the query.
  The first launch unpacks files and can take longer than later launches.
