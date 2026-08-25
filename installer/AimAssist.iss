; AUTOAIMLOCK installer — Inno Setup 6 script
; Build: "C:\Users\fortu\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer\AUTOAIMLOCK.iss

#define AppName "AUTOAIMLOCK"
#define AppVersion "1.3.1"
#define AppPublisher "AUTOAIMLOCK Project"
#define AppExeName "AUTOAIMLOCK.App.exe"
#define PublishDir "..\publish"

[Setup]
AppId={{F8D2A6B3-7E4C-4B91-A2D5-3C6E8F0B1D24}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\AUTOAIMLOCK
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\publish
OutputBaseFilename=AUTOAIMLOCK-Setup-{#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; self-contained single-file app (native libs self-extract from the exe)
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
