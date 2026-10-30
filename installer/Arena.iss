; Inno Setup script for the Windows installer: Temnorid-Arena-Setup.exe
; Build the player first (Unity: Temnorid > Arena > Build Windows Player), then compile this with Inno Setup:
;   "ISCC.exe" installer\Arena.iss
; The installer is written to Builds\Installer.

#define AppName "Temnorid Arena"
#define AppVersion "0.2.0"
#define AppExe "TemnoridArena.exe"

[Setup]
AppId={{7B3F2C94-6A1E-4D58-9C07-E2A5D81F4B66}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Temnorid
AppPublisherURL=https://arena.temnorid.com
DefaultDirName={autopf}\Temnorid Arena
DefaultGroupName=Temnorid Arena
DisableProgramGroupPage=yes
OutputDir=..\Builds\Installer
OutputBaseFilename=Temnorid-Arena-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\Builds\Windows\TemnoridArena\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent
