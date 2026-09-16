@echo off
REM Abre el VISOR SOLIDO del Complejo (columnas/vigas/muros 3D, cargas, deformada)
REM Requiere Unity 2022.3.62f3 (mismo editor del proyecto del complejo).
set "UNITY=C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
set "PROY=%~dp0unity\EdificioSolidoUnity"
if not exist "%UNITY%" (
  echo No se encontro Unity en "%UNITY%". Edita la ruta en este .cmd.
  pause & exit /b 1
)
start "" "%UNITY%" -projectPath "%PROY%"
