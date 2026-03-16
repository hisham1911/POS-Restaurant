; TajerPro POS System - Inno Setup Script
; Developer password required: TajerPro@Installer2026
; MAC address binding active (license.key created on first install)

#define AppName "TajerPro"
#define AppVersion "1.0"
#define AppPublisher "TajerPro Software"
#define ServiceName "TajerProService"
#define ServiceDisplayName "TajerPro POS Backend"
#define SourceDir "C:\temp\tajerpro-src-win7-x64"
#define DbFileName "tajerpro.db"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=http://localhost:5243
AppSupportURL=http://localhost:5243
DefaultDirName=C:\{#AppName}
DirExistsWarning=no
DisableDirPage=yes
DisableProgramGroupPage=no
DefaultGroupName={#AppName}
OutputDir=C:\temp
OutputBaseFilename=TajerPro-Setup-Win7-x64
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
Password=TajerPro@Installer2026
Encryption=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayName={#AppName} POS
SetupLogging=yes

; Minimum Windows 10
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcuts"; GroupDescription: "Additional icons:"

[Files]
; All application files - SKIP the database file so existing client data is preserved
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "{#DbFileName},*.db,*.db.backup,license.key"

[Registry]
; Set ASPNETCORE_ENVIRONMENT=Production for the Windows Service
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\{#ServiceName}"; ValueType: multisz; ValueName: "Environment"; ValueData: "ASPNETCORE_ENVIRONMENT=Production"; Flags: uninsdeletevalue

[Icons]
; Desktop shortcuts
Name: "{commondesktop}\{#AppName} Printer Bridge"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; Comment: "KasserPro Thermal Printer Controller"; Tasks: desktopicon
Name: "{commondesktop}\{#AppName} POS"; Filename: "{app}\OpenBrowser.bat"; Comment: "Open KasserPro in browser"; Tasks: desktopicon

; Start menu
Name: "{group}\{#AppName} Printer Bridge"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; Comment: "KasserPro Thermal Printer Controller"
Name: "{group}\{#AppName} POS (Browser)"; Filename: "{app}\OpenBrowser.bat"; Comment: "Open KasserPro in browser"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Auto-start BridgeApp on Windows login (all users)
Name: "{commonstartup}\KasserPro BridgeApp"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; Comment: "KasserPro Thermal Printer Controller"

[Run]
; ---- Install Windows Service ----
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\KasserPro.API.exe"" DisplayName= ""{#ServiceDisplayName}"" start= auto type= own obj= LocalSystem"; Flags: runhidden waituntilterminated; StatusMsg: "Installing KasserPro backend service..."

; Set service description
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""KasserPro Point of Sale Backend Service"""; Flags: runhidden waituntilterminated

; Configure auto-restart on failure (3 restarts, then give up)
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} actions= restart/30000/restart/60000/restart/120000 reset= 86400"; Flags: runhidden waituntilterminated

; Start the service now
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated; StatusMsg: "Starting KasserPro backend service..."

; Open browser after all done (optional, user can skip)
Filename: "{app}\OpenBrowser.bat"; Flags: postinstall skipifsilent shellexec nowait; Description: "Open KasserPro in browser now"

[UninstallRun]
; Stop service first
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopService"

; Delete service
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteService"

[UninstallDelete]
; Keep the database on uninstall so data is not lost accidentally
; (admin can manually delete C:\KasserPro folder if they want full removal)
; Type: files; Name: "{app}\{#DbFileName}"

[Code]
var
  DbBackupPath: String;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

// Before files are copied: stop service and backup the database
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  DbPath: String;
begin
  if CurStep = ssInstall then
  begin
    DbPath := ExpandConstant('{app}\{#DbFileName}');
    DbBackupPath := ExpandConstant('{app}\kasserpro.db.update-backup');

    // Stop service before overwriting files
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);

    // Backup existing database
    if FileExists(DbPath) then
    begin
      CopyFile(DbPath, DbBackupPath, False);
    end;
  end;

  if CurStep = ssPostInstall then
  begin
    DbPath := ExpandConstant('{app}\{#DbFileName}');

    // Restore database if it was wiped during install (safety net)
    if (not FileExists(DbPath)) and FileExists(DbBackupPath) then
    begin
      CopyFile(DbBackupPath, DbPath, False);
    end;

    // Clean up backup file
    if FileExists(DbBackupPath) then
      DeleteFile(DbBackupPath);

    Sleep(2000);
  end;
end;

