"""
Versión ligera: extrae información clave de TODOS los planos DXF (38 archivos).
Enfoque: capas, MTEXT (especificaciones), TEXT (rótulos), INSERT (bloques), DIMENSION.
NO extrae polylines/círculos/líneas individuales (solo cuenta).
"""
import ezdxf
import os
from collections import Counter

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DXF_DIR = r"C:\Users\Usuario\Desktop\Planos DXF"
OUT_PATH = os.path.join(_SCRIPT_DIR, "..", "docs", "info_planos_dxf.txt")

PLANOS = {
    "2017_67-000.dxf": ("DETALLE", "Detalles típicos - Trabas y mallas de fundación"),
    "2017_67-001.dxf": ("DETALLE", "Detalles típicos - Contención y juntas"),
    "2017_67-002.dxf": ("DETALLE", "Detalles típicos - Refuerzo y conexiones"),
    "2017_67-100.dxf": ("PLANTA", "Planta de fundaciones y corte típico"),
    "2017_67-101.dxf": ("PLANTA", "Planta cielo piso 1° + cielo 1° subterráneo (PLANTA PRINCIPAL)"),
    "2017_67-102.dxf": ("PLANTA", "Plantas cielo pisos 2° y 3°"),
    "2017_67-103.dxf": ("PLANTA", "Planta cielo piso 4° (techo)"),
    "2017_67-200-I.dxf": ("ARMADURA", "Malla de armadura inferior - Fundaciones"),
    "2017_67-200-S.dxf": ("ARMADURA", "Malla de armadura superior - Fundaciones"),
    "2017_67-201.dxf": ("ARMADURA", "Armadura - Losa piso 1°"),
    "2017_67-202-I.dxf": ("ARMADURA", "Armadura inferior - Losa piso 2°"),
    "2017_67-202-S.dxf": ("ARMADURA", "Armadura superior - Losa piso 2°"),
    "2017_67-203.dxf": ("ARMADURA", "Armadura - Losa piso 3°"),
    "2017_67-204.dxf": ("ARMADURA", "Armadura - Losa piso 4° (techo)"),
    "2017_67-205.dxf": ("ARMADURA", "Armadura - Losa adicional"),
    "2017_67-300.dxf": ("ELEVACION", "Elevación Eje 1-1' (vista lateral)"),
    "2017_67-301.dxf": ("ELEVACION", "Elevación Eje 2-2a (vista lateral)"),
    "2017_67-302.dxf": ("ELEVACION", "Elevación Eje 3 (vista lateral)"),
    "2017_67-303.dxf": ("ELEVACION", "Elevación Eje A3 (fachada trasera)"),
    "2017_67-304.dxf": ("ELEVACION", "Elevación Eje A2 (fachada interior)"),
    "2017_67-305.dxf": ("CORTE", "Corte por eje E (sección transversal)"),
    "2017_67-306.dxf": ("CORTE", "Corte por eje F (sección transversal)"),
    "2017_67-307.dxf": ("CORTE", "Corte por eje G (sección transversal)"),
    "2017_67-308.dxf": ("CORTE", "Corte por eje H (sección transversal)"),
    "2017_67-309.dxf": ("CORTE", "Corte por eje I (sección transversal)"),
    "2017_67-310.dxf": ("ELEVACION", "Elevación Eje J (fachada - anexo metálico)"),
    "2017_67-400.dxf": ("VARIOS", "Plano 400"),
    "2017_67-401.dxf": ("VARIOS", "Plano 401"),
    "2017_67-402.dxf": ("VARIOS", "Plano 402"),
    "2017_67-500.dxf": ("ESCALERA", "Escalera típica - Planta"),
    "2017_67-501.dxf": ("ESCALERA", "Escalera típica - Desarrollo y niveles"),
    "2017_67-502.dxf": ("ESCALERA", "Escalera típica - Detalle"),
    "2017_67-503.dxf": ("ESCALERA", "Escalera típica - Refuerzo"),
    "2017_67-600.dxf": ("RESUMEN", "Plantas resumen + Elevaciones eje EA/EB"),
    "2017_67-700.dxf": ("CARGA", "Cuadro de cargas de diseño (SC, PM.adic, PP)"),
    "2017_67-800.dxf": ("METALICO", "Elevación eje con vigas metálicas V.M. - Perno Nelson (1)"),
    "2017_67-801.dxf": ("METALICO", "Elevación eje con vigas metálicas V.M. - Perno Nelson (2)"),
    "2017_67-802.dxf": ("METALICO", "Elevación eje con vigas metálicas V.M. - Perno Nelson (3)"),
}

# Capas que no interesan para el texto
SKIP_LAYERS = {"DEFPOINTS"}
SKIP_TEXT_PREFIX = ("COLOR", "PRINT", "PLOT", "SCALE", "LINEWEIGHT",
                    "PEN.NO", "AUTOMATIC", "PARA FORMATO", "PEN STYLE")


def extraer_info_rapido(ruta_dxf):
    """Extracción ligera: solo lo esencial."""
    try:
        doc = ezdxf.readfile(ruta_dxf)
        msp = doc.modelspace()
    except Exception as e:
        return {"error": str(e)}

    n_capas = len(list(doc.layers))
    entidades = 0
    tipos = Counter()
    textos = []
    mtextos = []
    dims = 0
    inserts = Counter()
    capas_conenido = {}

    for entity in msp:
        entidades += 1
        etype = entity.dxftype()
        tipos[etype] += 1
        layer = entity.dxf.layer if hasattr(entity.dxf, 'layer') else "?"

        if layer not in capas_conenido:
            capas_conenido[layer] = 0
        capas_conenido[layer] += 1

        if etype == "TEXT":
            txt = entity.dxf.text if hasattr(entity.dxf, 'text') else ""
            if (txt.strip() and layer not in SKIP_LAYERS
                    and not any(txt.startswith(p) for p in SKIP_TEXT_PREFIX)):
                pos = entity.dxf.insert if hasattr(entity.dxf, 'insert') else None
                textos.append((layer, txt.strip()[:200],
                              f"({pos.x:.0f},{pos.y:.0f})" if pos else ""))

        elif etype == "MTEXT":
            txt = entity.text if hasattr(entity, 'text') else ""
            if txt.strip() and len(txt.strip()) > 3:
                pos = entity.dxf.insert if hasattr(entity.dxf, 'insert') else None
                mtextos.append((layer, txt.strip()[:300],
                               f"({pos.x:.0f},{pos.y:.0f})" if pos else ""))

        elif etype == "DIMENSION":
            dims += 1

        elif etype == "INSERT":
            name = entity.dxf.name if hasattr(entity.dxf, 'name') else "?"
            inserts[name] += 1

    return {
        "n_capas": n_capas, "entidades": entidades, "tipos": tipos,
        "textos": textos, "mtextos": mtextos, "dims": dims,
        "inserts": inserts, "capas": capas_conenido,
    }


def main():
    lineas = []
    L = lineas.append

    L("=" * 80)
    L("CONTENIDO DE LOS PLANOS — EDIFICIO DE INGENIERÍA (Proyecto P1 MCOC)")
    L("Serie: 2017_67-XXX (38 planos DXF) | Conversión DWG→DXF 2018 vía ODA")
    L("=" * 80)
    L("")
    L("NOTA: Unidad de dibujo = CENTÍMETROS (1 cm = 1 unidad).")
    L("Cotas en mm (ej: 360 = 3.60 m entre ejes).")
    L("")

    # Agrupar
    cats = {}
    for nombre, (cat, desc) in PLANOS.items():
        cats.setdefault(cat, []).append((nombre, desc))

    orden = ["PLANTA", "ELEVACION", "CORTE", "ARMADURA", "METALICO",
             "ESCALERA", "CARGA", "RESUMEN", "DETALLE", "VARIOS"]

    for cat in orden:
        if cat not in cats:
            continue
        L(f"\n{'#' * 80}")
        L(f"# CATEGORÍA: {cat}")
        L(f"{'#' * 80}")

        for nombre, desc in cats[cat]:
            ruta = os.path.join(DXF_DIR, nombre)
            if not os.path.exists(ruta):
                L(f"\n  {nombre}: NO ENCONTRADO")
                continue

            print(f"  {nombre}...", end=" ", flush=True)
            info = extraer_info_rapido(ruta)
            print(f"OK")

            if "error" in info:
                L(f"\n  {nombre}: ERROR - {info['error']}")
                continue

            kb = os.path.getsize(ruta) / 1024
            L(f"\n{'=' * 70}")
            L(f"PLANO: {nombre}")
            L(f"DESCRIPCIÓN: {desc}")
            L(f"Tamaño: {kb:.0f} KB | Capas: {info['n_capas']} | "
              f"Entidades: {info['entidades']} | Dims: {info['dims']}")
            L(f"Distribución: {', '.join(f'{t}:{c}' for t,c in info['tipos'].most_common(8))}")

            # Capas más activas
            top_capas = sorted(info['capas'].items(), key=lambda x: -x[1])[:15]
            L(f"Capas más activas:")
            for cn, cc in top_capas:
                L(f"  {cn}: {cc} entidades")

            # Bloques insertados
            if info['inserts']:
                L(f"Bloques insertados ({sum(info['inserts'].values())} total):")
                for bn, bc in info['inserts'].most_common(20):
                    L(f"  {bn}: {bc}x")

            # Textos
            if info['textos']:
                L(f"Textos TEXT ({len(info['textos'])}):")
                for layer, txt, pos in info['textos'][:60]:
                    L(f"  [{layer}] {pos}: \"{txt}\"")

            # MTEXT (los más importantes - especificaciones)
            if info['mtextos']:
                L(f"MTEXT - Especificaciones ({len(info['mtextos'])}):")
                for layer, txt, pos in info['mtextos'][:50]:
                    txt_clean = txt.replace('\n', ' | ').replace('\P', ' | ')
                    L(f"  [{layer}] {pos}: \"{txt_clean}\"")

    # Resumen estructural
    L(f"\n\n{'=' * 80}")
    L("RESUMEN ESTRUCTURAL DEL EDIFICIO (extraído de planos y bitácora)")
    L("=" * 80)
    L("""
UBICACIÓN: Edificio de Ingeniería (Universidad)
PLANOS: Serie 2017_67-XXX (38 planos DWG/DXF)

GEOMETRÍA GENERAL:
- Retícula X (plano 101): E–I' = 0/10/20/30/40/45 m + J = 50 m (anexo metálico)
- Retícula Y: 0 / 2.31 / 7.25 / 12.25 / 16.15 m
- Panelaje X: [10, 10, 10, 10, 5] m | Panelaje Y: [2.31, 4.94, 5.00, 3.90] m
- Área de planta: 726.75 m² (principal) + 80.75 m² (anexo I'→J)
- 4 niveles + anexo metálico en 2 niveles superiores

NIVELES:
- Base (sello fundación): −4.21 m (empotramiento)
- Radier piso 1°: −0.05 m (altura 4.16 m)
- Cielo piso 2°: +3.91 m | Cielo piso 3°: +7.87 m | Techo: +11.83 m (3.96 m c/u)

MATERIALES:
- Hormigón G35: f'c=35 MPa, Ec=27,806 MPa, γc=25 kN/m³
- Acero (anexo): E=200 GPa, G=77 GPa, γ=78.5 kN/m³
- Losa M.H.A. e=15 cm

ELEMENTOS:
- Pilares P.70×70: 18 por piso (E…I' × A3,A2,A1)
- Vigas V.60/80 (b=0.60 h=0.80)
- Anexo metálico I'→J: Columnas P.M. 300×300×20 + Vigas V.M. 300×300×5
- 5 muros: ME-32, MI-32, MI-12, M1c-E, M2a

CARGAS:
- q_G = 6.30 kN/m² | q_Q = 2.50 kN/m² | α = 0.10

VOLADIZOS EN Y (detectados en cortes):
- Lado A3 (trasera): P.M. 300×300×20 en ejes F,G,H,I' a −3.8/−4.9 m
- Lado A1 (frente): P.M.I. y V.M. en ejes G,H,I' (verificar)
""")
    L("=" * 80)
    L("FIN DEL DOCUMENTO")
    L("=" * 80)

    resultado = "\n".join(lineas)
    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write(resultado)
    print(f"\nGenerado: {OUT_PATH}")
    print(f"Tamaño: {len(resultado):,} chars, {resultado.count(chr(10)):,} líneas")


if __name__ == "__main__":
    main()
