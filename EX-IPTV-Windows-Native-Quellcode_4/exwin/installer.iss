; EX-IPTV Installer (Inno Setup)
#define AppName "EX-IPTV"
#define AppVer "3.0.1"

[Setup]
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher=EX-IPTV
DefaultDirName={autopf}\EX-IPTV
DefaultGroupName=EX-IPTV
UninstallDisplayIcon={app}\EX-IPTV.exe
OutputDir=Output
OutputBaseFilename=EX-IPTV-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest

[Files]
; Kompletter Publish-Ordner inkl. libvlc + plugins
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\EX-IPTV"; Filename: "{app}\EX-IPTV.exe"
Name: "{userdesktop}\EX-IPTV"; Filename: "{app}\EX-IPTV.exe"

[Run]
Filename: "{app}\EX-IPTV.exe"; Description: "EX-IPTV starten"; Flags: nowait postinstall skipifsilent
