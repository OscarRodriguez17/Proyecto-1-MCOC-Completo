"""Punto de entrada del COMPLEJO (Edificios A y B).

Analiza cada edificio de forma independiente (OpenSees), fusiona ambos en
`results/edificio_completo.json` y genera las visualizaciones lado a lado.

Uso:
    python src\\complejo.py                 # analiza A y B, fusiona, visualiza
    python src\\complejo.py --offset-b 70   # separacion del Edificio B en X [m]
    python src\\complejo.py --sin-visualizar
"""
import argparse
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
B3 = os.path.join(AQUI, "benchmark_3d")
sys.path.insert(0, B3)
sys.path.insert(0, AQUI)

import fusionar


def _sincronizar_unity():
    """Copia los JSON de results/ al StreamingAssets del proyecto Unity, si
    existe. Evita que Unity cargue una copia vieja tras regenerar el modelo."""
    import shutil
    results = os.path.abspath(os.path.join(AQUI, "..", "results"))
    sa = os.path.abspath(os.path.join(
        AQUI, "..", "unity", "EdificioComplejoUnity",
        "Assets", "StreamingAssets"))
    if not os.path.isdir(sa):
        return
    copiados = []
    for nombre in ("edificio_completo.json", "modelo_resultados.json"):
        src = os.path.join(results, nombre)
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(sa, nombre))
            copiados.append(nombre)
    if copiados:
        print("[UNITY] StreamingAssets (lineas) actualizado:", ", ".join(copiados))


def _exportar_y_sincronizar_solido(offset_b_x):
    """Genera el JSON del visor SOLIDO (columnas/vigas/muros 3D) y lo copia al
    StreamingAssets del proyecto Unity solido, si existe."""
    import shutil
    import exportar_unity_solido
    exportar_unity_solido.exportar(offset_b_x=offset_b_x)
    results = os.path.abspath(os.path.join(AQUI, "..", "results"))
    sa = os.path.abspath(os.path.join(
        AQUI, "..", "unity", "EdificioSolidoUnity",
        "Assets", "StreamingAssets"))
    src = os.path.join(results, "edificio_solido.json")
    if os.path.isdir(sa) and os.path.isfile(src):
        # el renderer solido lee "edificio_completo.json" de SU StreamingAssets
        shutil.copy2(src, os.path.join(sa, "edificio_completo.json"))
        print("[UNITY] StreamingAssets (solido) actualizado: edificio_completo.json")


def _enriquecer_secciones_solido():
    """Overlay ADITIVO (Semana 03): inyecta catalogo P-M, demandas y la
    etiqueta `seccion` por elemento en el contrato del visor solido, si el
    cache de secciones existe. Es opcional y NUNCA debe tumbar el pipeline."""
    import shutil
    results = os.path.abspath(os.path.join(AQUI, "..", "results"))
    cache = os.path.join(results, "secciones_semana03.json")
    if not os.path.isfile(cache):
        print("[SECCIONES] sin cache (results/secciones_semana03.json); "
              "se omite el overlay P-M")
        return
    try:
        from secciones import exportar_unity as overlay
        overlay.exportar(verbose=False)
        sa = os.path.abspath(os.path.join(
            AQUI, "..", "unity", "EdificioSolidoUnity",
            "Assets", "StreamingAssets"))
        if os.path.isdir(sa):
            src = os.path.join(results, "edificio_solido.json")
            shutil.copy2(src, os.path.join(sa, "edificio_completo.json"))
        print("[SECCIONES] overlay P-M inyectado en edificio_solido.json")
    except Exception as exc:  # aditivo: no afecta al resto del pipeline
        print("[SECCIONES] overlay omitido:", exc)


def main():
    ap = argparse.ArgumentParser(description="Complejo de Ingenieria A+B")
    ap.add_argument("--offset-b", type=float, default=60.0,
                    help="Offset X [m] del Edificio B (default 60)")
    ap.add_argument("--sin-visualizar", action="store_true")
    args = ap.parse_args()

    fusionar.fusion(offset_b_x=args.offset_b)
    _sincronizar_unity()
    _exportar_y_sincronizar_solido(args.offset_b)
    _enriquecer_secciones_solido()
    if not args.sin_visualizar:
        import visualizar_complejo
        import visualizar_complejo_html
        visualizar_complejo.dibujar()
        visualizar_complejo_html.main()


if __name__ == "__main__":
    main()