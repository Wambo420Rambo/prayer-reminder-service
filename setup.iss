#define MyAppName "Prayer Reminder Service"
#define MyAppVersion "1.0"
#define MyAppPublisher "AdhanService"
#define MyAppExeName "PrayerTimesService.exe"
#define ServiceName "Prayer Reminder Service"

[Setup]
AppId={{B4F2E3A1-8C7D-4E6F-9A0B-1D2C3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
PrivilegesRequired=admin
OutputBaseFilename=PrayerReminder-Setup
OutputDir=.
SolidCompression=yes
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=no
DisableFinishedPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Files]
Source: "bin\Release\net8.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "sc.exe"; Parameters: "create ""{#ServiceName}"" binPath= ""{app}\{#MyAppExeName}"" start= auto"; Flags: runhidden; StatusMsg: "Installing Windows service..."
Filename: "sc.exe"; Parameters: "start ""{#ServiceName}"""; Flags: runhidden; StatusMsg: "Starting service..."

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ""{#ServiceName}"""; Flags: runhidden; RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete ""{#ServiceName}"""; Flags: runhidden; RunOnceId: "DeleteService"

[Code]
var
  UrlPage: TInputQueryWizardPage;
  UserUrl: string;
  UrlLink: TNewStaticText;

function ServiceExists: Boolean;
var
  ResultCode: Integer;
begin
  Exec('sc.exe', ExpandConstant('query "{#ServiceName}"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := ResultCode = 0;
end;

function ValidateUrl(Url: string): Integer;
var
  ResultCode: Integer;
  ScriptFile: string;
begin
  ScriptFile := ExpandConstant('{tmp}\validate.ps1');
  SaveStringToFile(ScriptFile,
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    'try { $r = Invoke-WebRequest -Uri "' + Url + '" -UseBasicParsing -TimeoutSec 15; ' +
    'if ($r.Content -match ''today\.(fajr|shuruk|dhuhr|asr|maghrib|ishaa)\.'') { exit 0 } ' +
    'else { exit 1 } } catch { exit 2 }', False);

  if Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -File "' + ScriptFile + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode
  else
    Result := 2;
end;

function NextButtonClick(PageId: Integer): Boolean;
var
  ValidationResult: Integer;
  ErrorMsg: string;
begin
  Result := True;

  if PageId = UrlPage.ID then
  begin
    UserUrl := Trim(UrlPage.Values[0]);

    if (UserUrl = '') or ((Pos('http://', UserUrl) <> 1) and (Pos('https://', UserUrl) <> 1)) then
    begin
      MsgBox('Please enter a valid URL starting with http:// or https://', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    ValidationResult := ValidateUrl(UserUrl);

    case ValidationResult of
      0: Result := True;
      1: begin
           ErrorMsg := 'This URL does not appear to be a valid prayer times page.';
           MsgBox(ErrorMsg + #13#10#13#10 +
             'Please open https://www.gebetszeiten.de in your browser, search your city, ' +
             'select MWL 2007, and copy the full URL.', mbError, MB_OK);
           Result := False;
         end;
      2: begin
           ErrorMsg := 'Could not reach the URL. Please check:' + #13#10 +
             '  - The URL is correct' + #13#10 +
             '  - You have an internet connection' + #13#10 +
             '  - The website is accessible';
           MsgBox(ErrorMsg, mbError, MB_OK);
           Result := False;
         end;
    end;
  end;
end;

procedure WriteAppSettings;
var
  Json: string;
begin
  Json :=
    '{' + #13#10 +
    '  "Logging": {' + #13#10 +
    '    "LogLevel": {' + #13#10 +
    '      "Default": "Information",' + #13#10 +
    '      "Microsoft.Hosting.Lifetime": "Information"' + #13#10 +
    '    }' + #13#10 +
    '  },' + #13#10 +
    '  "PrayerSettings": {' + #13#10 +
    '    "Url": "' + UserUrl + '"' + #13#10 +
    '  }' + #13#10 +
    '}';
  SaveStringToFile(ExpandConstant('{app}\appsettings.json'), Json, False);
end;

procedure RemoveExistingService;
var
  ResultCode: Integer;
begin
  Exec('sc.exe', ExpandConstant('stop "{#ServiceName}"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', ExpandConstant('delete "{#ServiceName}"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure UrlLinkClick(Sender: TObject);
var
  ResultCode: Integer;
begin
  ShellExec('open', 'https://www.gebetszeiten.de', '', '', SW_SHOW, ewNoWait, ResultCode);
end;

procedure InitializeWizard;
begin
  UrlPage := CreateInputQueryPage(wpWelcome,
    'Prayer Times URL',
    'Paste your gebetszeiten.de URL',
    'Search your city, select MWL 2007, and copy the full URL.' + #13#10 +
    'Click the link below to open the website:');

  UrlPage.Add('Paste your URL:', False);
  UrlPage.Values[0] := '';

  UrlLink := TNewStaticText.Create(UrlPage);
  UrlLink.Parent := UrlPage.Surface;
  UrlLink.Caption := 'URL: https://www.gebetszeiten.de';
  UrlLink.Cursor := crHand;
  UrlLink.Font.Style := [fsUnderline];
  UrlLink.Font.Color := clBlue;
  UrlLink.OnClick := @UrlLinkClick;
  UrlLink.AutoSize := True;
  UrlLink.Left := UrlPage.Edits[0].Left;

  UrlPage.Edits[0].Top := UrlPage.Edits[0].Top + ScaleY(30);
  UrlPage.PromptLabels[0].Top := UrlPage.PromptLabels[0].Top + ScaleY(30);

  UrlLink.Top := UrlPage.Edits[0].Top - ScaleY(40);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if ServiceExists then
    RemoveExistingService;
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteAppSettings;
end;
