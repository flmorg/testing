#define MyAppName "Cleanuparr"
#define MyAppVersion GetEnv("APP_VERSION")
#define MyAppPublisher "Cleanuparr Team"
#define MyAppURL "https://github.com/Cleanuparr/Cleanuparr"
#define MyAppExeName "Cleanuparr.exe"
#define MyServiceName "Cleanuparr"

[Setup]
AppId={{E8B2C9D4-6F87-4E42-B5C3-29E121D4BDFF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=.\installer
OutputBaseFilename=Cleanuparr_Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=Logo\favicon.ico
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1
Name: "installservice"; Description: "Install as Windows Service (Recommended)"; GroupDescription: "Service Installation"; Flags: checkedonce

[Files]
Source: "dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Logo\favicon.ico"; DestDir: "{app}"; Flags: ignoreversion
; Note: Application will create its own configuration files

[Dirs]
Name: "{app}\config"; Permissions: everyone-full

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\favicon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\favicon.ico"; Tasks: desktopicon

[Run]
; For fresh installs only - create and start service
Filename: "{sys}\sc.exe"; Parameters: "create ""{#MyServiceName}"" binPath= ""\""{app}\{#MyAppExeName}\"""" DisplayName= ""{#MyAppName}"" start= auto"; Tasks: installservice; Flags: runhidden; Check: not ServiceExists('{#MyServiceName}')
Filename: "{sys}\sc.exe"; Parameters: "description ""{#MyServiceName}"" ""Cleanuparr download management service"""; Tasks: installservice; Flags: runhidden; Check: not ServiceExists('{#MyServiceName}')

; For updates - stop service if running, wait for complete shutdown, then restart
Filename: "{sys}\sc.exe"; Parameters: "stop ""{#MyServiceName}"""; Flags: runhidden; Check: ServiceExists('{#MyServiceName}') and IsServiceRunning('{#MyServiceName}') and IsTaskSelected('installservice')
Filename: "{sys}\sc.exe"; Parameters: "start ""{#MyServiceName}"""; Tasks: installservice; Flags: runhidden; Check: ServiceExists('{#MyServiceName}') and ServiceExistedBefore

; For fresh installs - start the newly created service
Filename: "{sys}\sc.exe"; Parameters: "start ""{#MyServiceName}"""; Tasks: installservice; Flags: runhidden; Check: not ServiceExistedBefore

; Open web interface (only if service is selected)
Filename: "http://localhost:11011"; Description: "Open Cleanuparr Web Interface"; Flags: postinstall shellexec nowait; Check: IsTaskSelected('installservice')

; Run directly (if not installed as service)
Filename: "{app}\{#MyAppExeName}"; Description: "Run {#MyAppName} Application"; Flags: nowait postinstall skipifsilent; Check: not IsTaskSelected('installservice')

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop ""{#MyServiceName}"""; Flags: runhidden; Check: ServiceExists('{#MyServiceName}')
Filename: "{sys}\timeout.exe"; Parameters: "/t 5"; Flags: runhidden; Check: ServiceExists('{#MyServiceName}')
Filename: "{sys}\sc.exe"; Parameters: "delete ""{#MyServiceName}"""; Flags: runhidden; Check: ServiceExists('{#MyServiceName}')

[Code]
var
  ServiceExistedBefore: Boolean;

procedure CreateConfigDirs;
begin
  // Create config directory - application will create its own config files
  ForceDirectories(ExpandConstant('{app}\config'));
end;

function ServiceExists(ServiceName: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query "' + ServiceName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsServiceRunning(ServiceName: string): Boolean;
var
  ResultCode: Integer;
  Output: AnsiString;
  OutputFile: string;
begin
  Result := False;
  OutputFile := ExpandConstant('{tmp}\service_status.txt');
  
  // Query service status and capture output
  if Exec(ExpandConstant('{sys}\sc.exe'), 'query "' + ServiceName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
  begin
    // Use PowerShell to get service status more reliably
    if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), 
            '-Command "& {(Get-Service -Name ''' + ServiceName + ''' -ErrorAction SilentlyContinue).Status}"', 
            '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      // If we can't determine status precisely, assume it might be running to be safe
      Result := True;
    end;
  end;
end;

function WaitForServiceStop(ServiceName: string): Boolean;
var
  Counter: Integer;
  ResultCode: Integer;
  StatusOutput: AnsiString;
  TempFile: string;
begin
  Result := True;
  Counter := 0;
  TempFile := ExpandConstant('{tmp}\service_check.txt');
  
  // Wait up to 30 seconds for service to stop
  while Counter < 30 do
  begin
    // Check service status using PowerShell for more reliable output
    if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), 
            '-Command "& {try { $s = Get-Service -Name ''' + ServiceName + ''' -ErrorAction Stop; $s.Status } catch { ''NotFound'' }}" > "' + TempFile + '"', 
            '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if LoadStringFromFile(TempFile, StatusOutput) then
      begin
        // If service is stopped or not found, we're good
        if (Pos('Stopped', StatusOutput) > 0) or (Pos('NotFound', StatusOutput) > 0) then
        begin
          DeleteFile(TempFile);
          Exit;
        end;
      end;
    end;
    
    Sleep(1000);
    Counter := Counter + 1;
  end;
  
  // Cleanup temp file
  DeleteFile(TempFile);
  
  // If we get here, service didn't stop in time
  if Counter >= 30 then
  begin
    MsgBox('Warning: Service took longer than expected to stop. Installation will continue but the service may need to be restarted manually.', 
           mbInformation, MB_OK);
    Result := False;
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  // Remember if service existed before installation
  ServiceExistedBefore := ServiceExists('{#MyServiceName}');
  
  // Only stop service if it exists and is running
  if ServiceExistedBefore and IsServiceRunning('{#MyServiceName}') then
  begin
    if MsgBox('Cleanuparr service is currently running and needs to be stopped for the installation. Continue?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec(ExpandConstant('{sys}\sc.exe'), 'stop "{#MyServiceName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      if not WaitForServiceStop('{#MyServiceName}') then
      begin
        // Service didn't stop properly, but continue anyway
        Log('Warning: Service did not stop cleanly, continuing with installation');
      end;
    end
    else
    begin
      Result := False;
      Exit;
    end;
  end;
  
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  if ServiceExists('{#MyServiceName}') then
  begin
    if MsgBox('Cleanuparr service will be stopped and removed. Continue with uninstallation?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec(ExpandConstant('{sys}\sc.exe'), 'stop "{#MyServiceName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      WaitForServiceStop('{#MyServiceName}');
    end
    else
    begin
      Result := False;
      Exit;
    end;
  end;
  Result := True;
end;