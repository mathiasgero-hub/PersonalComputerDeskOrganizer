; Installateur Inno Setup — compilé par GitHub Actions (.github/workflows/build.yml)
; Installation par utilisateur : aucun droit administrateur requis.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define MyAppName "PersonalComputerDeskOrganizer"
#define MyAppExe "PersonalComputerDeskOrganizer.exe"

[Setup]
AppId={{6F3C2A1E-8B4D-4E7A-9C21-5D0B7A3E9F42}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=Mathias Gero
AppPublisherURL=https://desk-organizer.artmonie.com
AppSupportURL=https://desk-organizer.artmonie.com
AppUpdatesURL=https://desk-organizer.artmonie.com
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
SetupIconFile=..\src\PersonalComputerDeskOrganizer\Assets\Icons\app.ico
OutputDir=..\installer-output
OutputBaseFilename=PersonalComputerDeskOrganizer-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
CloseApplications=force
RestartApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExe} /F"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'PersonalComputerDeskOrganizer';

{ Si le démarrage automatique était activé (y compris par une ancienne copie portable),
  on le fait pointer vers l'exe installé. }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and RegValueExists(HKCU, RunKey, RunValue) then
    RegWriteStringValue(HKCU, RunKey, RunValue, '"' + ExpandConstant('{app}\{#MyAppExe}') + '"');
end;

{ Désinstallation : retire le démarrage automatique. Les profils (%AppData%) sont conservés. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, RunKey, RunValue);
end;
