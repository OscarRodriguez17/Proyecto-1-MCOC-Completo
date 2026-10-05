# ÍNDICE DE ENTREGA — Proyecto 1 MCOC (Complejo de Ingeniería A+B)

> Repositorio: `https://github.com/OscarRodriguez17/Proyecto-1-MCOC-Completo.git`
> Rama `master` — Commit + tag a evaluar: consultar `docs/bitacora.md` (sesión final).
> Suite de verificación: `python -m pytest tests/ -q` → **150 passed**.

## Reportes semanales

| Archivo | Semana | Qué cubre |
|---|---|---|
| [semana03.md](semana03.md) | 3 | Motor de secciones P–M (`src/secciones`) y superposición §3: envolventes de 17 puntos por fibra (malla 12×12) para 40 columnas + 60 muros del B y catálogo del A; punto balanceado; **demanda–capacidad D/C por elemento** (muro crítico `muro0.60x2.91`, EX/EY) y verificación `G+Q ≡ GQ` con `max_dP ≈ 5e-11 kN`. |
| [semana04.md](semana04.md) | 4 | **Unity como postprocesador estructural**: esfuerzos completos por elementTag (9 coeficientes, 315 elems B / 343 A), metadatos y ejes locales, panel de consulta por clic (viga/columna/muro), diagramas 3D y ventana 2D, **ventana P–M** (envolvente + balanceado + `M_cap(P_d)` + D/C) y cadena de trazabilidad completa tag 41 → JSON → panel → P–M. |
| [semana05.md](semana05.md) | 5 | Modelo "de la hoja al teléfono": **MOD1** sobrecarga `SC_PISO` 3,0→4,0 (Q 11 029,6→14 423,3 kN) y **MOD2** muro t 0,60→0,70 (P₀_aci 38 645,8→44 829,6; D/C 0,74→0,79) con flujo **editar → correr → "Recargar JSON"**; superposición interactiva G+Q≡GQ/G+EX/G+Q+EX (`|superpuesta − OpenSees| ≈ e-11`); sidequest "carga móvil" documentada (NO implementada en v1); **build Android** (`Tools/MCOC/Build Android`) y **actualización de los datos en el teléfono sin recompilar** (`adb push` a `persistentDataPath`). |
| [supuesto_armado_edificio_A.md](supuesto_armado_edificio_A.md) | Anexo | Hipótesis de armado y materiales del Edificio A (f'c 30 MPa G35, columna 70×70 8Ø28, recetas de muros) — supuesto documentado para el catálogo. |
| [semana06.md](semana06.md) | 6 | **Corrección del signo de `Mz(x)`** (era `Mz + Vy·x + Wy·x²/2`; correcto `Mz − Vy·x − Wy·x²/2`, porque `dMz/dx = −Vy`) en las 3 fórmulas del visor, con evidencia contra OpenSees (A tag 14 GQ: **−672,32 → +219,38 kN·m** vs **+219**); **cierre `Mz(L) = −Mz_j`** añadido a A y B; el mismo bug corregido en `src/secciones/diagramas.py` con `dMz` en `_cierre_verticales()`. **App AR de inspección en obra completa**: `src/ar/` genera `ar_elementos.json` (tags 14/26 columnas con P–M, 134 viga) desde los datos ya verificados, 12 tests propios (**suite 138 → 150**); escena `AR_Inspeccion.unity` + 5 guiones en `Assets/Scripts/AR/`, shader propio sin URP, AR Foundation 4.2.0 y build Android con API 24 / IL2CPP ARM64 / OpenGLES3 / ARCore. El APK **no se genera en esta máquina** (falta Android Build Support, §8.3). |

Figuras de apoyo: `reports/fig/` (envolventes de muros/columnas, superposición, mosaico).

## Cómo correr el pipeline (regenerar resultados + visor)

1. **Python** — desde la raíz del proyecto regenera análisis, fusión y sincroniza
   `StreamingAssets` de ambos proyectos Unity (sin abrir ventanas):

   ```
   python src\complejo.py --sin-visualizar
   # (la variante sin flag abre además la visualización matplotlib + HTML)
   ```

   El pipeline corre el análisis lineal de A y B (casos G/Q/GQ/EX/EY), verifica
   equilibrio y superposición, fusiona en `results/edificio_completo.json`,
   genera el contrato sólido `results/edificio_solido.json` y lo copia a
   `unity/EdificioSolidoUnity/Assets/StreamingAssets/`.

2. **Visor Unity** — abrir `unity/EdificioSolidoUnity` (Unity 2022.3 LTS / 2021.3).
   La escena `Assets/Scenes/Main.unity` se prepara sola al primer arranque
   (`Tools/MCOC/Preparar escena Main`). Acceso directo:
   `abrir_unity_solido.cmd` (abre el proyecto). El botón **"Recargar JSON"**
   re-lee `StreamingAssets/edificio_completo.json` sin cerrar el editor.

3. **Suite de protección**:

   ```
   python -m pytest tests/ -q    # → 150 passed
   ```

## Cómo generar el build Android (APK)

Desde `unity/EdificioSolidoUnity` con el módulo **Android Build Support**
instalado (SDK/NDK/JDK) — *Unity Hub → Edit → Installs → Android Build Support*.
Hay **dos** APKs distintos:

1. **`Tools/MCOC/Build Android visor`** → `build/EdificioComplejo_MCOC.apk`,
   package **`com.mcoc.edificiocomplejo`**, solo `Assets/Scenes/Main.unity`,
   `bundleVersion 1.0`, IL2CPP. Es el visor clásico, **sin AR Foundation**.

2. **`Tools/MCOC/Build Android AR`** → `build/EdificioComplejo_MCOC_AR.apk`,
   package **`com.mcoc.edificiocomplejo.ar`**, escenas
   `AR_Inspeccion.unity` (índice 0) + `Main.unity`, **min SDK 24**,
   **IL2CPP ARM64**, **OpenGLES3**, loader **ARCore** y permiso de cámara.

Los dos scripts llaman a `MCOCXRSetup.ConfigurarAndroid()`, que activa el
`ARCoreLoader` en *XR Plug-in Management* solo para el target Android.

## Cómo actualizar los DATOS en el teléfono (sin recompilar el APK)

El APK lleva el **C# compilado y la escena**, así que el **diseño** solo cambia
recompilando. Los **datos** no: el visor busca primero
`Application.persistentDataPath` (`/sdcard/Android/data/com.mcoc.edificiocomplejo/files/`),
que es escribible por `adb`.

```
powershell -ExecutionPolicy Bypass -File scripts\subir_json_telefono.ps1
```

El script empuja `StreamingAssets\edificio_completo.json` y verifica con `ls -l`
lo que quedó en el dispositivo. Después, en el teléfono, se aprieta
**"Recargar JSON"** y el panel confirma `Fuente: telefono (actualizable)` con la
fecha. Con `-Borrar` (o con el botón **"Usar JSON del APK"** del panel) se vuelve
al JSON embebido. Detalle en `reports/semana05.md` §6.2.

> **Pendiente de hardware:** el APK y las capturas de Play
> (`[CAPTURA: …]` en semana04/05) se completan a mano en Unity. La app AR exige
> además un **teléfono con ARCore** para validar la colocación — ver los
> pendientes en `docs/bitacora.md`.