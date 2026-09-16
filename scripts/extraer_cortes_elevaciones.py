"""
Extrae el contenido de los bloques dentro de los planos de ELEVACIONES y CORTES
(300-310), donde está la información de los perfiles de voladizos.
Los TEXT/MTEXT de la geometría viven DENTRO de bloques (no en modelspace).
"""
import ezdxf
import os

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DXF_DIR = r"C:\Users\Usuario\Desktop\Planos DXF"
OUT_PATH = os.path.join(_SCRIPT_DIR, "..", "docs", "info_cortes_elevaciones.txt")

PLANOS = {
    "2017_67-300.dxf": "Elevación Eje 1-1' (vista lateral)",
    "2017_67-301.dxf": "Elevación Eje 2-2a (vista lateral)",
    "2017_67-302.dxf": "Elevación Eje 3 (vista lateral)",
    "2017_67-303.dxf": "Elevación Eje A3 (fachada trasera)",
    "2017_67-304.dxf": "Elevación Eje A2 (fachada interior)",
    "2017_67-305.dxf": "Corte por eje E (sección transversal)",
    "2017_67-306.dxf": "Corte por eje F (sección transversal)",
    "2017_67-307.dxf": "Corte por eje G (sección transversal)",
    "2017_67-308.dxf": "Corte por eje H (sección transversal)",
    "2017_67-309.dxf": "Corte por eje I (sección transversal)",
    "2017_67-310.dxf": "Elevación Eje J (fachada - anexo metálico)",
}

SKIP_PREFIX = ("COLOR", "PRINT", "PLOT", "SCALE", "LINEWEIGHT",
               "PEN.NO", "AUTOMATIC", "PARA FORMATO", "PEN STYLE",
               "M. KUPFER", "DICIEMBRE", "INDICADAS")


def texto_util(txt):
    t = txt.strip()
    if not t or len(t) < 2:
        return None
    if t.startswith(SKIP_PREFIX):
        return None
    t = t.replace("\\P", " | ").replace("\n", " | ")
    return t[:400]


def extraer_bloques_detallado(ruta):
    """Extrae textos de TODOS los bloques de un DXF."""
    doc = ezdxf.readfile(ruta)

    # 1. Bloques del documento (definiciones)
    info_bloques = []
    for block in doc.blocks:
        if block.name.startswith("*"):
            continue
        textos_bloque = []
        for e in block:
            if e.dxftype() == "TEXT":
                t = texto_util(e.dxf.text if hasattr(e.dxf, 'text') else "")
                if t:
                    textos_bloque.append(("TEXT", t))
            elif e.dxftype() == "MTEXT":
                t = texto_util(e.text if hasattr(e, 'text') else "")
                if t:
                    textos_bloque.append(("MTEXT", t))
            elif e.dxftype() == "INSERT":
                textos_bloque.append(("INSERT", e.dxf.name))
        if textos_bloque:
            info_bloques.append({
                "nombre": block.name,
                "entidades": sum(1 for _ in block),
                "textos": textos_bloque,
            })

    # 2. Modelspace con inserts recursivos (para ver qué bloques se usan y dónde)
    msp = doc.modelspace()
    inserts = []
    for e in msp:
        if e.dxftype() == "INSERT":
            pos = e.dxf.insert if hasattr(e.dxf, 'insert') else None
            inserts.append((e.dxf.name,
                           f"({pos.x:.0f},{pos.y:.0f})" if pos else "?"))

    return info_bloques, inserts


def main():
    L = []
    L.append("=" * 80)
    L.append("CORTES Y ELEVACIONES — CONTENIDO DE BLOQUES (perfiles de voladizos)")
    L.append("Los TEXT/MTEXT de estos planos viven dentro de BLOQUES, no en modelspace.")
    L.append("=" * 80)
    L.append("")

    for nombre, desc in PLANOS.items():
        ruta = os.path.join(DXF_DIR, nombre)
        print(f"  {nombre}...", end=" ", flush=True)
        try:
            bloques, inserts = extraer_bloques_detallado(ruta)
        except Exception as e:
            print(f"ERROR: {e}")
            L.append(f"\n{'=' * 70}")
            L.append(f"PLANO: {nombre} — {desc}")
            L.append(f"ERROR: {e}")
            continue
        print(f"OK ({len(bloques)} bloques con texto, {len(inserts)} inserts)")

        L.append(f"\n{'=' * 70}")
        L.append(f"PLANO: {nombre}")
        L.append(f"DESCRIPCIÓN: {desc}")
        L.append(f"Tamaño: {os.path.getsize(ruta)/1024:.0f} KB")
        L.append(f"Bloques con contenido de texto: {len(bloques)}")
        L.append(f"Bloques insertados en modelspace: {len(inserts)}")
        if inserts:
            L.append(f"  Inserts:")
            for bn, bp in inserts:
                L.append(f"    {bn} @ {bp}")
        L.append("")

        for b in sorted(bloques, key=lambda x: -x["entidades"]):
            L.append(f"  BLOQUE: {b['nombre']} ({b['entidades']} entidades)")
            for tipo, txt in b["textos"]:
                L.append(f"    {tipo}: \"{txt}\"")

    L.append("\n" + "=" * 80)
    L.append("FIN")
    L.append("=" * 80)

    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(L))
    print(f"\nGenerado: {OUT_PATH}")
    print(f"Tamaño: {len('\n'.join(L)):,} chars, {len(L):,} líneas")


if __name__ == "__main__":
    main()