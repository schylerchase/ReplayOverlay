; Inno Setup Script for Replay Overlay
; Requires Inno Setup 6.0 or later
; Build before running: .\build.ps1 -Configuration Release

#define MyAppName "Replay Overlay"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "ReplayOverlay"
#define MyAppURL "https://github.com/schylerchase/replay-overlay-interactive"
#define MyAppExeName "ReplayOverlay.exe"
#define HostOutputDir "src\OBSReplay.Host\bin\Release\net8.0-windows"
#define OverlayOutputDir "build\overlay\bin\Release"
; NOTE: CMake outputs to build\overlay\bin\{Config}\ via RUNTIME_OUTPUT_DIRECTORY

[Setup]
AppId={{8F3B9A5C-7D2E-4F1A-B8C6-9E0D1F2A3B4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer_output
OutputBaseFilename=ReplayOverlay_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
CloseApplicationsFilter=ReplayOverlay.exe,OverlayRenderer.exe
; Add/Remove Programs metadata
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
; Update detection: allow reinstall/upgrade
UsePreviousAppDir=yes
UsePreviousGroup=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; C# Host (exe + all dependency DLLs + runtime config)
Source: "{#HostOutputDir}\ReplayOverlay.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#HostOutputDir}\ReplayOverlay.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#HostOutputDir}\ReplayOverlay.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#HostOutputDir}\ReplayOverlay.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#HostOutputDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
; C++ Overlay
Source: "{#OverlayOutputDir}\OverlayRenderer.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM ReplayOverlay.exe"; Flags: runhidden; RunOnceId: "KillHost"
Filename: "taskkill"; Parameters: "/F /IM OverlayRenderer.exe"; Flags: runhidden; RunOnceId: "KillOverlay"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\ReplayOverlay"
Type: filesandordirs; Name: "{app}"

[InstallDelete]
; Clean up old startup folder shortcuts
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"
Type: files; Name: "{userstartup}\Replay Overlay.lnk"
Type: files; Name: "{commonstartup}\{#MyAppName}.lnk"
; Clean up old OBS-named files from previous versions
Type: files; Name: "{app}\OBSReplay.exe"
Type: files; Name: "{app}\OBSReplay.dll"
Type: files; Name: "{app}\OBSReplay.deps.json"
Type: files; Name: "{app}\OBSReplay.runtimeconfig.json"
Type: files; Name: "{app}\OBSReplay.pdb"
Type: files; Name: "{app}\OBSReplayOverlay.exe"

[Registry]
; Clean up old registry startup entries
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ReplayOverlayInteractive"; Flags: deletevalue uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ReplayOverlay"; Flags: deletevalue uninsdeletevalue
; Add startup entry if selected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ReplayOverlay"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Code]
function IsDotNet8DesktopInstalled(): Boolean;
var
  Output: AnsiString;
  ResultCode: Integer;
  TempFile: String;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\dotnet_check.txt');
  // Run dotnet --list-runtimes and pipe to file
  if Exec('cmd.exe', '/C dotnet --list-runtimes > "' + TempFile + '" 2>&1',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(TempFile, Output) then
    begin
      // Check for Microsoft.WindowsDesktop.App 8.x
      if Pos('Microsoft.WindowsDesktop.App 8.', String(Output)) > 0 then
        Result := True;
    end;
    DeleteFile(TempFile);
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  // Kill running instances (current + old OBS-named versions)
  Exec('taskkill', '/F /IM ReplayOverlay.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/F /IM OverlayRenderer.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/F /IM OBSReplay.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/F /IM OBSReplayOverlay.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Check for .NET 8 Desktop Runtime before installation begins
  if not IsDotNet8DesktopInstalled() then
  begin
    if MsgBox('.NET 8 Desktop Runtime is required but not installed.' + Chr(13) + Chr(10) +
              Chr(13) + Chr(10) + 'Would you like to download it now?' + Chr(13) + Chr(10) +
              '(Choose x64 -> Run desktop apps -> .NET Desktop Runtime 8.x)' + Chr(13) + Chr(10) +
              Chr(13) + Chr(10) + 'Installation will abort. Please re-run setup after installing the runtime.',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end
    else
    begin
      MsgBox('Replay Overlay requires .NET 8 Desktop Runtime to run.' + Chr(13) + Chr(10) +
             'Please install it from: https://dotnet.microsoft.com/download/dotnet/8.0',
             mbInformation, MB_OK);
    end;
    Result := False;
    exit;
  end;

  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ReplayOverlay');
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OBSReplay');  // legacy cleanup
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ReplayOverlay');
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ReplayOverlayInteractive');
  end;
end;
