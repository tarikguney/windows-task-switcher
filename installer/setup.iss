[Setup]
AppName=Windows Task Switcher
#ifndef APP_VERSION
  #define APP_VERSION GetEnv('APP_VERSION')
  #if APP_VERSION == ""
    #define APP_VERSION "0.0.0"
  #endif
#endif
AppVersion={#APP_VERSION}
AppPublisher=tarikguney
AppPublisherURL=https://github.com/tarikguney/windows-task-switcher
DefaultDirName={autopf}\WindowsTaskSwitcher
DefaultGroupName=Windows Task Switcher
UninstallDisplayIcon={app}\WindowTaskSwitcher.exe
OutputDir=..\installer-output
OutputBaseFilename=WindowsTaskSwitcher-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\WindowTaskSwitcher\Resources\app.ico
WizardStyle=modern
DisableProgramGroupPage=yes

[Files]
Source: "..\publish\WindowTaskSwitcher.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Windows Task Switcher"; Filename: "{app}\WindowTaskSwitcher.exe"
Name: "{group}\Uninstall Windows Task Switcher"; Filename: "{uninstallexe}"
Name: "{userstartup}\Windows Task Switcher"; Filename: "{app}\WindowTaskSwitcher.exe"; Tasks: startup

[Tasks]
Name: "startup"; Description: "Start automatically with Windows"; GroupDescription: "Additional options:"

[Run]
Filename: "{app}\WindowTaskSwitcher.exe"; Description: "Launch Windows Task Switcher"; Flags: nowait postinstall skipifsilent
