#ifndef MyAppVersion
  #error Supply MyAppVersion from Directory.Build.props through scripts/package.ps1
#endif
[Setup]
AppId={{657DCFD1-3CCB-46E7-9BB1-534F69B57C36}
AppName=ClipDrop
AppVersion={#MyAppVersion}
AppPublisher=NasaatR
AppPublisherURL=https://github.com/NasaatR/ClipDrop
DefaultDirName={localappdata}\Programs\ClipDrop
DefaultGroupName=ClipDrop
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\release
OutputBaseFilename=ClipDrop_{#MyAppVersion}_windows-x64_setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\ClipDrop.Windows\Assets\ClipDrop.ico
UninstallDisplayIcon={app}\ClipDrop.exe
AppMutex=Local\NasaatR.ClipDrop
CloseApplications=no
LicenseFile=..\LICENSE
MinVersion=10.0.17763
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
Name: startup; Description: "Start ClipDrop when I sign in"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\ClipDrop"; Filename: "{app}\ClipDrop.exe"
Name: "{autodesktop}\ClipDrop"; Filename: "{app}\ClipDrop.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClipDrop"; ValueData: """{app}\ClipDrop.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\com.nasaat.clipdrop.downloads"; ValueType: string; ValueData: "{app}\com.nasaat.clipdrop.downloads.json"; Flags: 32bit
[Run]
Filename: "{app}\ClipDrop.exe"; Description: "Open ClipDrop"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var CurrentManifest: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU32, 'Software\Google\Chrome\NativeMessagingHosts\com.nasaat.clipdrop.downloads', '', CurrentManifest) then
      if CompareText(CurrentManifest, ExpandConstant('{app}\com.nasaat.clipdrop.downloads.json')) = 0 then
        RegDeleteKeyIncludingSubkeys(HKCU32, 'Software\Google\Chrome\NativeMessagingHosts\com.nasaat.clipdrop.downloads');
end;
