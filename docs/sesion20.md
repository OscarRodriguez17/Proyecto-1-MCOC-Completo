
---

## Sesión 20 — Corrección 2: rig AR con UNA cámara, fondo, pose y toques

**Fecha:** 2026-10-01
**Alcance:** únicamente la app AR. Sin tocar `exportar_ar.py`, `ar_elementos.json`,
`ARGeometriaBuilder`, `ARColocacion` ni `Main.unity`. Todos los cambios son aditivos.

### Diagnóstico recibido

En el teléfono ARCore **ya trackeaba** (`SessionTracking`, permiso de cámara OK), pero:

- el fondo era el azul de borrado de Unity, sin imagen de la cámara;
- la vista no seguía al teléfono;
- la interfaz bloqueaba los toques.

### Causa raíz

Faltaban tres cosas del rig canónico de AR Foundation 4.2:

1. **`ARCameraBackground` no existía.** Es el componente que vuelca la textura de la
   cámara AR. Sin él la cámara se dibuja contra su color de borrado: de ahí el azul.
2. **La cámara no era hija de un `ARSessionOrigin`.** El rig viejo colgaraba
   `ARCamera` del GameObject raíz, así que el origen de trackeo no la gobernaba y la
   pose no seguía el dispositivo.
3. **No había `ARPoseDriver`**, que es el que aplica la pose cada frame en 4.2.

Se exploratory el API real de los paquetes en `Library/PackageCache` y se confirmó:

- El subsistema se llama **`XRCameraSubsystem`**, no `ARCameraSubsystem` (por eso un
  intento previo de diagnóstico con `ARCameraSubsystem` no compilaba).
- `ARCameraManager` deriva de
  `SubsystemLifecycleManager<XRCameraSubsystem, ...>`.
- `ARCameraBackground` tiene `[RequireComponent(typeof(ARCameraManager))]` y toma su
  material de `cameraManager.cameraMaterial`.
- `ARPoseDriver` es público y usa el `InputDevices` legado.
- `ARSessionOrigin` expone la propiedad `camera`.
- `ARCameraBackground` **guarda y sobrescribe** `clearFlags` a `Nothing` mientras
  pinta el fondo, restaurando el valor previo al desactivarse. Por eso el
  `clearFlags` se fija *después* de añadir el componente.

### 1) Una sola cámara

Se extrajo el rig a `Assets/Scripts/AR/ARRig.cs` con `ARRig.Construir(Transform)`:

```
ARSession                    (ARSession)
  ARSessionOrigin            (ARSessionOrigin, ARPlaneManager, ARAnchorManager, ARRaycastManager)
    ARCamera                 (Camera, ARCameraManager, ARCameraBackground, ARPoseDriver)
```

- La `ARCamera` lleva `tag = MainCamera`, `clearFlags = SolidColor`, fondo **negro**,
  `depth = 0`, near 0.05, far 200.
- `ARRig.Construir` llama a `DestruirCamarasAjenas()`, que desactiva y destruye
  cualquier otra `Camera` de la escena y registra los nombres eliminados en
  `camarasEliminadas`.
- Se eliminó por completo el `ConstruirRigAR()` antiguo de `ARInspeccionApp`.
- `AR_Inspeccion.unity` se verificó: 1 GameObject (`ARInspeccion`), **0 cámaras**.
  El rig se construye 100 % por código.

### 2) Diagnóstico ampliado (cada 0,5 s)

`ARRig.Describir()` alimenta un panel de UI (se sustituyó el `OnGUI` anterior):

- `Cámaras: N (nombres)` — incluye el sufijo `[off]` si está deshabilitada.
- `Fondo: ARCameraBackground enabled=? / material=<shader o "<sin material>">`
- `Pose cámara: x,y,z / yaw` — debe cambiar al mover el teléfono.
- `Planos detectados: N`
- `Ancla: sí/no`, `Selección: <tag>`, `FPS`.

El panel se refresca en `Update()` con `proximoDiag = Time.time + 0.5f`.

### 3) Migración a AR Foundation 5.1 — **NO ejecutada (condicional)**

Queda **pendiente**: sólo aplica si, con una sola cámara, el fondo sigue sin la
imagen de la cámara o la pose no cambia. Ninguna de las dos condiciones se puede
evaluar sin una prueba en el teléfono. Ver "Pendientes" más abajo.

### 4) Interfaz

`ARInterfaz.Construir` pasó a llamarse **`ARInterfaz.Crear`**.

- **Táctil:** los `Image` de los paneles de fondo ahora tienen `raycastTarget = false`.
  Antes `true` en paneles de pantalla casi completa: el `GraphicRaycaster` se comía el
  toque dirigido al piso. Sólo los botones interceptan.
- Se añadió `SobreUI(Touch)` en `ToqueSimple()`: sin esa comprobación, tocar un botón
  también disparaba el gesto de colocar y el ancla saltaba al centro de la pantalla.
- **Panel DIAGNOSTICO:** abajo a la izquierda, `anchorMax.x = 0.40` (≤ 40 % del ancho),
  banda vertical 10..150. `raycastTarget = false` en el `Image` y en sus textos.
- **Botón "Diag"** en la columna Controles que muestra u oculta el panel.
- **Panel Elementos:** la lista sale ahora del contrato (`datos.EnOrden()`), no de la
  geometría, así que hay un botón por tag aunque la construcción visual no haya
  terminado. Tags del contrato: **134, 14, 26** (ordenados: 14, 26, 134).
- **Resaltado:** `ResaltarElemento(tag)` pinta el botón elegido de azul
  (`0.16, 0.52, 0.86`). Se indexa con `botonPorTag` en vez de parsear `b.name`.
- **Defaults:** Eje SÍ, M SÍ, V SÍ, N **no**, No principal no, P-M SÍ.
- **Layout:** cabecera y estado ocupan 0..-134; los paneles laterales arrancan en
  y = -142; la banda inferior ocupa 0..150. Los textos `estado` e `info` quedan
  verificados por test contra los tres paneles.
- `info` ahora muestra tag, ubicación, sección, material, plano principal y longitud,
  con respaldo al contrato cuando la geometría todavía no está construida.

### 5) Test EditMode

Nuevo `Assets/Tests/EditMode/TestRigYInterfaz.cs` — **16 tests, 16 pasan**.

Cubre: exactamente 1 cámara; jerarquía canónica; `clearFlags` negro y `depth` 0;
`ARCameraBackground` enabled; destrucción de cámaras ajenas; 0 cámaras en
`AR_Inspeccion.unity`; ningún `Image` con `raycastTarget = true` solapa un `Button`
en rect de pantalla 2772x1280; paneles de fondo no interceptan; un botón por tag;
resaltado del elegido; diag ≤ 40 % y en la mitad inferior; `estado`/`info` no solapan
paneles.

Dos desviaciones que hicieron falta para que compilara el assembly de tests:

- Se creó `Assets/Scripts/AR/MCOC.AR.asmdef`: los tests no pueden referenciar
  `Assembly-CSharp`, así que el código AR necesita su propio ensamblado.
- `XElement.Count()` requiere `System.Linq`; se llama explícitamente.

### Herramientas de build

- `MCOCBuildAndroid.TestsYBuildArBatch()` — lee `editmode_results.xml` y, sólo si
  está en verde, compila el APK. Si algún test falló, no genera el binario.

**Los EditMode se lanzan con el flag de línea de comandos, no con
`TestRunnerApi.Execute`.** Invocado desde `-executeMethod` en batchmode, el runner no
completa y el proceso queda esperando el XML indefinidamente (se colgaron dos
corridas de ~8 min por esto). Además `TestRunnerApi` es un `ScriptableObject`:
necesita `CreateInstance`, y `new` lanza una excepción. La secuencia que funciona
son **dos invocaciones**:

```powershell
# 1) Tests
Unity.exe -batchmode -projectPath <proj> -runTests -testPlatform EditMode `
  -testResults <proj>\editmode_results.xml -logFile tests.log

# 2) Build (sólo si los tests pasaron)
Unity.exe -batchmode -nographics -projectPath <proj> -buildTarget Android `
  -executeMethod MCOC.EditorTools.MCOCBuildAndroid.TestsYBuildArBatch `
  -logFile build_android.log
```

### Resultado de la corrida

Tests: **16/16 correctos**, 0 fallos.
Build: `Succeeded`, 456 574 771 bytes. APK de 23,3 MB.

Verificación del binario con `aapt2` y lectura de la tabla `lib/` del ZIP:

- `com.mcoc.edificiocomplejo.ar`, versionName 1.0, compileSdk 36
- `application-label: 'MCOC AR'`
- `android.hardware.camera.ar`, `android.hardware.camera`, `com.google.ar.core.depth`
- permiso `android.permission.CAMERA`
- un solo ABI: `arm64-v8a`

### Problema de entorno encontrado

`Start-Process` con `-ArgumentList` como **array** no entrecomilla los argumentos en
PowerShell 5.1. Con rutas que contienen espacios (`Proyecto 1`) Unity recibía
`-logFile C:\Users\pablo\Desktop\Proyecto` y un argumento basura, y el build terminaba
sin escribir el log. La forma correcta es construir **un único string** con comillas
explícitas alrededor de cada ruta.

### Corrección de robustez anterior (Sesión 19)

`ARInterfaz.CargarFuente` hacía `Resources.FindObjectsOfTypeAll<Font>()[0]`: sin
fuentes cargadas ese índice lanzaba `IndexOutOfRangeException` sin capturar, y como
`Awake()` la llamaba en la primera línea, abortaba **antes** de construir el rig.
Ahora nunca lanza y `Awake` va dentro de un `try/catch` que deja la interfaz en
pantalla aunque algo falle.

### Pendientes

- [ ] **Probar en el teléfono** y mandar los valores del panel DIAGNOSTICO con el
      teléfono en la mano. Es lo que decide si hace falta el punto 3.
- [ ] Si el fondo sigue sin imagen de cámara o la pose no cambia: migrar a
      AR Foundation 5.1.x + ARCore XR Plugin 5.1.x, `XROrigin`
      (`com.unity.xr.core-utils`) en vez de `ARSessionOrigin`, `TrackedPoseDriver`
      (`com.unity.inputsystem`) en vez de `ARPoseDriver`, `Active Input Handling = Both`
      (el código usa `Input.touchCount`), y ajustar `ARRig`, `ARRigTests` y
      `XRAndroidConfigTests` a 5.1. `ARColocacion` no cambia.
- [ ] El `productName` ya es `MCOC AR` (cambiado en `ConfigurarAndroid`).
- [ ] Decidir si `MCOC_Complejo_AB/` se integra al repo principal o se trabaja aparte.

### Entrega

- **APK:** `C:\Users\pablo\Desktop\MCOC_AR.apk` (copia del de
  `unity/EdificioSolidoUnity/Builds/MCOC_AR.apk`, 24 479 023 bytes,
  SHA-256 `09A7AB82F9F95554D2EB7CCD5974077722EA374FA707847FFB60884FAA739969`).
- **ZIP:** `C:\Users\pablo\Desktop\MCOC_Complejo_AB_entrega.zip` (28 MB, 407 entradas,
  `7z t` = OK). Contiene el proyecto (excluidos `Library/`, `Temp/`, `Logs/`, `obj/`,
  `Builds/`, `.vscode/`, `UserSettings/`, `.git/`) más el `MCOC_AR.apk` en la raíz; el
  hash del APK dentro del zip coincide con el de escritorio.
- Para transferirlo al teléfono no hace falta el cable: copiar el `.apk` al móvil y
  tocarlo. Android pedirá permitir "instalar apps de orígenes desconocidos" para el
  explorador de archivos; después, conceder el permiso de cámara.
