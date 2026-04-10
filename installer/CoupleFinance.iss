#define MyAppName "Couple Finance"
#define MyAppVersion "1.1.44"
#define MyAppPublisher "Couple Finance"
#define MyAppExeName "CoupleFinance.Desktop.exe"

[Setup]
AppId={{6A7F6A4D-BEF1-4F55-8A6D-76F4B381A1A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Couple Finance
DefaultGroupName=Couple Finance
OutputDir=..\artifacts\installer
OutputBaseFilename=CoupleFinance-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\CoupleFinance.Desktop\Assets\AppIcon.ico
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UsePreviousAppDir=no
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Couple Finance"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\Couple Finance"; Filename: "{app}\{#MyAppExeName}"; Check: ShouldCreateDesktopShortcut

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Couple Finance"; Flags: nowait postinstall skipifsilent

[Code]
function LegacyDesktopShortcutPath: string;
begin
  Result := AddBackslash(GetEnv('PUBLIC')) + 'Desktop\Couple Finance.lnk';
end;

function ShouldCreateDesktopShortcut: Boolean;
begin
  Result := WizardIsTaskSelected('desktopicon') or FileExists(LegacyDesktopShortcutPath);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if FileExists(LegacyDesktopShortcutPath) then
      DeleteFile(LegacyDesktopShortcutPath);
  end;
end;
