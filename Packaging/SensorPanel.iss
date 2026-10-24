#define AppName "AORUS Sensor Panel - FAN MADE"
#define AppVersion "1.8.12"
#ifndef PackageRoot
  #error PackageRoot must identify the validated shared-runtime publish directory.
#endif
#ifndef UpgradeCleanup
  #error UpgradeCleanup must identify the exact legacy package file cleanup list.
#endif
#ifndef OutputRoot
  #error OutputRoot must identify the installer output directory.
#endif
[Setup]
#if defined(RuntimeProbeRoot) || defined(WebViewProbeVersion)
AppId={{A581AA94-321C-4C69-AB20-7551DA1D7212}
#else
AppId={{1B7C308F-694A-4C92-8C95-218009316F78}
#endif
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Independent FAN MADE project
AppComments=Independent fan-made sensor panel; not affiliated with GIGABYTE or AORUS.
DefaultDirName={autopf}\AORUS Sensor Panel FAN MADE
DefaultGroupName={#AppName}
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\Assets\Branding\AorusFanMade.ico
UninstallDisplayIcon={app}\SensorPanel.exe
OutputDir={#OutputRoot}
OutputBaseFilename=AORUS-Sensor-Panel-FAN-MADE-Setup
Compression=lzma2
SolidCompression=yes
DiskSpanning=no
CloseApplications=no
RestartApplications=no
AppMutex=Local\AORUS-FAN-MADE-SensorPanel-1B7C308F
InfoBeforeFile=README.txt
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start with Windows (fullscreen sensor display)"; GroupDescription: "Startup:"; Flags: unchecked; Check: IsAdminInstallMode
[Files]
Source: "{#PackageRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[InstallDelete]
#include UpgradeCleanup
[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\SensorPanel.exe"; IconFilename: "{app}\SensorPanel.exe"
Name: "{group}\README - FAN MADE"; Filename: "{app}\SensorPanel.exe"; Parameters: "--readme"; IconFilename: "{app}\SensorPanel.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\SensorPanel.exe"; IconFilename: "{app}\SensorPanel.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\SensorPanel.exe"; Parameters: "--enable-startup"; Tasks: startup; Flags: runascurrentuser
Filename: "{app}\SensorPanel.exe"; Parameters: "--disable-startup"; Check: DisableStartup; Flags: runascurrentuser
Filename: "{app}\SensorPanel.exe"; Parameters: "--readme"; Description: "Open the README"; Flags: postinstall nowait skipifsilent
Filename: "{app}\SensorPanel.exe"; Description: "Launch AORUS Sensor Panel - FAN MADE"; Flags: postinstall nowait skipifsilent unchecked runascurrentuser
[UninstallRun]
Filename: "{app}\SensorPanel.exe"; Parameters: "--remove-install-startup"; Flags: runascurrentuser skipifdoesntexist; RunOnceId: RemoveOwnedStartup
[Code]
const
  RuntimeDownload = 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0';
  WebViewDownload = 'https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section';
  WebViewMessage = 'Microsoft Edge WebView2 Evergreen Runtime is required for the ship viewer but was not found.' + #13#10#13#10 +
    'Install the Evergreen Runtime from Microsoft, then choose Check again.' + #13#10 +
    'Setup does not install or change your web browser.';
  RuntimeMessage = 'Microsoft .NET 10 Desktop Runtime (x64) is required but was not found.' + #13#10#13#10 +
    'Install the Windows x64 Desktop Runtime from Microsoft, then retry Setup.' + #13#10 +
    'The x86 runtime, .NET Framework, and .NET 8 do not satisfy this requirement.' + #13#10#13#10 +
    'Windows 11 alone does not guarantee that this runtime is installed.';
var
  RuntimePage: TWizardPage;
  RuntimeLabel: TNewStaticText;

function StableRuntimeVersion(Version: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  if (Copy(Version, 1, 5) <> '10.0.') or (Length(Version) < 6) then Exit;
  for I := 6 to Length(Version) do
    if (Version[I] < '0') or (Version[I] > '9') then Exit;
  Result := True;
end;

function FrameworkAtRoot(Root, FrameworkName, RequiredFile: String): Boolean;
var
  Found: TFindRec;
  Base: String;
begin
  Result := False;
  Base := AddBackslash(Root) + 'shared\' + FrameworkName + '\';
  if FindFirst(Base + '10.0.*', Found) then begin
    try
      repeat
        if StableRuntimeVersion(Found.Name) and FileExists(Base + Found.Name + '\' + RequiredFile) then begin
          Result := True;
          Exit;
        end;
      until not FindNext(Found);
    finally
      FindClose(Found);
    end;
  end;
end;

function RuntimeAtRoot(Root: String): Boolean;
begin
  Result := FrameworkAtRoot(Root, 'Microsoft.NETCore.App', 'coreclr.dll') and
    FrameworkAtRoot(Root, 'Microsoft.WindowsDesktop.App', 'PresentationFramework.dll');
end;

function HasDesktopRuntime: Boolean;
var
  Root: String;
begin
  Result := False;
  if not IsWin64 then Exit;
  { Compile-time fixture for prerequisite validation; never enabled in releases. }
#ifdef RuntimeProbeRoot
  Result := RuntimeAtRoot('{#RuntimeProbeRoot}');
  Exit;
#endif
  if RegQueryStringValue(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64', 'InstallLocation', Root) then
    if RuntimeAtRoot(Root) then begin Result := True; Exit; end;
  if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64', 'InstallLocation', Root) then
    if RuntimeAtRoot(Root) then begin Result := True; Exit; end;
  Result := RuntimeAtRoot(ExpandConstant('{commonpf64}\dotnet'));
end;

function HasWebViewRuntime: Boolean;
var
  Version, Key: String;
begin
  Key := 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  { Compile-time negative fixture; never supplied to release builds. }
#ifdef WebViewProbeVersion
  Version := '{#WebViewProbeVersion}';
  Result := (Trim(Version) <> '') and (Version <> '0.0.0.0');
  Exit;
#endif
  Result := False;
  if RegQueryStringValue(HKLM32, Key, 'pv', Version) then
    if (Trim(Version) <> '') and (Version <> '0.0.0.0') then begin Result := True; Exit; end;
  if RegQueryStringValue(HKCU32, Key, 'pv', Version) then
    if (Trim(Version) <> '') and (Version <> '0.0.0.0') then begin Result := True; Exit; end;
  if RegQueryStringValue(HKCU64, Key, 'pv', Version) then
    if (Trim(Version) <> '') and (Version <> '0.0.0.0') then begin Result := True; Exit; end;
end;

procedure RefreshRuntime(Sender: TObject);
begin
  if not HasDesktopRuntime then RuntimeLabel.Caption := RuntimeMessage
  else if not HasWebViewRuntime then RuntimeLabel.Caption := WebViewMessage
  else RuntimeLabel.Caption := 'Microsoft .NET 10 Desktop Runtime (x64) and WebView2 Evergreen Runtime were found.' + #13#10#13#10 +
    'Setup uses the shared runtimes already on your PC. It does not bundle private copies.';
end;

procedure OpenRuntimeDownload(Sender: TObject);
var
  ErrorCode: Integer;
begin
  if not HasDesktopRuntime then ShellExec('open', RuntimeDownload, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode)
  else ShellExec('open', WebViewDownload, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard;
var
  DownloadButton, RefreshButton: TNewButton;
begin
  RuntimePage := CreateCustomPage(wpWelcome, 'Required runtimes', '.NET 10 Desktop (x64) and WebView2 Evergreen');
  RuntimeLabel := TNewStaticText.Create(RuntimePage);
  RuntimeLabel.Parent := RuntimePage.Surface;
  RuntimeLabel.SetBounds(0, 0, RuntimePage.SurfaceWidth, ScaleY(145));
  RuntimeLabel.AutoSize := False;
  RuntimeLabel.WordWrap := True;
  DownloadButton := TNewButton.Create(RuntimePage);
  DownloadButton.Parent := RuntimePage.Surface;
  DownloadButton.SetBounds(0, ScaleY(160), ScaleX(200), ScaleY(28));
  DownloadButton.Caption := 'Open Microsoft download';
  DownloadButton.OnClick := @OpenRuntimeDownload;
  RefreshButton := TNewButton.Create(RuntimePage);
  RefreshButton.Parent := RuntimePage.Surface;
  RefreshButton.SetBounds(ScaleX(210), ScaleY(160), ScaleX(110), ScaleY(28));
  RefreshButton.Caption := 'Check again';
  RefreshButton.OnClick := @RefreshRuntime;
  RefreshRuntime(nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = RuntimePage.ID then begin
    RefreshRuntime(nil);
    Result := HasDesktopRuntime and HasWebViewRuntime;
    if not Result then begin
      if WizardSilent then Log(RuntimeLabel.Caption)
      else MsgBox(RuntimeLabel.Caption, mbError, MB_OK);
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  { Also gates silent installs before any legacy runtime cleanup or file writes. }
  if not HasDesktopRuntime then Result := RuntimeMessage + #13#10#13#10 + RuntimeDownload
  else if not HasWebViewRuntime then Result := WebViewMessage + #13#10#13#10 + WebViewDownload;
end;

function DisableStartup: Boolean;
begin
  Result := IsAdminInstallMode and not WizardIsTaskSelected('startup');
end;
