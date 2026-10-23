; IBRA SYSTEM CARE — Windows installer (for the current user, no administrator rights), Arabic first.
; تصميم وتطوير وتعريب إبراهيم الخزرجي
; Built by the GitHub workflow or BUILD-ON-WINDOWS.ps1, which pass /DAppVersion=1.0.N /DSource=<published folder>

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef Source
  #define Source "..\build\publish"
#endif

[Setup]
AppId={{28F73BCE-7579-46C7-AAED-92B72D9016E4}
AppName=IBRA SYSTEM CARE
AppVersion={#AppVersion}
AppVerName=IBRA SYSTEM CARE {#AppVersion}
AppPublisher=إبراهيم الخزرجي — IBRAPROGRAM
AppPublisherURL=https://github.com/ibrata2/ibra-system-care
AppSupportURL=https://t.me/ibraprogram
AppUpdatesURL=https://github.com/ibrata2/ibra-system-care/releases/latest
AppCopyright=تصميم وتطوير وتعريب إبراهيم الخزرجي
VersionInfoVersion={#AppVersion}
VersionInfoDescription=IBRA SYSTEM CARE — صيانة Windows
DefaultDirName={autopf}\IBRA SYSTEM CARE
DefaultGroupName=IBRA SYSTEM CARE
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
SetupIconFile=..\assets\ibracare.ico
WizardImageFile=wizard-side-100.bmp,wizard-side-150.bmp,wizard-side-200.bmp
WizardSmallImageFile=wizard-corner-100.bmp,wizard-corner-150.bmp,wizard-corner-200.bmp
UninstallDisplayIcon={app}\IbraSystemCare.exe
UninstallDisplayName=IBRA SYSTEM CARE
OutputDir=..\build\installer
OutputBaseFilename=IBRA-System-Care-Setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
ShowLanguageDialog=auto
MinVersion=10.0.17763
CloseApplications=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ar.DesktopIcon=إنشاء اختصار على سطح المكتب
en.DesktopIcon=Create a desktop shortcut
ar.Extras=خيارات:
en.Extras=Options:
ar.Launch=تشغيل IBRA SYSTEM CARE
en.Launch=Start IBRA SYSTEM CARE

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:Extras}"

[Files]
Source: "{#Source}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\IBRA SYSTEM CARE"; Filename: "{app}\IbraSystemCare.exe"
Name: "{autodesktop}\IBRA SYSTEM CARE"; Filename: "{app}\IbraSystemCare.exe"; Tasks: desktopicon

[Run]
; The app starts in the language of the installer the first time.
Filename: "{app}\IbraSystemCare.exe"; Parameters: "--lang {language}"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent
; After an update from inside the app (silent install), start it again.
Filename: "{app}\IbraSystemCare.exe"; Flags: nowait; Check: WizardSilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
{ Closes a running copy so every file can be replaced. History, backups and settings (in %LOCALAPPDATA%) are kept. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im IbraSystemCare.exe', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(400);
  Result := '';
end;
