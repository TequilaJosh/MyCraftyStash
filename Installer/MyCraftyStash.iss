; ============================================================
;  My Crafty Stash - Inno Setup Script
;  Version is read automatically from the compiled exe (publish.ps1
;  bumps <FileVersion> in the .csproj before each build).
;
;  NOTE: Inno Setup creates a .exe installer (not .msi).
;
;  Per-user install: targets %LOCALAPPDATA%\Programs\My Crafty Stash so
;  the app folder is fully writable by the running user (no admin/UAC).
;  Both SQLite databases (inventory.db, settings.db) and the Logs\ folder
;  live next to the .exe.
;
;  Prerequisites:
;    1. Publish the app first:
;         dotnet publish -c Release -r win-x64 --self-contained true ^
;             -p:PublishSingleFile=true
;    2. Download + install Inno Setup 6: https://jrsoftware.org/isinfo.php
;    3. Compile this script (or use Installer\publish.ps1 to do everything,
;       including auto-version-bump and copying setup.exe + version.txt to
;       the network share for the in-app updater to find):
;         "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\MyCraftyStash.iss
; ============================================================

#define AppName      "My Crafty Stash"
#define AppPublisher "My Crafty Stash"
#define AppURL       ""
#define AppExeName   "MyCraftyStash.exe"
#define PublishDir   "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
#define AppVersion   GetVersionNumbersString(PublishDir + "\" + AppExeName)

[Setup]
; Stable AppId - keep across versions so upgrades replace the existing install
; instead of going side-by-side. Changing this orphans old installs.
AppId={{F3B7E1D2-9C84-4A6B-8E5F-1A0B7C2D9E3F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (C) My Crafty Stash 2026

; Per-user install to %LOCALAPPDATA%\Programs\My Crafty Stash. {userpf} is the
; per-user equivalent of {autopf}; together with PrivilegesRequired=lowest
; this gives a fully writable install folder without UAC.
DefaultDirName={userpf}\{#AppName}
DisableProgramGroupPage=yes

; Require Windows 10 1903+ (matches net8.0-windows10.0.19041.0)
MinVersion=10.0.19041

; Per-user — no admin elevation needed.
PrivilegesRequired=lowest

; Output: writes MyCraftyStash_Setup_X.Y.Z.W.exe to Installer\output\.
; publish.ps1 copies it to the network share as \\...\Installation\setup.exe.
OutputDir=output
OutputBaseFilename=MyCraftyStash_Setup_{#AppVersion}
SetupIconFile=..\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Appearance
WizardStyle=modern
WizardSizePercent=110
WizardImageFile=compiler:WizModernImage.bmp
WizardSmallImageFile=compiler:WizModernSmallImage.bmp

; If the app is running when the installer launches, prompt to close it
; (and re-launch after) instead of failing with "file in use".
CloseApplications=force
RestartApplications=yes
RestartIfNeededByRun=no

VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut";          GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupicon"; Description: "Launch {#AppName} when Windows &starts"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Main executable (single-file publish)
Source: "{#PublishDir}\{#AppExeName}";                   DestDir: "{app}"; Flags: ignoreversion

; WPF native DLLs - required for WPF to start (cannot be bundled in single-file)
Source: "{#PublishDir}\wpfgfx_cor3.dll";                 DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\PresentationNative_cor3.dll";     DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\D3DCompiler_47_cor3.dll";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\PenImc_cor3.dll";                 DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\vcruntime140_cor3.dll";           DestDir: "{app}"; Flags: ignoreversion

; SQLite native interop (EF Core Sqlite provider)
Source: "{#PublishDir}\e_sqlite3.dll";                   DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Magick.NET native (image processing)
Source: "{#PublishDir}\Magick.Native-Q8-x64.dll";        DestDir: "{app}"; Flags: ignoreversion

; App icon (used by shortcuts)
Source: "..\icon.ico";                                   DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Sweep stale leftovers from earlier installs.
Type: filesandordirs; Name: "{app}\tessdata"
Type: filesandordirs; Name: "{app}\x64"
Type: filesandordirs; Name: "{app}\x86"
; Old SQL Server connectivity DLL (no longer used after move to SQLite).
Type: files;          Name: "{app}\Microsoft.Data.SqlClient.SNI.dll"
; Old appsettings.json (connection strings no longer needed).
Type: files;          Name: "{app}\appsettings.json"
; Old Config\ folder (lists now live in settings.db).
Type: filesandordirs; Name: "{app}\Config"

[Icons]
; Start menu (per-user)
Name: "{userprograms}\{#AppName}";              Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{userprograms}\Uninstall {#AppName}";    Filename: "{uninstallexe}"

; Desktop (optional task)
Name: "{userdesktop}\{#AppName}";               Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

; Startup (optional task)
Name: "{userstartup}\{#AppName}";               Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: startupicon

[Run]
; Offer to launch the app after install
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Leave user data alone on uninstall by default — inventory.db, settings.db,
; and the Logs folder all live in {app} but the user may want them preserved
; across reinstalls. Uncomment the lines below to wipe everything on uninstall.
; Type: files;          Name: "{app}\inventory.db"
; Type: files;          Name: "{app}\settings.db"
; Type: filesandordirs; Name: "{app}\Logs"
