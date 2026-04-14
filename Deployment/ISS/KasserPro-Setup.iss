; KasserPro POS System - Inno Setup Script
; Developer password required: KasserPro@Installer2026
; MAC address binding active (license.key created on first install)

#define AppName "TajerPro"
#define AppVersion "1.0"
#define AppPublisher "TajerPro Software"
#define ServiceName "KasserProService"
#define ServiceDisplayName "TajerPro POS Backend"
#define SourceDir "C:\temp\kasserpro-src"
#define DbFileName "kasserpro.db"
#define DeploymentRoot "f:\POS\Deployment"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=http://localhost:5243
AppSupportURL=http://localhost:5243
DefaultDirName=C:\KasserPro
DirExistsWarning=no
DisableDirPage=yes
DisableProgramGroupPage=no
DefaultGroupName={#AppName}
OutputDir={#DeploymentRoot}\Installers
OutputBaseFilename=TajerPro-Setup
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
Password=KasserPro@Installer2026
Encryption=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayName={#AppName} POS
SetupLogging=yes
SetupIconFile={#DeploymentRoot}\Icons\kasserpro.ico
UninstallDisplayIcon={app}\kasserpro.ico

; Minimum Windows 10
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcuts"; GroupDescription: "Additional icons:"

[Files]
; All application files - SKIP the database file so existing client data is preserved
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "{#DbFileName},*.db,*.db.backup,license.key"
; Service control scripts
Source: "{#DeploymentRoot}\Scripts\StartKasserPro.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DeploymentRoot}\Scripts\StopKasserPro.bat"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; Set ASPNETCORE_ENVIRONMENT=Production for the Windows Service
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\{#ServiceName}"; ValueType: multisz; ValueName: "Environment"; ValueData: "ASPNETCORE_ENVIRONMENT=Production"; Flags: uninsdeletevalue

[Icons]
; Desktop shortcuts
Name: "{commondesktop}\{#AppName} Printer Bridge"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "TajerPro Thermal Printer Controller"; Tasks: desktopicon
Name: "{commondesktop}\{#AppName} POS"; Filename: "{app}\KasserPro.url"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Open TajerPro in browser"; Tasks: desktopicon
Name: "{commondesktop}\{#AppName} - Start Service"; Filename: "{app}\StartKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Start TajerPro Backend Service"; Tasks: desktopicon
Name: "{commondesktop}\{#AppName} - Stop Service"; Filename: "{app}\StopKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Stop TajerPro Backend Service"; Tasks: desktopicon

; Start menu
Name: "{group}\{#AppName} Printer Bridge"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "TajerPro Thermal Printer Controller"
Name: "{group}\{#AppName} POS (Browser)"; Filename: "{app}\KasserPro.url"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Open TajerPro in browser"
Name: "{group}\{#AppName} - Start Service"; Filename: "{app}\StartKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Start TajerPro Backend Service"
Name: "{group}\{#AppName} - Stop Service"; Filename: "{app}\StopKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Stop TajerPro Backend Service"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Auto-start BridgeApp on Windows login (all users)
Name: "{commonstartup}\{#AppName} BridgeApp"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "TajerPro Thermal Printer Controller"

[Run]
; ---- Install Windows Service ----
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\KasserPro.API.exe"" DisplayName= ""{#ServiceDisplayName}"" start= auto type= own obj= LocalSystem"; Flags: runhidden waituntilterminated; StatusMsg: "Installing TajerPro backend service..."

; Set service description
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""TajerPro Point of Sale Backend Service"""; Flags: runhidden waituntilterminated

; Configure auto-restart on failure (3 restarts, then give up)
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} actions= restart/30000/restart/60000/restart/120000 reset= 86400"; Flags: runhidden waituntilterminated

; Start the service now
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated; StatusMsg: "Starting TajerPro backend service..."

; Open browser after all done (optional, user can skip)
Filename: "{app}\KasserPro.url"; Flags: postinstall skipifsilent shellexec nowait; Description: "Open TajerPro in browser now"

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
  FreshInstallMode: Boolean;

function GenerateRandomPassword: String;
var
  Chars: String;
  PwdLen: Integer;
  i, idx: Integer;
begin
  Chars := 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%';
  PwdLen := 32;
  Result := '';
  for i := 1 to PwdLen do
  begin
    idx := Random(62) + 1;
    Result := Result + Copy(Chars, idx, 1);
  end;
end;

procedure CreateDbPassword;
var
  SecretsDir: String;
  FilePath: String;
  Password: String;
  ResultCode: Integer;
begin
  SecretsDir := ExpandConstant('{commonappdata}') + '\KasserPro\secrets';
  FilePath := SecretsDir + '\svc.dat';

  if FileExists(FilePath) then
    Exit;

  if not DirExists(SecretsDir) then
    ForceDirectories(SecretsDir);

  Password := GenerateRandomPassword;
  SaveStringToFile(FilePath, Password, False);

  Exec('icacls.exe',
    '"' + FilePath + '" /inheritance:r /grant "SYSTEM:(R)" /grant "Administrators:(R)"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function IsAlreadyInstalled(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\{#ServiceName}')
         or FileExists('C:\KasserPro\{#DbFileName}');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  FreshInstallMode := False;

  if not IsAlreadyInstalled() then
    Exit;

  // App already installed - offer choice
  case MsgBox(
    'TajerPro is already installed on this computer.' + #13#10 + #13#10 +
    'Choose what to do:' + #13#10 + #13#10 +
    '  [Yes]   Update - keep all your existing data' + #13#10 +
    '  [No]    Fresh Install - DELETE all data and start over',
    mbConfirmation, MB_YESNO) of

    IDYES:
    begin
      // Update mode - keep data (default)
      FreshInstallMode := False;
    end;

    IDNO:
    begin
      // Must confirm twice before wiping data
      if MsgBox(
        'WARNING: Fresh Install will permanently delete:' + #13#10 +
        '  - All sales history and receipts' + #13#10 +
        '  - All products and categories' + #13#10 +
        '  - All customer and supplier records' + #13#10 +
        '  - All expenses and cash register data' + #13#10 + #13#10 +
        'This action CANNOT be undone!' + #13#10 + #13#10 +
        'Click Yes to confirm deletion, or No to cancel.',
        mbError, MB_YESNO) = IDYES then
      begin
        FreshInstallMode := True;
      end else begin
        Result := False; // User cancelled - abort
      end;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  DbPath, UrlFile, UrlContent: String;
begin
  if CurStep = ssInstall then
  begin
    CreateDbPassword();

    DbPath       := ExpandConstant('{app}\{#DbFileName}');
    DbBackupPath := ExpandConstant('{app}\kasserpro.db.update-backup');

    Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);

    if FreshInstallMode then
    begin
      // Fresh install - wipe existing database
      if FileExists(DbPath) then DeleteFile(DbPath);
    end else begin
      // Update - backup database before files are overwritten
      if FileExists(DbPath) then CopyFile(DbPath, DbBackupPath, False);
    end;
  end;

  if CurStep = ssPostInstall then
  begin
    DbPath := ExpandConstant('{app}\{#DbFileName}');

    if not FreshInstallMode then
    begin
      if (not FileExists(DbPath)) and FileExists(DbBackupPath) then
        CopyFile(DbBackupPath, DbPath, False);
      if FileExists(DbBackupPath) then DeleteFile(DbBackupPath);
    end;

    // Write KasserPro.url with correct icon path
    UrlFile    := ExpandConstant('{app}\KasserPro.url');
    UrlContent := '[InternetShortcut]' + #13#10 +
                  'URL=http://localhost:5243' + #13#10 +
                  'IconFile=' + ExpandConstant('{app}\kasserpro.ico') + #13#10 +
                  'IconIndex=0' + #13#10;
    SaveStringToFile(UrlFile, UrlContent, False);

    // Refresh icon cache so updated shortcut icons are applied on upgrades.
    Exec(ExpandConstant('{sys}\ie4uinit.exe'), '-ClearIconCache', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\ie4uinit.exe'), '-show', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    Sleep(2000);
  end;
end;
