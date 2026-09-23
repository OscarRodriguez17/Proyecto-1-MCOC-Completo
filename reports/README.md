# ÍNDICE DE ENTREGA — Proyecto 1 MCOC (Complejo de Ingeniería A+B)

> Repositorio: `https://github.com/OscarRodriguez17/Proyecto-1-MCOC-Completo.git`
> Rama `master` — Commit + tag a evaluar: consultar `docs/bitacora.md` (sesión final).
> Suite de verificación: `python -m pytest tests/ -q` → **134 passed**.

## Reportes semanales

| Archivo | Semana | Qué cubre |
|---|---|---|
| [semana03.md](semana03.md) | 3 | Motor de secciones P–M (`src/secciones`) y superposición §3: envolventes de 17 puntos por fibra (malla 12×12) para 40 columnas + 60 muros del B y catálogo del A; punto balanceado; **demanda–capacidad D/C por elemento** (muro crítico `muro0.60x2.91`, EX/EY) y verificación `G+Q ≡ GQ` con `max_dP ≈ 5e-11 kN`. |
| [semana04.md](semana04.md) | 4 | **Unity como postprocesador estructural**: esfuerzos completos por elementTag (9 coeficientes, 315 elems B / 343 A), metadatos y ejes locales, panel de consulta por clic (viga/columna/muro), diagramas 3D y ventana 2D, **ventana P–M** (envolvente + balanceado + `M_cap(P_d)` + D/C) y cadena de trazabilidad completa tag 41 → JSON → panel → P–M. |
| [semana05.md](semana05.md) | 5 | Modelo "de la hoja al teléfono": **MOD1** sobrecarga `SC_PISO` 3,0→4,0 (Q 11 029,6→14 423,3 kN) y **MOD2** muro t 0,60→0,70 (P₀_aci 38 645,8→44 829,6; D/C 0,74→0,79) con flujo **editar → correr → "Recargar JSON"**; superposición interactiva G+Q≡GQ/G+EX/G+Q+EX (`|superpuesta − OpenSees| ≈ e-11`); sidequest "carga móvil" documentada (NO implementada en v1); **build Android** (`Tools/MCOC/Build Android`). |
| [supuesto_armado_edificio_A.md](supuesto_armado_edificio_A.md) | Anexo | Hipótesis de armado y materiales del Edificio A (f'c 30 MPa G35, columna 70×70 8Ø28, recetas de muros) — supuesto documentado para el catálogo. |

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
   python -m pytest tests/ -q    # → 134 passed
   ```

## Cómo generar el build Android (APK)

Desde `unity/EdificioSolidoUnity` con el módulo **Android Build Support**
instalado (SDK/NDK/JDK):

1. Menú **`Tools/MCOC/Preparar escena Main`** (deja escena + cámara + StickModel).
2. Menú **`Tools/MCOC/Build Android`** → compila
   `assets/scenes/Main.unity` a `build/EdificioComplejo_MCOC.apk` con
   package **`com.mcoc.edificiocomplejo`**, **min SDK 22**, **landscape
   (LandscapeLeft)**, `bundleVersion 1.0`, IL2CPP.
3. Instalar el APK en el teléfono (los datos van embebidos en `StreamingAssets`,
   sin internet).

> El APK y las capturas de Play (`[CAPTURA: …]` en semana04/05) se completan a
> mano en Unity — ver pendientes en `docs/bitacora.md`.