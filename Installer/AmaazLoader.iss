#define MyAppName "AmaazLoader"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "Amaazbots"
#define MyAppExeName "AmaazLoader.exe"

[Setup]
AppId={{C52D971A-9D0E-4A1B-9654-7DE5D4F38217}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

SetupIconFile=..\Assets\AmaazLoader.ico

OutputDir=..\publish\Installer
OutputBaseFilename=AmaazLoader-v{#MyAppVersion}-Setup

Compression=lzma2
SolidCompression=yes

WizardStyle=modern

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

PrivilegesRequired=admin

DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

VersionInfoVersion=1.2.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=AmaazLoader Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCopyright=Copyright © 2026 Amaazbots

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\AmaazLoader\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent