# -*- coding: utf-8 -*-
"""
exportar_ar.py — Datos precalculados para la app AR de inspección (Semana 6).

La app del teléfono NO calcula nada: lee `results/ar_elementos.json` y dibuja.
Todo lo que muestra sale de aquí, que a su vez lee los resultados OpenSees ya
verificados (`results/edificio_solido.json`, bloque Edificio A, caso GQ).

Por cada elemento preseleccionado se exporta:
  - identificación: tag, tipo, sección, material, L, ubicación legible
    (ejes de la grilla y piso), nodos i/j en coords OpenSees y Unity;
  - ejes locales del elemento;
  - diagramas MUESTREADOS a lo largo del elemento (x = 0 … L, desde el extremo i):
        N      axial (N < 0 = compresión)
        V_xz   corte del plano local x–z   = Vz(x)
        M_xz   momento del plano local x–z = My(x)
        V_xy   corte del plano local x–y   = −Vy(x)
        M_xy   momento del plano local x–y = Mz(x)   (fórmula CORREGIDA)
        T      torsión (constante)
    con extremos i/j, máximo |valor| y su posición, y el LADO DE TRACCIÓN de
    los valores positivos de momento (vector en coords OpenSees y Unity + texto);
  - `principal`: el plano con mayor |M| (el que la app muestra por defecto);
  - columnas/muros: curva P–M de su sección + demanda (P, M) en ambos extremos,
    extremo que gobierna y razón D/C a nivel de servicio (GQ, sin mayorar).

CONVENCIONES (verificadas contra casos de solución conocida en OpenSees; ver
tests/test_ar_elementos.py). A partir de `localForce` del extremo i
[N, Vy, Vz, T, My, Mz, …] y de la carga distribuida (Wy, Wz):
    N(x)    = −N                      (N < 0 → compresión)
    Vz(x)   = Vz + Wz·x
    My(x)   = My + Vz·x + Wz·x²/2     (M > 0 → tracción en la cara −z local)
    Vy(x)   = Vy + Wy·x
    Mz(x)   = Mz − Vy·x − Wy·x²/2     (M > 0 → tracción en la cara +y local)
  OJO: en el plano x–y la relación corte–momento lleva signo opuesto al plano
  x–z. La fórmula `Mz + Vy·x + Wy·x²/2` (usada hasta la Semana 5) NO cierra.
  Para que en ambos planos se cumpla dM/dx = V, el corte exportado del plano
  x–y es V_xy = −Vy(x).

Coordenadas: OpenSees (x, y, z) con z vertical → Unity (x, z, y) con y vertical.

Uso:
    python src/ar/exportar_ar.py                # tags por defecto
    python src/ar/exportar_ar.py --tags 134 14 26
"""
import argparse
import json
import math
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
RESULTS = os.path.join(RAIZ, "results")
ENTRADA = os.path.join(RESULTS, "edificio_solido.json")
SALIDA = os.path.join(RESULTS, "ar_elementos.json")

TAGS_POR_DEFECTO = [134, 14, 26]   # viga eje A3 F–G + sus dos columnas del 1er piso
CASO = "GQ"
N_MUESTRAS = 41


# ----------------------------------------------------------------- utilidades
def os_a_unity(v):
    """(x, y, z) OpenSees → (x, z, y) Unity."""
    return [v[0], v[2], v[1]]


def _r(v, n=4):
    return round(float(v), n)


def _vec(v):
    return [_r(c, 6) + 0.0 for c in v]


def _neg(v):
    return [-c for c in v]


def _eje_de(valor, grid, tol=1e-3):
    for nombre, c in grid.items():
        if abs(c - valor) < tol:
            return nombre
    return None


def _nombre_nivel(z, niveles):
    """Texto legible del nivel de un nudo (Edificio A: base −4,21; losas)."""
    etiquetas = {0: "base (subterráneo)", 1: "piso 1 (nivel de suelo)",
                 2: "cielo del 1er piso", 3: "cielo del 2° piso",
                 4: "cielo del 3er piso (cubierta)"}
    for k, zk in enumerate(niveles):
        if abs(z - zk) < 1e-3:
            return etiquetas.get(k, f"nivel {k}")
    return f"z={z:+.2f}"


def _texto_lado(vec_os, nodo, grid_x, grid_y):
    """Describe hacia dónde apunta un vector (coords OpenSees) en el edificio."""
    x, y, z = vec_os
    if abs(z) > 0.9:
        return "fibra inferior (abajo)" if z < 0 else "fibra superior (arriba)"

    def vecino(coord, grid, signo):
        cand = sorted(grid.items(), key=lambda kv: kv[1])
        if signo > 0:
            sig = [n for n, c in cand if c > coord + 1e-6]
            return sig[0] if sig else None
        ant = [n for n, c in cand if c < coord - 1e-6]
        return ant[-1] if ant else None

    if abs(x) >= abs(y):
        v = vecino(nodo["x"], grid_x, x)
        return (f"cara {'+X' if x > 0 else '−X'}"
                + (f" (hacia eje {v})" if v else " (hacia el exterior)"))
    v = vecino(nodo["y"], grid_y, y)
    return (f"cara {'+Y' if y > 0 else '−Y'}"
            + (f" (hacia eje {v})" if v else " (hacia el exterior, fachada)"))


# --------------------------------------------------------- diagramas por fórmula
def evaluar(esf, x):
    """Esfuerzos en la posición x [m] desde el extremo i (convenciones arriba)."""
    N, Vy, Vz, T = esf["N"], esf["Vy"], esf["Vz"], esf["T"]
    My, Mz, Wy, Wz = esf["My"], esf["Mz"], esf.get("Wy", 0.0), esf.get("Wz", 0.0)
    return {
        "N": -N,
        "V_xz": Vz + Wz * x,
        "M_xz": My + Vz * x + 0.5 * Wz * x * x,
        "V_xy": -(Vy + Wy * x),
        "M_xy": Mz - Vy * x - 0.5 * Wy * x * x,
        "T": T,
    }


def _extremo_parabola(esf, plano, L):
    """Posición de |M| máximo interior (donde V = 0), si existe."""
    if plano == "xz":
        V, W = esf["Vz"], esf.get("Wz", 0.0)
    else:
        V, W = esf["Vy"], esf.get("Wy", 0.0)
    if abs(W) < 1e-12:
        return None
    xs = -V / W
    return xs if 0.0 < xs < L else None


def _resumen(clave, xs, vals, esf, L):
    i_max = max(range(len(vals)), key=lambda k: abs(vals[k]))
    x_max, v_max = xs[i_max], vals[i_max]
    if clave.startswith("M_"):
        xe = _extremo_parabola(esf, clave[2:], L)
        if xe is not None:
            ve = evaluar(esf, xe)[clave]
            if abs(ve) >= abs(v_max):
                x_max, v_max = xe, ve
    # máximos con signo (en vigas: M negativo en apoyos y positivo en el tramo)
    cand = list(zip(xs, vals))
    if clave.startswith("M_"):
        xe = _extremo_parabola(esf, clave[2:], L)
        if xe is not None:
            cand.append((xe, evaluar(esf, xe)[clave]))
    xp, vp = max(cand, key=lambda t: t[1])
    xn, vn = min(cand, key=lambda t: t[1])
    return {
        "valores": [_r(v, 3) for v in vals],
        "i": _r(vals[0], 3),
        "j": _r(vals[-1], 3),
        "max_abs": {"valor": _r(v_max, 3), "x": _r(x_max, 3)},
        "max_pos": {"valor": _r(vp, 3), "x": _r(xp, 3)},
        "max_neg": {"valor": _r(vn, 3), "x": _r(xn, 3)},
    }


# ------------------------------------------------------------- P–M y demanda
def _capacidad_M(env, P):
    """M resistente de la envolvente para un P dado (interpolación lineal)."""
    Ps, Ms = env["P"], env["M"]
    pares = sorted(zip(Ps, Ms))
    if P <= pares[0][0] or P >= pares[-1][0]:
        return None
    for (p0, m0), (p1, m1) in zip(pares, pares[1:]):
        if p0 <= P <= p1:
            t = (P - p0) / (p1 - p0) if p1 != p0 else 0.0
            return m0 + t * (m1 - m0)
    return None


def _pm(ed, tag, esf, L):
    sec = ed.get("secciones", {})
    info = sec.get("elementos", {}).get(str(tag))
    if not info:
        return None
    cat = sec.get("secciones", {})
    clave = info.get("seccion")
    if clave not in cat:
        clave = "col" if info.get("tipo") == "pilar" else "muro"
    s = cat.get(clave)
    if not s:
        return None
    env = s["envelope"]
    # P > 0 = compresión en el catálogo P–M (= N de localForce en el extremo i)
    P = esf["N"]
    fin = evaluar(esf, L)
    dem = {
        "i": {"P": _r(P, 2), "M": _r(math.hypot(esf["My"], esf["Mz"]), 2)},
        "j": {"P": _r(P, 2), "M": _r(math.hypot(fin["M_xz"], fin["M_xy"]), 2)},
    }
    gob = max(dem, key=lambda k: dem[k]["M"])
    mcap = _capacidad_M(env, P)
    return {
        "seccion": clave,
        "envolvente": {"P": [_r(p, 2) for p in env["P"]],
                       "M": [_r(m, 2) for m in env["M"]]},
        "balanceado": s.get("balanceado"),
        "demanda": dem,
        "gobierna": gob,
        "M_capacidad_en_P": None if mcap is None else _r(mcap, 2),
        "DC": None if not mcap else _r(dem[gob]["M"] / mcap, 3),
        "nota": "Demanda de servicio (GQ, sin mayorar). P > 0 = compresión.",
    }


# ------------------------------------------------------------------ elemento
TIPO = {"vigas_x": "viga", "vigas_y": "viga", "column": "columna", "wall": "muro"}


def exportar_elemento(ed, tag):
    t = str(tag)
    el = next((e for e in ed["elementos"] if str(e["tag"]) == t), None)
    if el is None:
        raise KeyError(f"tag {tag} no existe en el Edificio A")
    md = ed["metadatos"][t]
    esf = ed["esfuerzos_completos"][CASO][t]
    ni, nj = ed["nodos"][str(el["ni"])], ed["nodos"][str(el["nj"])]
    gx, gy = ed["geometria"]["grid_x"], ed["geometria"]["grid_y"]
    niveles = ed["geometria"]["niveles_z"]
    L = esf["L"]
    ej = md["ejes_locales"]

    def extremo(n, nid):
        ex, ey = _eje_de(n["x"], gx), _eje_de(n["y"], gy)
        return {
            "nodo": nid,
            "xyz_opensees": [n["x"], n["y"], n["z"]],
            "xyz_unity": os_a_unity([n["x"], n["y"], n["z"]]),
            "etiqueta": f"Eje {ex or n['x']} / {ey or n['y']} · {_nombre_nivel(n['z'], niveles)}",
            "restriccion": md["nodos"].get("i" if nid == el["ni"] else "j"),
        }

    xs = [L * k / (N_MUESTRAS - 1) for k in range(N_MUESTRAS)]
    muestras = [evaluar(esf, x) for x in xs]
    diag = {k: _resumen(k, xs, [m[k] for m in muestras], esf, L)
            for k in ("N", "V_xz", "M_xz", "V_xy", "M_xy", "T")}

    # lado de tracción para M > 0 (y lado de dibujo de V > 0: el opuesto)
    lado_xz = _neg(ej["z"])          # M_xz > 0 → tracción en −z local
    lado_xy = list(ej["y"])          # M_xy > 0 → tracción en +y local
    for clave, lado in (("M_xz", lado_xz), ("M_xy", lado_xy)):
        diag[clave]["lado_positivo"] = {
            "vector_opensees": _vec(lado),
            "vector_unity": _vec(os_a_unity(lado)),
            "texto": "tracción en " + _texto_lado(lado, ni, gx, gy),
        }
        vk = "V_" + clave[2:]
        diag[vk]["lado_positivo"] = {
            "vector_opensees": _vec(_neg(lado)),
            "vector_unity": _vec(os_a_unity(_neg(lado))),
            "texto": "se dibuja hacia el lado opuesto al de tracción de M > 0",
        }

    plano = "xz" if diag["M_xz"]["max_abs"]["valor"] ** 2 >= \
        diag["M_xy"]["max_abs"]["valor"] ** 2 else "xy"

    ubic_i, ubic_j = extremo(ni, el["ni"]), extremo(nj, el["nj"])
    tipo = TIPO.get(el["tipo"], el["tipo"])
    if tipo == "viga":
        ubic = (f"Viga entre {ubic_i['etiqueta'].split(' · ')[0]} y "
                f"{ubic_j['etiqueta'].split(' · ')[0]} · {_nombre_nivel(ni['z'], niveles)}"
                f" (z = {ni['z']:+.2f} m)")
    else:
        ubic = (f"{tipo.capitalize()} en {ubic_i['etiqueta'].split(' · ')[0]} · "
                f"de {_nombre_nivel(ni['z'], niveles)} a {_nombre_nivel(nj['z'], niveles)}")

    out = {
        "tag": int(tag),
        "tipo": tipo,
        "tipo_modelo": el["tipo"],
        "seccion": md["seccion"],
        "material": md["material"],
        "L": _r(L, 3),
        "ubicacion": ubic,
        "extremos": {"i": ubic_i, "j": ubic_j},
        "ejes_locales": {"opensees": ej,
                         "unity": {k: os_a_unity(v) for k, v in ej.items()}},
        "x": [_r(x, 3) for x in xs],
        "diagramas": diag,
        "principal": {"M": "M_" + plano, "V": "V_" + plano,
                      "plano": f"local x–{plano[1]}"},
        "coeficientes_extremo_i": {k: _r(v, 6) for k, v in esf.items()},
        "anclaje": _anclaje(ni, nj, niveles, tipo),
    }
    if tipo in ("columna", "muro"):
        out["pm"] = _pm(ed, tag, esf, L)
    return out


def _anclaje(ni, nj, niveles, tipo):
    """Punto del PISO justo bajo el centro del elemento (coords modelo).

    La app AR coloca este punto sobre el piso que el usuario toca: resta
    `punto_unity` a toda la geometría del elemento y así queda centrado en el
    ancla, a su altura real sobre ese piso. Piso = losa sobre la que está parado
    el inspector: para vigas, el nivel inmediatamente inferior a la viga; para
    columnas/muros, el nivel de su extremo inferior.
    """
    cx, cy = (ni["x"] + nj["x"]) / 2.0, (ni["y"] + nj["y"]) / 2.0
    z_bajo = min(ni["z"], nj["z"])
    if tipo == "viga":
        inferiores = [z for z in niveles if z < z_bajo - 1e-6]
        z_piso = max(inferiores) if inferiores else z_bajo
    else:
        z_piso = z_bajo
    p = [cx, cy, z_piso]
    return {
        "punto_opensees": [_r(c, 4) for c in p],
        "punto_unity": [_r(c, 4) for c in os_a_unity(p)],
        "z_piso": _r(z_piso, 4),
        "altura_sobre_piso": {"i": _r(ni["z"] - z_piso, 4),
                              "j": _r(nj["z"] - z_piso, 4)},
        "nota": ("Restar punto_unity a la geometría del elemento y colocarla "
                 "como hija del ancla (sobre el piso). El elemento queda "
                 "centrado en el ancla y a su altura real sobre ese piso."),
    }


def exportar(tags=None, entrada=ENTRADA, salida=SALIDA, verbose=True):
    tags = tags or TAGS_POR_DEFECTO
    with open(entrada, encoding="utf-8") as f:
        data = json.load(f)
    ed = next(e for e in data["edificios"] if e["bloque"].startswith("Edificio A"))
    elementos = {str(t): exportar_elemento(ed, t) for t in tags}
    doc = {
        "version": 1,
        "edificio": "A",
        "caso": CASO,
        "caso_descripcion": "estático de servicio G + Q (sin sismo, sin mayorar)",
        "unidades": {"longitud": "m", "fuerza": "kN", "momento": "kN·m"},
        "coordenadas": "OpenSees (x, y, z; z vertical) → Unity (x, z, y; y vertical)",
        "convenciones": {
            "x": "posición desde el extremo i hacia el j [m]",
            "N": "N < 0 = compresión",
            "M_xz": "My(x) = My + Vz·x + Wz·x²/2; M > 0 → tracción en −z local",
            "M_xy": "Mz(x) = Mz − Vy·x − Wy·x²/2; M > 0 → tracción en +y local",
            "V": "V_xz = Vz(x); V_xy = −Vy(x); en ambos planos dM/dx = V",
            "dibujo": "M se dibuja hacia su lado de tracción; V > 0 hacia el lado opuesto",
        },
        "fuente": os.path.relpath(entrada, RAIZ).replace("\\", "/"),
        "elementos": elementos,
    }
    with open(salida, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    if verbose:
        print(f"[AR] {os.path.relpath(salida, RAIZ)}: {len(elementos)} elementos ({CASO})")
        for t, e in elementos.items():
            p = e["principal"]["M"]
            d = e["diagramas"][p]
            extra = ""
            if e.get("pm") and e["pm"]["DC"] is not None:
                extra = f" | D/C servicio = {e['pm']['DC']:.2f}"
            print(f"   tag {t:>4} {e['tipo']:7s} L={e['L']:5.2f}  {p}: i={d['i']:8.1f} "
                  f"j={d['j']:8.1f} max={d['max_abs']['valor']:8.1f} @x={d['max_abs']['x']:.2f}{extra}")
    return doc


def main():
    ap = argparse.ArgumentParser(description="Exporta datos para la app AR")
    ap.add_argument("--tags", type=int, nargs="*", default=None)
    a = ap.parse_args()
    exportar(a.tags)


if __name__ == "__main__":
    main()
