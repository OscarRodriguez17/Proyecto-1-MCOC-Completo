# Proyecto 1 MCOC — Laboratorio estructural digital 3D del Complejo de Ingeniería (Edificios A y B)

**Grupo 5:** Nicolás Letelier · Oscar Rodríguez · Pablo Arancibia — Universidad de los Andes.

El proyecto modela en OpenSeesPy (lineal elástico 3D) dos edificios reales a partir de sus planos DXF:

- **Edificio A**: planos 2017_67, hormigón G35, con anexo y voladizos metálicos.
- **Edificio B**: planos 2024_22, hormigón H30.

Sobre esos modelos se agregaron:

- un motor de secciones de fibras (M–φ, P–M y demanda–capacidad);
- un **visor Unity**, que funciona como pre y postprocesador;
- una **app Android de realidad aumentada** que superpone los diagramas de esfuerzos de 9 elementos del Edificio A sobre la estructura real.

**Informe técnico final:** [`reports/final.pdf`](reports/final.pdf). El mismo contenido está en Markdown en [`reports/final.md`](reports/final.md) y la fuente LaTeX en `reports/final.tex`.

---

## 1. Dependencias y versiones

| Componente | Versión usada | Notas |
|---|---|---|
| Python | **3.12** (también probado con 3.11) | Windows del grupo y Linux |
| OpenSeesPy | **3.8.0.0** (fijada en `requirements.txt`) | La suite también pasa con 3.7.1.2 |
| numpy / matplotlib / pytest / ezdxf | ≥ 2.5 / ≥ 3.11 / ≥ 9.1 / 1.4.4 | Ver `requirements.txt` |
| Unity Editor | **2022.3.62f3** (LTS) | Con el módulo **Android Build Support** (SDK, NDK y OpenJDK) |
| AR Foundation / ARCore XR Plugin | **4.2.0 / 4.2.0** | Además: XR Plug-in Management 4.4.0 y Test Framework 1.1.33 |
| Teléfono (app AR) | Android 7.0+ (API 24), **ARM64**, con **ARCore** | Si el teléfono tiene Depth API, el modo «apuntar a la viga» funciona mejor |

> **macOS:** openseespy no trae binarios nativos para Apple Silicon. Hay que usar un entorno x86_64, por ejemplo Miniforge `osx-64`. El grupo compila los APK en Windows.

## 2. Instalación

```powershell
# Desde la raíz del proyecto (Windows / PowerShell)
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

En Linux o macOS se activa con `source .venv/bin/activate`.

## 3. Ejecutar el análisis y generar los resultados

```powershell
# Pipeline completo (unos segundos):
#   analiza A y B (G, Q, GQ, EX, EY), verifica equilibrio y superposición,
#   fusiona los dos edificios y genera los contratos de Unity y de la app AR
python src\complejo.py --sin-visualizar

# Contrato AR con los 9 elementos de la app (complejo.py exporta por defecto solo 3)
python src\ar\exportar_ar.py --tags 134 14 26 354 350 337 340 342 105
Copy-Item results\ar_elementos.json unity\EdificioSolidoUnity\Assets\StreamingAssets\ -Force

# La misma ejecución, pero abriendo además la vista matplotlib + HTML
python src\complejo.py

# Figuras del informe final (blanco y negro, en reports/fig/final_*.png)
python scripts\final_figuras.py

# Compilar el informe (requiere una distribución LaTeX: MiKTeX, TeX Live u Overleaf)
cd reports
pdflatex final.tex
pdflatex final.tex
```

Al terminar, el pipeline imprime `equilibrio=OK superposición=OK` para cada edificio y deja estos archivos:

| Archivo | Contenido |
|---|---|
| `results/modelo_resultados.json` | Edificio A: geometría, cargas, reacciones, desplazamientos y esfuerzos |
| `results/modelo_resultados_b.json` | Edificio B, con la misma estructura |
| `results/edificio_completo.json` | Contrato fusionado A+B (B desplazado +60 m en x) |
| `results/edificio_solido.json` | Contrato del visor: catálogo P–M, demandas, esfuerzos completos y metadatos por `elementTag` |
| `results/ar_elementos.json` | Contrato de la app AR, caso GQ: 9 elementos (14, 26, 105, 134, 337, 340, 342, 350, 354) |
| `results/secciones_semana03.json`, `secciones_semana04.json` | Cachés del motor de secciones: curvas M–φ y P–M, y esfuerzos completos |

El pipeline copia automáticamente los JSON a `unity/EdificioSolidoUnity/Assets/StreamingAssets/` (`edificio_completo.json` y `ar_elementos.json`).

**Comprobación de reproducibilidad.** El 7 de octubre de 2026 se ejecutó `python src/complejo.py --sin-visualizar` en un entorno nuevo con openseespy 3.8.0.0, seguido de `exportar_ar.py --tags ...`. Los archivos de resultados salieron con el mismo contenido que los del repositorio. Solo puede cambiar el fin de línea: en Windows es CRLF.

### Cambiar el modelo y regenerar el motor de secciones

```powershell
# 1) editar el dato (por ejemplo src\edificio_b\cargas.py o src\benchmark_3d\datos_edificio.py)
python src\complejo.py --sin-visualizar                  # reanaliza A y B y regenera los contratos
python scripts\semana05_refrescar_caches.py --force-curvas  # demandas y curvas P–M nuevas, si hacen falta
python scripts\semana04_esfuerzos_completos_run.py --recalcular
python src\ar\exportar_ar.py --tags 134 14 26 354 350 337 340 342 105   # contrato AR de 9
# 2) en Unity: botón «Recargar JSON»
```

> **Ojo:** `scripts\semana03_run.py --recalcular` parte con la caché vacía y borra curvas de B (bug conocido, ver el informe §19). Para refrescar solo las combinaciones hay que usar `superposicion.verificar` sobre la caché cargada.

## 4. Abrir el visor (Unity)

1. Unity Hub → **Add** → `unity/EdificioSolidoUnity`, con el Editor 2022.3.62f3. También sirve hacer doble clic en `abrir_unity_solido.cmd`.
2. Abrir la escena `Assets/Scenes/Main.unity` y presionar **Play**. La escena pesa unos 10 KB porque se reconstruye desde `StreamingAssets/edificio_completo.json`.
3. Controles:
   - **Navegación:** botón derecho para rotar (sensibilidad ajustable), botón central para desplazar, rueda para zoom; teclas `0`/`R` vuelven a la vista inicial.
   - **Capas:** columnas, vigas, muros, apoyos, cargas G/Q/sismo, tributaria y deformada (Base/G/GQ/EX/EY, con amplificación).
   - **Consulta:** «Consulta de elemento (click izquierdo)» abre el panel del elemento, con la ventana de diagramas y la ventana P–M (incluye el D/C).
   - **«Recargar JSON»:** vuelve a leer los datos sin cerrar el editor.
4. `unity/EdificioComplejoUnity/` es el visor antiguo. **No es el que se entrega.**

## 5. Compilar las apps móviles (Android)

Se compilan desde `unity/EdificioSolidoUnity` y requieren Android Build Support:

| Menú | APK | Paquete | Contenido |
|---|---|---|---|
| **Tools → MCOC → Build Android AR** | `build/EdificioComplejo_MCOC_AR.apk` | `com.mcoc.edificiocomplejo.ar` | App AR (`AR_Inspeccion.unity` + `Main.unity`), minSdk 24, IL2CPP ARM64, OpenGLES3, ARCore |
| Tools → MCOC → Build Android visor | `build/EdificioComplejo_MCOC.apk` | `com.mcoc.edificiocomplejo` | Visor clásico, sin AR |

`Tools → MCOC → Configurar AR` deja configurado el `ARCoreLoader` para Android. Los build scripts lo llaman solos.

**Instalación:** `adb install -r build\EdificioComplejo_MCOC_AR.apk`, o copiar el APK al teléfono.

**Uso de la app AR:**
1. Elegir el elemento de la lista: 14, 26, 105, 134, 337, 340, 342, 350 o 354.
2. Elegir un modo de colocación:
   - «Encuadrar: 4 esquinas»: se tocan las 4 esquinas de la cara, en cualquier orden, y el diagrama queda plano y fijo;
   - «Marcar base (anillo)»;
   - para las vigas, «Marcar viga: apuntar a ella» o «Marcar viga: pie de columnas».
3. Si los textos no se leen bien en el teléfono, usar «Panel 2D».

**Datos del visor sin recompilar:** `powershell -ExecutionPolicy Bypass -File scripts\subir_json_telefono.ps1` hace un `adb push` del JSON a `persistentDataPath`. Después, en el teléfono, se presiona «Recargar JSON». Con `-Borrar` se vuelve al JSON que trae el APK.

## 6. Tests

```powershell
python -m pytest tests -q        # → 169 passed
```

Usar la ruta `tests`: `para_entrega/` tiene una copia antigua de un test con el mismo nombre de módulo.

**Unity:** Window → General → **Test Runner** → **EditMode** → **Run All** → **153/153**. Cubren la colocación AR, el encuadre, el piso, los rótulos, el panel 2D, la interfaz y la configuración XR de Android.

## 7. Estructura

```
├── data/        esquema JSON de entrada (nodos, elementos, materiales, cargas)
├── docs/        bitácora (bitacora.md), fichas de geometría, extracción de los DXF
├── reports/     final.pdf / final.md / final.tex + informes semanales 03–06 + fig/
├── results/     contratos y resultados JSON, figuras PNG/HTML
├── scripts/     runners del motor de secciones, refresco de cachés, figuras, adb
├── src/
│   ├── complejo.py       punto de entrada: A + B + fusión + contratos
│   ├── benchmark_3d/     Edificio A (datos, construir, voladizos, vigas secundarias,
│   │                      pilares metálicos, cargas, analizar, esfuerzos, fusionar)
│   ├── edificio_b/       Edificio B (paquete)
│   ├── secciones/        motor de fibras: materiales, sección, M–φ, P–M, acero, D/C, superposición
│   └── ar/               exportador del contrato de la app AR
├── tests/       169 tests de pytest
└── unity/EdificioSolidoUnity/   visor + app AR (Assets/Scripts, Assets/Scripts/AR, Assets/Editor)
```

## 8. Resultados de referencia (modelo final)

| Magnitud | Edificio A | Edificio B |
|---|---|---|
| Nodos / elementos | 206 / 354 | 235 (+5 maestros) / 350 |
| G / Q [kN] | 45 417,4 / 7 671,3 | 48 171,1 / 11 029,6 |
| Corte basal V = 0,10·W [kN] | 4 541,7 | 4 817,1 |
| Techo EX / EY [mm] | 8,48 / 2,35 | 16,00 / 19,77 |
| Superposición, máx. ΔR [kN] | 3,6e-11 | 6,0e-11 |

Elementos de la app AR, caso GQ:
- columnas 14 y 26: D/C = 0,19 y 0,19;
- viga 134: M = −220,0 / +211,9 / −265,3 kN·m (extremo i, centro, extremo j);
- pilar de acero 340: D/C = 0,02;
- muro 105: D/C = 0,04.

## 9. Entrega

- Rama `master`, tag **`entrega-final`**. El APK de la app AR (`MCOC_AR_EdificioComplejo.apk`) va adjunto al Release `entrega-final`.
- La historia de cada sesión está en `docs/bitacora.md`.
- Los informes semanales están en `reports/semana0X.md`.
