#define AppName "AORUS Sensor Panel - FAN MADE"
#define AppVersion "1.0.1"
#ifndef PackageRoot
  #error PackageRoot must identify the validated self-contained publish directory.
#endif
#ifndef OutputRoot
  #error OutputRoot must identify the installer output directory.
#endif
[Setup]
AppId={{1B7C308F-694A-4C92-8C95-218009316F78}
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
function DisableStartup: Boolean;
begin
  Result := IsAdminInstallMode and not WizardIsTaskSelected('startup');
end;
