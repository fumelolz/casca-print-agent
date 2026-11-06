; Instalador de Casca Print Agent (Inno Setup 6).
; Se compila con build-installer.ps1, que publica el agente y pasa PublishDir y AppVersion.
; Nota: mantener este archivo en ASCII.

#ifndef PublishDir
  #error Falta PublishDir: compila con build-installer.ps1
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "Casca Print Agent"
#define AppExe "Casca.PrintAgent.exe"

[Setup]
AppId={{7D3B5C1E-4A6F-4E0B-9C2A-5F1B8E6D2A47}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Casca Tiendas
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Instalacion por usuario: no pide permisos de administrador.
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=CascaPrintAgent-Setup-{#AppVersion}
SetupIconFile=..\src\Casca.PrintAgent\Assets\casca.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ShowLanguageDialog=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,appsettings.Development.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Inicio automatico al abrir sesion en Windows.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CascaPrintAgent"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Iniciar {#AppName} ahora"; Flags: nowait postinstall skipifsilent

[Code]
// Cierra el agente si esta abierto, para poder reemplazar sus archivos al actualizar o desinstalar.
procedure StopAgent();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopAgent();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopAgent();
end;
