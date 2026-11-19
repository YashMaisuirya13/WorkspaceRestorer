; ============================================================
; Workspace Session Restorer — Inno Setup Script
; Per-user install, no admin/UAC required.
; The app is published self-contained (bundles the .NET 8
; runtime) — no external runtime prerequisite.
; Build:  installer\build-installer.ps1  (or ISCC directly)
; ============================================================

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "Workspace Session Restorer"
#define MyAppPublisher "WorkspaceRestorer"
#define MyAppExeName "WorkspaceRestorer.exe"
#define MyAppMutex "WorkspaceRestorer_SingleInstance"
#define MyDataDir "WorkspaceRestorer"
; Keep AppId stable across releases so upgrades replace cleanly (escape leading { with {{ for Inno Setup)
#define MyAppId "{{D74079BE-070F-4BBA-8548-BB81EFE4F111}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductTextVersion={#MyAppVersion}

; Per-user install — no elevation
DefaultDirName={localappdata}\Programs\{#MyAppName}
PrivilegesRequired=lowest
DisableProgramGroupPage=yes

; x64 only (matches win-x64 publish)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=..\artifacts
OutputBaseFilename=WorkspaceRestorer-Setup-{#MyAppVersion}
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
UninstallFilesDir={app}\uninst

; Handle running tray instance during installation / upgrade
AppMutex={#MyAppMutex},Local\{#MyAppMutex}
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startup"; Description: "Start with &Windows"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcut (indexed by Windows Search)
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
      WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"; \
      Comment: "{#MyAppName}"

; Desktop shortcut (per user)
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
      WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"; \
      Comment: "{#MyAppName}"; Tasks: desktopicon

; Startup shortcut — auto-appended .lnk must match the filename
; StartupManager.IsEnabled() checks (WorkspaceRestorer.lnk)
Name: "{userstartup}\WorkspaceRestorer"; Filename: "{app}\{#MyAppExeName}"; \
      WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"; \
      Comment: "{#MyAppName}"; Tasks: startup

[Registry]
; Immediate Windows startup upon user logon (evaluated instantly by Windows without startup folder delay)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WorkspaceRestorer"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
; Launch after install — checked by default on Finish page
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName}"; \
         WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Remove app dir leftovers (data dir is NOT touched — preserved on uninstall)
Type: filesandordirs; Name: "{app}"

[Code]
// ------------------------------------------------------------
// Check if the application is running prior to uninstall.
// Inno Setup only checks AppMutex during installation by default;
// this ensures uninstallation doesn't fail due to locked files.
// ------------------------------------------------------------
function InitializeUninstall(): Boolean;
begin
  Result := True;
  while CheckForMutexes('{#MyAppMutex},Local\{#MyAppMutex}') do
  begin
    if MsgBox('{#MyAppName} is currently running in the background.' + #13#10#13#10 +
              'Please exit the application from the system tray before uninstalling.' + #13#10#13#10 +
              'Click Retry after closing the app, or Cancel to abort.',
              mbConfirmation, MB_RETRYCANCEL) <> IDRETRY then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// ------------------------------------------------------------
// On uninstall: data dir (%LOCALAPPDATA%\WorkspaceRestorer) is
// deliberately preserved (session.json, settings, logs).
// ------------------------------------------------------------
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Intentionally left empty — keep user data.
    // To also remove data on uninstall, add:
    //   DelTree(ExpandConstant('{localappdata}\{#MyDataDir}'), True, True, True);
  end;
end;
