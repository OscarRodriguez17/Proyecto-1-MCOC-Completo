# -*- coding: utf-8 -*-
"""
diagramas.py — Semana 04: esfuerzos COMPLETOS de TODOS los elementos (A+B).

Extiende de forma ADITIVA la semana 03: el bloque ``esfuerzos`` del contrato
sólido solo exportaba diagramas de VIGAS. Aquí se exporta el MISMO esquema
{L,N,Vy,Vz,T,My,Mz,Wy,Wz} para CADA elemento estructural (columnas, muros,
vigas y aspas) de cada edificio y en cada caso (G/Q/GQ/EX/EY), usando la misma
pasada validada ``esfuerzos._correr_caso`` — ningún resultado cambia, solo se
extiende la cobertura.

Convenciones (idénticas a la semana 03 / demandas):
  - Coeficientes en ejes LOCALES del elemento; y en el nudo i (I) del elemento.
  - ``N = Ni``: en este modelo ``Ni > 0`` = COMPRESIÓN (igual que ``P`` de las
    demandas y de las curvas P–M del visor).
  - Vigas con carga distribuida: ``Wy = 0``, ``Wz = -w`` (w = carga aplicada,
    tal cual las verificó la semana 03). Columnas/muros/aspas: ``Wy = Wz = 0``
    (su peso propio se aplica como carga NODAL, quedando los coeficientes
    lineales en el elemento).

Evaluación de diagramas en x ∈ [0, L] (paridad con ``edificio_b/esfuerzos.py``
y con el panel de Unity de la semana 03):

    N(x)  = -N                  (constante)
    Vz(x) = Vz + Wz·x
    My(x) = My + Vz·x + Wz·x²/2
    Vy(x) = Vy + Wy·x
    Mz(x) = Mz + Vy·x + Wy·x²/2
    T(x)  = T
    (ver ``evaluar``)

METADATOS por elemento (traza completa para el postprocesador Unity):
  - tipo de VISOR (`column`/`wall`/`vigas_x`/`vigas_y`), sección, material
    (G35 en A, H30 en B), longitud L;
  - nodos i/j con su RESTRICCIÓN efectiva:
      * ``empotrado``         nodo de base totalmente fijo;
      * ``maestro_diafragma`` nodo maestro del diafragma rígido del piso;
      * ``esclavo_diafragma`` nodo esclavo del diafragma rígido;
      * ``libre``             resto (puntas de voladizo, etc.);
  - ejes locales (x̂ de I→J, ẑ = proyección de (0,0,1) ⊥ x̂ con fallback
    (1,0,0), ŷ = ẑ×x̂): reproduce los ejes declarados con vecxz (1,0,0)/(0,0,1)
    de ambos edificios.

Verificación nueva (``verif``): cierre de los ELEMENTOS VERTICALES —con
``Wz = 0``— ``Vz(L) = -Vzj``, ``My(L) = -Myj`` y ``N` constante (``Ni+Nj≈0``)
más el equilibrio global ΣR + Σaplicada ≈ 0 por caso (igual que semana 03).

Unidades: m, kN, kN·m.  ADITIVO: no toca construir/analizar/esfuerzos de
semana 03; solo invoca ``_correr_caso`` y lee ``localForce``.
"""
import math
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import openseespy.opensees as ops

CASOS = ("G", "Q", "GQ", "EX", "EY")

TIPO_VISOR = {"pilar": "column", "muro": "wall"}
MATERIAL = {"B": "H30", "A": "G35"}
# Sección NOMINAL de viga por edificio (catálogo del visor sólido:
# geometria.secciones.viga de cada bloque).
VIGA_NOMINAL = {"B": "viga0.60x0.80", "A": "viga0.60x0.80"}


def _larga(n1, n2):
    x1, y1, z1 = ops.nodeCoord(n1)
    x2, y2, z2 = ops.nodeCoord(n2)
    return ((x2 - x1) ** 2 + (y2 - y1) ** 2 + (z2 - z1) ** 2) ** 0.5


def _viga_view(n1, n2):
    """Tipo de viga del VISOR (misma regla que exportar_unity_solido)."""
    x1, y1, _ = ops.nodeCoord(n1)
    x2, y2, _ = ops.nodeCoord(n2)
    return "vigas_x" if abs(x2 - x1) >= abs(y2 - y1) else "vigas_y"


# --------------------------------------------------------------------- ejes
def _ejes_locales(n1, n2, L):
    """x̂ = (J−I)/L; ẑ = proyección de (0,0,1) ⊥ x̂ (fallback (1,0,0)); ŷ = ẑ×x̂.

    Reproduce los ejes de OpenSees (vecxz=(1,0,0) en verticales, (0,0,1) en
    vigas/aspas) para orientar los diagramas My/Mz en el visor.
    """
    x1, y1, z1 = ops.nodeCoord(n1)
    x2, y2, z2 = ops.nodeCoord(n2)
    xh, yh, zh = (x2 - x1) / L, (y2 - y1) / L, (z2 - z1) / L
    pz = (-xh * zh, -yh * zh, 1.0 - zh * zh)          # proyecto (0,0,1)
    n = math.hypot(pz[0], pz[1], pz[2])
    if n < 1e-9:                                       # vertical: fallback X
        pz = (1.0 - xh * xh, -xh * yh, -xh * zh)
        n = math.hypot(pz[0], pz[1], pz[2])
    if n < 1e-9:
        pz = (1.0, 0.0, 0.0)
        n = 1.0
    zx, zy, zz = (pz[0] / n, pz[1] / n, pz[2] / n)
    yx = zy * zh - zz * yh                             # ŷ = ẑ × x̂
    yy = zz * xh - zx * zh
    yz = zx * yh - zy * xh
    return {"x": [xh, yh, zh], "y": [yx, yy, yz], "z": [zx, zy, zz]}


# -------------------------------------------------------------- restricciones
def _estado_nodo(tag, fijos, maestros, esclavos):
    if tag in fijos:
        return "empotrado"
    if tag in maestros:
        return "maestro_diafragma"
    if tag in esclavos:
        return "esclavo_diafragma"
    return "libre"


def _clasificar_b(M):
    """tag -> estado para el Edificio B (diafragma = TODO nodo no fijo ni
    maestro del nivel; empotrado = base)."""
    fijos = set(ops.getFixedNodes())
    maestros = set(M.masters.values())
    todos = set(ops.getNodeTags())
    esclavos = todos - fijos - maestros
    return {t: _estado_nodo(t, fijos, maestros, esclavos) for t in todos}


def _clasificar_a(modelo):
    """tag -> estado para el Edificio A.

    Esclavos = nodos de RETÍCULA (xi,yi,lvl<=1) + extremos de muro por nivel
    (exactamente los que entran en el rigidDiaphragm de construir.py). Los
    nodos que añaden los voladizos (aspas, puntas, raíces) quedan "libres".
    """
    fijos = set(ops.getFixedNodes())
    maestros = set(modelo["master"].values())
    esclavos = set()
    for clave, tag in modelo["nid"].items():
        if isinstance(clave, tuple) and len(clave) == 3 \
                and isinstance(clave[2], int) and clave[2] >= 1:
            esclavos.add(tag)
    for (wi, lvl, ep), tag in modelo["wall_nodes"].items():
        if isinstance(lvl, int) and lvl >= 1:
            esclavos.add(tag)
    todos = set(ops.getNodeTags())
    return {t: _estado_nodo(t, fijos, maestros, esclavos) for t in todos}


# ----------------------------------------------------------------- metadatos
def _metadatos(tipo_elem, etiquetas, material, nom_viga):
    """Mapa tag(str) -> metadatos para el visor.

    `tipo_elem`: tag -> 'viga'|'pilar'|'muro'|'aspa' (o 'brazo' que se omite).
    `etiquetas`: tag(str) -> 'col.*'/'muro.*' para pilar/muro (de semana 03).
    """
    out = {}
    for e in sorted(tipo_elem):
        t = tipo_elem[e]
        if t == "brazo":
            continue
        n1, n2 = ops.eleNodes(e)
        L = _larga(n1, n2)
        if math.isclose(L, 0.0):
            continue
        if t in ("pilar", "muro"):
            visor = TIPO_VISOR[t]
            info = etiquetas.get(str(e))
            if isinstance(info, dict):
                seccion = info.get("seccion")
            else:
                seccion = info
        else:
            visor = _viga_view(n1, n2)
            seccion = nom_viga
        out[str(e)] = {
            "tipo": visor,
            "seccion": seccion,
            "material": material,
            "L": float(L),
            "ejes_locales": _ejes_locales(n1, n2, L),
        }
    return out


def _agregar_nodos_y_estados(met, estados):
    """Añade a cada elemento de `met` sus nodos i/j y la restricción de cada
    extremo (empotrado / maestro_diafragma / esclavo_diafragma / libre)."""
    for k, reg in met.items():
        e = int(k)
        n1, n2 = ops.eleNodes(e)
        reg["nodos"] = {"ni": n1, "nj": n2,
                        "i": estados.get(n1, "libre"),
                        "j": estados.get(n2, "libre")}
    return met


# ----------------------------------------------------------------- esfuerzos
def _bloque_esfuerzos(tipo_elem, w_por_viga):
    """localForce de TODOS los elementos estructurales -> same schema semana03.

    Vigas llevan Wz=-w (paridad exacta con el bloque validado); el resto de
    tipos Wz=Wy=0 (peso propio nodal, coeficientes lineales).
    """
    out = {}
    for e in sorted(tipo_elem):
        t = tipo_elem[e]
        if t == "brazo":
            continue
        n1, n2 = ops.eleNodes(e)
        L = _larga(n1, n2)
        f = ops.eleResponse(e, "localForce")
        if f is None or len(f) < 12:
            continue
        Ni, Vyi, Vzi, Ti, Myi, Mzi, _Nj, _Vyj, _Vzj, _Tj, _Myj, _Mzj = f
        w = w_por_viga.get(e, 0.0) if t == "viga" else 0.0
        out[str(e)] = {
            "L": float(L),
            "N": float(Ni),
            "Vy": float(Vyi),
            "Vz": float(Vzi),
            "T": float(Ti),
            "My": float(Myi),
            "Mz": float(Mzi),
            "Wy": 0.0,
            "Wz": float(-w),
        }
    return out


def evaluar(coef, x):
    """Coeficientes -> esfuerzos en la posición x ∈ [0, L].

    Paridad exacta con el panel de la semana 03 y con las fórmulas de
    `edificio_b/esfuerzos.py` (N(x) = -N, etc.).
    """
    return {
        "N": -coef["N"],
        "Vy": coef["Vy"] + coef["Wy"] * x,
        "Vz": coef["Vz"] + coef["Wz"] * x,
        "T": coef["T"],
        "My": coef["My"] + coef["Vz"] * x + coef["Wz"] * x * x / 2.0,
        "Mz": coef["Mz"] + coef["Vy"] * x + coef["Wy"] * x * x / 2.0,
    }


# ---------------------------------------------------------------- verificación
def _cierre_verticales(tipo_elem, bloque):
    """Cierre en extremos de columnas/muros/aspas (Wz=0).

    Vz(L) = Vz == -Vzj; My(L) = My + Vz·L == -Myj; N constante: Ni + Nj ≈ 0.
    Devuelve (max_dN, max_dVz, max_dMy); tol relativa 1e-6·max(1,|ref|).
    """
    max_dN = max_dVz = max_dMy = 0.0
    for e in sorted(tipo_elem):
        t = tipo_elem[e]
        if t == "brazo" or t == "viga":
            continue
        f = ops.eleResponse(e, "localForce")
        if f is None or len(f) < 12:
            continue
        Ni, _Vyi, Vzi, _Ti, Myi, _Mzi, Nj, _Vyj, Vzj, _Tj, Myj, _Mzj = f
        L = _larga(*ops.eleNodes(e))
        VzL = Vzi                                   # Wz = 0
        MyL = Myi + Vzi * L
        max_dN = max(max_dN, abs(Ni + Nj) / max(1.0, abs(Nj)))
        max_dVz = max(max_dVz, abs(VzL - (-Vzj)) / max(1.0, abs(Vzj)))
        max_dMy = max(max_dMy, abs(MyL - (-Myj)) / max(1.0, abs(Myj)))
    ok = max_dN < 1e-6 and max_dVz < 1e-6 and max_dMy < 1e-6
    return {"max_dN": max_dN, "max_dVz": max_dVz, "max_dMy": max_dMy,
            "ok": bool(ok)}


def _equilibrio(base_nodes):
    ops.reactions()
    rx = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    for nt in base_nodes:
        r = ops.nodeReaction(nt)
        rx["fx"] += r[0]
        rx["fy"] += r[1]
        rx["fz"] += r[2]
    return rx


def _resumen(bloque, tipo_elem, aplicada, base_nodes):
    rx = _equilibrio(base_nodes)
    e_fx = abs(rx["fx"] + aplicada["fx"])
    e_fy = abs(rx["fy"] + aplicada["fy"])
    e_fz = abs(rx["fz"] - aplicada["fz"])
    n_v = sum(1 for t in tipo_elem.values() if t == "viga")
    n_c = sum(1 for t in tipo_elem.values() if t == "pilar")
    n_m = sum(1 for t in tipo_elem.values() if t == "muro")
    n_a = sum(1 for t in tipo_elem.values() if t == "aspa")
    br = sum(1 for t in tipo_elem.values() if t == "brazo")
    return {
        "n_elementos": len(bloque),
        "n_viga": n_v, "n_pilar": n_c, "n_muro": n_m, "n_aspa": n_a,
        "n_brazo": br,
        "aplicada": aplicada, "reacciones": rx,
        "e_fx": e_fx, "e_fy": e_fy, "e_fz": e_fz,
        "equilibrio": bool(max(e_fx, e_fy, e_fz) < 1e-3),
        "cierre_verticales": _cierre_verticales(tipo_elem, bloque),
    }


# ------------------------------------------------------------------- todo A/B
def correr_edificio_b(etiquetas):
    """(esfuerzos_completos, metadatos, verif) para el Edificio B.

    `etiquetas`: tag(str) -> sección (col/muro) de la semana 03.
    """
    from edificio_b import esfuerzos as ef
    from edificio_b.construir import build_model

    # --- metadatos con un modelo nuevo (sin resolver) ---
    M0 = build_model()
    estados = _clasificar_b(M0)
    metadatos = _agregar_nodos_y_estados(
        _metadatos(M0.tipo, etiquetas, MATERIAL["B"], VIGA_NOMINAL["B"]),
        estados)

    por_caso = {}
    verif = {}
    for caso in CASOS:
        M, aplicada, w_por_viga = ef._correr_caso(caso)
        bloque = _bloque_esfuerzos(M.tipo, w_por_viga)
        por_caso[caso] = bloque
        verif[caso] = _resumen(bloque, M.tipo, aplicada, M.base_nodes)
    return por_caso, metadatos, verif


def correr_edificio_a(etiquetas):
    """(esfuerzos_completos, metadatos, verif) para el Edificio A."""
    # Importar benchmark_3d.analizar PRIMERO: su módulo inserta su propia
    # carpeta en sys.path y habilita el `import datos_edificio` de esfuerzos.
    from benchmark_3d.analizar import construir_con_voladizo
    from benchmark_3d import esfuerzos as ef
    from benchmark_3d import datos_edificio as d

    # --- metadatos con un modelo nuevo (sin resolver) ---
    modelo = construir_con_voladizo(d)
    tipo0, _, _ = ef._tipo_y_area(modelo)
    estados = _clasificar_a(modelo)
    metadatos = _agregar_nodos_y_estados(
        _metadatos(tipo0, etiquetas, MATERIAL["A"], VIGA_NOMINAL["A"]),
        estados)
    base_nodes = set(ops.getFixedNodes())

    por_caso = {}
    verif = {}
    for caso in CASOS:
        tipo, aplicada, w_por_viga = ef._correr_caso(caso)
        bloque = _bloque_esfuerzos(tipo, w_por_viga)
        por_caso[caso] = bloque
        verif[caso] = _resumen(bloque, tipo, aplicada, base_nodes)
    return por_caso, metadatos, verif


def resultado(cache_semana03, verbose=True):
    """Corre A+B y devuelve el diccionario del cache de la semana 04.

    `cache_semana03`: dict de results/secciones_semana03.json (secciones B/A).
    """
    per = cache_semana03.get("per_elemento", {})
    per_a = cache_semana03.get("per_elemento_A", {})

    eb, mb, vib_b = correr_edificio_b(per)
    ea, ma, vib_a = correr_edificio_a(per_a)

    out = {
        "esfuerzos_completos": eb,
        "esfuerzos_completos_A": ea,
        "metadatos": mb,
        "metadatos_A": ma,
        "verif_semana04": {"B": vib_b, "A": vib_a},
        "convencion": ("N = Ni (localForce) con Ni>0 = COMPRESION; evaluacion "
                       "N(x)=-N, Vz(x)=Vz+Wz*x, My(x)=My+Vz*x+Wz*x^2/2; "
                       "Wy=Wz=0 en columnas/muros/aspas; Wz=-w en vigas."),
        "unidades": "m, kN, kN*m",
        "edificios": {"A": {"material": MATERIAL["A"],
                            "viga_nominal": VIGA_NOMINAL["A"]},
                      "B": {"material": MATERIAL["B"],
                            "viga_nominal": VIGA_NOMINAL["B"]}},
    }
    if verbose:
        for ed, ec, mv, vib in (("B", eb, mb, vib_b), ("A", ea, ma, vib_a)):
            n = {t: 0 for t in ("column", "wall", "vigas_x", "vigas_y")}
            for r in mv.values():
                n[r["tipo"]] = n.get(r["tipo"], 0) + 1
            print(f"[S04 {ed}] elementos {len(mv)} (col {n['column']}, "
                  f"muro {n['wall']}, vigas {n['vigas_x'] + n['vigas_y']})")
            for caso in CASOS:
                v = vib[caso]
                cv = v["cierre_verticales"]
                print(f"  {caso}: eq={v['equilibrio']} n={v['n_elementos']} "
                      f"cierreV dN={cv['max_dN']:.1e} dVz={cv['max_dVz']:.1e} "
                      f"dMy={cv['max_dMy']:.1e}")
    return out