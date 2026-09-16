@echo off
setlocal
set UNITY="C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
set PROY=%~dp0unity\EdificioComplejoUnity

if not exist %UNITY% (
  echo [ERROR] No se encontro el editor Unity en %UNITY%
  exit /b 1
)

echo Abriendo proyecto Unity del complejo...
%UNITY% -projectPath "%PROY%"