#!/usr/bin/env pwsh
# =============================================================================
# subir_json_telefono.ps1 — actualiza el JSON del visor en el telefono SIN
# recompilar el APK.
#
# El APK lleva dentro el C# compilado (IL2CPP) y la escena, asi que el DISENO
# solo cambia recompilando (Tools/MCOC/Build Android). Los DATOS, en cambio,
# se toman de Application.persistentDataPath, que en Android es
#
#     /sdcard/Android/data/com.mcoc.edificiocomplejo/files/
#
# y es escribible por adb. Este script copia ahi el JSON vigente; despues, en
# el telefono, se aprieta "Recargar JSON" y el visor reconstruye las 10 capas
# con el dato nuevo. El panel muestra la fuente usada ("Fuente: telefono
# (actualizable) <fecha>"), asi que se puede confirmar que el push enteso.
#
# Uso (desde la raiz del proyecto):
#   powershell -ExecutionPolicy Bypass -File scripts\subir_json_telefono.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\subir_json_telefono.ps1 -Json results\edificio_completo.json
#   powershell -ExecutionPolicy Bypass -File scripts\subir_json_telefono.ps1 -Borrar   # vuelve al JSON del APK
# =============================================================================
[CmdletBinding()]
param(
    [string]$Json = "",
    [switch]$Borrar
)

$ErrorActionPreference = "Stop"

$AQUI = Split-Path -Parent $MyInvocation.MyCommand.Path
$RAIZ = Split-Path -Parent $AQUI
$Paquete = "com.mcoc.edificiocomplejo"
$Destino = "/sdcard/Android/data/$Paquete/files"
$DestinoArchivo = "$Destino/edificio_completo.json"

if (-not $Json) { $Json = Join-Path $RAIZ "unity\EdificioSolidoUnity\Assets\StreamingAssets\edificio_completo.json" }
if (-not (Test-Path -LiteralPath $Json)) { throw "No existe el JSON local: $Json" }

# --- 1) adb disponible y con un solo dispositivo --------------------------------
$adb = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adb) { throw "adb no esta en el PATH. Instalalo (platform-tools de Android SDK) y anadelo al PATH." }

$lineas = & adb devices | Select-Object -Skip 1 | Where-Object { $_.Trim() -ne "" }
$dispositivos = @($lineas | Where-Object { $_ -match "\sdevice$" })
if ($dispositivos.Count -eq 0) { throw "No hay ningun telefono conectado (o sin depuracion USB). 'adb devices' no lista ninguno." }
if ($dispositivos.Count -gt 1) {
    Write-Warning "Hay $($dispositivos.Count) dispositivos; se usa el primero."
}
$serial = ($dispositivos[0] -split "\s+")[0]
Write-Host "Dispositivo: $serial" -ForegroundColor Cyan

# --- 2) borrar la copia (volver al JSON del APK) --------------------------------
if ($Borrar) {
    & adb -s $serial shell rm -f $DestinoArchivo | Out-Null
    Write-Host "Borrada la copia del telefono. La app volvio al JSON del APK." -ForegroundColor Yellow
    Write-Host "Reinicia la app y aprieta 'Recargar JSON'."
    exit 0
}

# --- 3) empujar el JSON ----------------------------------------------------------
$info = Get-Item -LiteralPath $Json
Write-Host ("Enviando {0} ({1:N0} bytes, {2})" -f $info.Name, $info.Length, $info.LastWriteTime) -ForegroundColor Cyan
Write-Host "  -> $DestinoArchivo"

& adb -s $serial shell mkdir -p $Destino | Out-Null
& adb -s $serial push $Json $DestinoArchivo
if ($LASTEXITCODE -ne 0) { throw "adb push fallo (codigo $LASTEXITCODE)." }

# --- 4) verificar lo que quedo en el telefono ------------------------------------
$salida = & adb -s $serial shell ls -l $DestinoArchivo
Write-Host "En el telefono: $salida" -ForegroundColor Green
Write-Host ""
Write-Host "Listo. En el telefono: aprieta 'Recargar JSON'." -ForegroundColor Green
Write-Host "El panel debe decir 'Fuente: telefono (actualizable)' con la fecha de hoy."
