; KasserPro POS System - Inno Setup Script (Win7 x86 edition)
; Developer password required: KasserPro@Installer2026
; MAC address binding active (license.key created on first install)

#define AppName "KasserPro"
#define AppVersion "1.0"
#define AppPublisher "KasserPro Software"
#define ServiceName "KasserProService"
#define ServiceDisplayName "KasserPro POS Backend"
#define SourceDir "C:\temp\kasserpro-src-win7-x86"
#define DbFileName "kasserpro.db"
#define DeploymentRoot "f:\POS\Deployment"

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
OutputDir={#DeploymentRoot}\Installers
OutputBaseFilename=KasserPro-Setup-Win7-x86
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
Password=KasserPro@Installer2026
Encryption=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayName={#AppName} POS
SetupLogging=yes

; Windows 7 SP1 minimum (6.1.7601)
MinVersion=6.1.7601

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
Name: "{commondesktop}\{#AppName} Printer Bridge"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; Comment: "KasserPro Thermal Printer Controller"; Tasks: desktopicon
Name: "{commondesktop}\{#AppName} POS"; Filename: "{app}\KasserPro.url"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Open KasserPro in browser"; Tasks: desktopicon
Name: "{commondesktop}\{#AppName} - Start Service"; Filename: "{app}\StartKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Start KasserPro Backend Service"; Tasks: desktopicon
Name: "{commondesktop}\{#AppName} - Stop Service"; Filename: "{app}\StopKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Stop KasserPro Backend Service"; Tasks: desktopicon
Name: "{group}\{#AppName} Printer Bridge"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; Comment: "KasserPro Thermal Printer Controller"
Name: "{group}\{#AppName} POS (Browser)"; Filename: "{app}\KasserPro.url"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Open KasserPro in browser"
Name: "{group}\{#AppName} - Start Service"; Filename: "{app}\StartKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Start KasserPro Backend Service"
Name: "{group}\{#AppName} - Stop Service"; Filename: "{app}\StopKasserPro.bat"; IconFilename: "{app}\kasserpro.ico"; IconIndex: 0; Comment: "Stop KasserPro Backend Service"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commonstartup}\KasserPro BridgeApp"; Filename: "{app}\bridge\KasserPro.BridgeApp.exe"; Comment: "KasserPro Thermal Printer Controller"

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\KasserPro.API.exe"" DisplayName= ""{#ServiceDisplayName}"" start= auto type= own obj= LocalSystem"; Flags: runhidden waituntilterminated; StatusMsg: "Installing KasserPro backend service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""KasserPro Point of Sale Backend Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} actions= restart/30000/restart/60000/restart/120000 reset= 86400"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated; StatusMsg: "Starting KasserPro backend service..."
Filename: "{app}\KasserPro.url"; Flags: postinstall skipifsilent shellexec nowait; Description: "Open KasserPro in browser now"

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteService"

[UninstallDelete]
; Keep database on uninstall

[Code]
var
  DbBackupPath: String;
  FreshInstallMode: Boolean;

function IsWin7(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major = 6) and (Version.Minor = 1);
end;

function IsSSUInstalled(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Updates\Windows 7\SP2\KB4490628');
end;

function IsSHA2Installed(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Updates\Windows 7\SP2\KB4474419');
end;

function IsAlreadyInstalled(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\{#ServiceName}')
         or FileExists('C:\{#AppName}\{#DbFileName}');
end;

function InitializeSetup(): Boolean;
var
  MissingPatches: String;
begin
  Result := True;
  FreshInstallMode := False;

  if IsWin7() then
  begin
    MissingPatches := '';
    if not IsSSUInstalled() then
      MissingPatches := MissingPatches + '- KB4490628 (Servicing Stack Update)' + #13#10;
    if not IsSHA2Installed() then
      MissingPatches := MissingPatches + '- KB4474419 (SHA-2 Code Signing Support)' + #13#10;
    if MissingPatches <> '' then
    begin
      if MsgBox(
        'KasserPro requires the following Windows 7 updates to run:' + #13#10 + #13#10 +
        MissingPatches + #13#10 +
        'Run "Win7-Prerequisites.bat" from the installer folder first.' + #13#10 + #13#10 +
        'Install order: KB4490628 first, then KB4474419, then restart.' + #13#10 + #13#10 +
        'Continue anyway? (App will NOT start without these updates)',
        mbConfirmation, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
  end;

  if not IsAlreadyInstalled() then
    Exit;

  case MsgBox(
    'KasserPro is already installed on this computer.' + #13#10 + #13#10 +
    'Choose what to do:' + #13#10 + #13#10 +
    '  [Yes]   Update — keep all your existing data' + #13#10 +
    '  [No]    Fresh Install — DELETE all data and start over',
    mbConfirmation, MB_YESNO) of

    IDYES:
    begin
      FreshInstallMode := False;
    end;

    IDNO:
    begin
      if MsgBox(
        'WARNING: Fresh Install will permanently delete:' + #13#10 +
        '  • All sales history and receipts' + #13#10 +
        '  • All products and categories' + #13#10 +
        '  • All customer and supplier records' + #13#10 +
        '  • All expenses and cash register data' + #13#10 + #13#10 +
        'This action CANNOT be undone!' + #13#10 + #13#10 +
        'Click Yes to confirm deletion, or No to cancel.',
        mbError, MB_YESNO) = IDYES then
      begin
        FreshInstallMode := True;
      end else begin
        Result := False;
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
    DbPath       := ExpandConstant('{app}\{#DbFileName}');
    DbBackupPath := ExpandConstant('{app}\kasserpro.db.update-backup');

    Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);

    if FreshInstallMode then
    begin
      if FileExists(DbPath) then DeleteFile(DbPath);
    end else begin
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

    UrlFile    := ExpandConstant('{app}\KasserPro.url');
    UrlContent := '[InternetShortcut]' + #13#10 +
                  'URL=http://localhost:5243' + #13#10 +
                  'IconFile=' + ExpandConstant('{app}\kasserpro.ico') + #13#10 +
                  'IconIndex=0' + #13#10;
    SaveStringToFile(UrlFile, UrlContent, False);

    if IsWin7() then
      Exec(ExpandConstant('{sys}\ie4uinit.exe'), '-ClearIconCache', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    else
      Exec(ExpandConstant('{sys}\ie4uinit.exe'), '-show', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    Sleep(2000);
  end;
end;
