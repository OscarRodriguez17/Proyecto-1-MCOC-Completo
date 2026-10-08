# -*- coding: utf-8 -*-
"""
final_figuras.py — Figuras en blanco y negro del informe final (Semana 7).

Lee SOLO resultados ya generados por el pipeline (no reanaliza nada):
  results/modelo_resultados.json      (Edificio A)
  results/modelo_resultados_b.json    (Edificio B)
  results/secciones_semana03.json     (catálogo P–M y M–φ)
  results/edificio_solido.json        (demandas por elemento)
  results/ar_elementos.json           (elementos de la app AR)

y escribe reports/fig/final_*.png (escala de grises, sin colores).

Uso (desde la raíz del proyecto):
    python scripts\\final_figuras.py
"""
import json
import os

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(RAIZ, "results")
FIG = os.path.join(RAIZ, "reports", "fig")

plt.rcParams.update({
    "font.family": "serif",
    "font.size": 9,
    "axes.edgecolor": "black",
    "axes.labelcolor": "black",
    "xtick.color": "black",
    "ytick.color": "black",
    "text.color": "black",
    "axes.prop_cycle": matplotlib.cycler(color=["black"]),
    "savefig.dpi": 200,
    "savefig.bbox": "tight",
})


def _json(nombre):
    with open(os.path.join(RES, nombre), encoding="utf-8") as f:
        return json.load(f)


def _guardar(fig, nombre):
    ruta = os.path.join(FIG, nombre)
    fig.savefig(ruta)
    plt.close(fig)
    print("  ->", os.path.relpath(ruta, RAIZ))


# ---------------------------------------------------------------- modelo 3D
def fig_modelo_3d(A, B, offset_b=60.0):
    fig = plt.figure(figsize=(9.0, 5.0))
    fig.subplots_adjust(0, 0, 1, 1)
    ax = fig.add_subplot(111, projection="3d")
    estilos = {
        "column": ("black", 1.0), "pilar": ("black", 1.0),
        "wall": ("black", 2.4), "muro": ("black", 2.4),
        "vigas_x": ("0.45", 0.6), "vigas_y": ("0.45", 0.6), "viga": ("0.45", 0.6),
        "aspa": ("black", 0.8), "brazo": ("0.7", 0.5),
    }
    nA = {int(k): v for k, v in A["nodos"].items()}
    for e in A["elementos"]:
        c, w = estilos.get(e["tipo"], ("0.3", 0.6))
        p, q = nA[e["ni"]], nA[e["nj"]]
        ax.plot([p["x"], q["x"]], [p["y"], q["y"]], [p["z"], q["z"]], color=c, lw=w)
    nB = {n["id"]: n for n in B["nodos"]}
    for e in B["elementos"]:
        c, w = estilos.get(e["tipo"], ("0.3", 0.6))
        p, q = nB[e["ni"]], nB[e["nj"]]
        ax.plot([p["x"] + offset_b, q["x"] + offset_b], [p["y"], q["y"]], [p["z"], q["z"]],
                color=c, lw=w)
    ax.text(8, 8, 19, "Edificio A", fontsize=9)
    ax.text(offset_b + 18, 25, 22, "Edificio B", fontsize=9)
    ax.view_init(elev=28, azim=-58)
    ax.set_box_aspect((105, 42, 26), zoom=1.0)
    ax.set_axis_off()
    ruta = os.path.join(FIG, "final_modelo_3d.png")
    fig.savefig(ruta, bbox_inches=None, dpi=250)
    plt.close(fig)
    _recortar(ruta)
    print("  ->", os.path.relpath(ruta, RAIZ))


def _recortar(ruta, margen=12):
    """Quita el blanco sobrante alrededor de una figura 3D."""
    from PIL import Image, ImageOps
    im = Image.open(ruta).convert("L")
    caja = ImageOps.invert(im).getbbox()
    if caja:
        x0, y0, x1, y1 = caja
        im.crop((max(x0 - margen, 0), max(y0 - margen, 0),
                 min(x1 + margen, im.width), min(y1 + margen, im.height))).save(ruta)


# ---------------------------------------------------------------- derivas
def _perfil(D, caso, comp, base_z):
    dm = D["resultados"][caso]["desplazamientos_maestro"]
    ks = sorted(dm, key=lambda k: dm[k]["z"])
    zs, us, ds = [base_z], [0.0], []
    prev_u, prev_z = 0.0, base_z
    for k in ks:
        z, u = dm[k]["z"], dm[k][comp]
        if abs(z - base_z) < 1e-9:
            continue
        ds.append((u - prev_u) / (z - prev_z) * 1000.0)
        zs.append(z)
        us.append(u * 1000.0)
        prev_u, prev_z = u, z
    return zs, us, ds


def fig_derivas(A, B):
    fig, (a1, a2) = plt.subplots(1, 2, figsize=(7.0, 3.2), sharey=True)
    series = [
        (A, "EX", "ux", -4.21, "A · EX", "-", "o"),
        (A, "EY", "uy", -4.21, "A · EY", "--", "s"),
        (B, "EX", "ux", -4.01, "B · EX", "-", "^"),
        (B, "EY", "uy", -4.01, "B · EY", "--", "D"),
    ]
    for D, caso, comp, z0, et, ls, mk in series:
        zs, us, ds = _perfil(D, caso, comp, z0)
        gris = "black" if et.startswith("A") else "0.45"
        a1.plot(us, zs, ls=ls, marker=mk, ms=4, color=gris, mfc="white", label=et)
        zmed = [(zs[i] + zs[i + 1]) / 2 for i in range(len(ds))]
        a2.plot(ds, zmed, ls=ls, marker=mk, ms=4, color=gris, mfc="white", label=et)
    a1.set_xlabel("Desplazamiento del diafragma [mm]")
    a1.set_ylabel("z [m]")
    a2.set_xlabel("Deriva de entrepiso [‰]")
    for a in (a1, a2):
        a.grid(True, color="0.85", lw=0.5)
    a1.legend(frameon=False, fontsize=8)
    _guardar(fig, "final_derivas.png")


# ---------------------------------------------------------------- M–phi
def fig_mphi(S):
    mfi = S["curvas"]["col"]["mfi"]
    fig, ax = plt.subplots(figsize=(5.2, 3.0))
    for clave, ls, et in (("6", ":", "malla 6×6"), ("12", "-", "malla 12×12"),
                          ("20", "--", "malla 20×20")):
        m = mfi[clave]
        ax.plot([k * 1000 for k in m["k"]], m["M"], ls=ls, color="black", lw=1.1, label=et)
    ax.set_xlabel("Curvatura φ [10⁻³ 1/m]")
    ax.set_ylabel("M [kN·m]")
    ax.set_xlim(0, 80)
    ax.grid(True, color="0.85", lw=0.5)
    ax.legend(frameon=False)
    ax.set_title("Columna 0,70×0,70 (8φ28, f'c = 25 MPa), P = 0", fontsize=9)
    _guardar(fig, "final_mphi_columna.png")


# ---------------------------------------------------------------- P–M
def _env(c):
    pares = sorted(zip(c["envelope"]["P"], c["envelope"]["M"]))
    return [p for p, _ in pares], [m for _, m in pares]


def fig_pm(S, SOL, AR):
    cat_A = S["curvas_A"]
    elA = SOL["edificios"][0]["secciones"]["elementos"]
    elB = SOL["edificios"][1]["secciones"]["elementos"]
    fig, (a1, a2) = plt.subplots(1, 2, figsize=(7.2, 3.6))

    P, M = _env(cat_A["col_A_0.70x0.70"])
    a1.plot(M, P, "-", color="black", lw=1.2, label="col_A 0,70×0,70 (f'c 30, 8φ28)")
    b = cat_A["col_A_0.70x0.70"]["balanceado"]
    a1.plot([b["M"]], [b["P"]], "s", color="black", mfc="white", ms=5, label="balanceado")
    for tag, mk in (("14", "o"), ("26", "^")):
        pm = AR["elementos"][tag]["pm"]
        d = pm["demanda"][pm["gobierna"]]
        a1.plot([d["M"]], [d["P"]], mk, color="black", ms=5,
                label=f"col {tag} GQ: D/C = {pm['DC']:.2f}")
    P, M = _env(cat_A["PM_A_300x300x20"])
    a1.plot(M, P, "--", color="0.45", lw=1.0, label="P.M. 300×300×20 (acero)")
    a1.set_xlabel("M [kN·m]")
    a1.set_ylabel("P [kN] (compresión +)")
    a1.set_title("Edificio A — columnas", fontsize=9)
    a1.legend(frameon=False, fontsize=7, loc="upper center",
              bbox_to_anchor=(0.5, -0.18), ncol=2)
    a1.grid(True, color="0.85", lw=0.5)

    c = S["curvas"]["muro0.60x2.91"]
    P, M = _env(c)
    a2.plot(M, P, "-", color="black", lw=1.2, label="muro 0,60×2,91 (f'c 25)")
    b = c["balanceado"]
    a2.plot([b["M"]], [b["P"]], "s", color="black", mfc="white", ms=5, label="balanceado")
    d41 = elB["41"]["demanda"]
    a2.plot([d41["GQ"]["M"]], [d41["GQ"]["P"]], "o", color="black", ms=5,
            label="tag 41 GQ (D/C 0,74)")
    a2.plot([d41["EY"]["M"]], [d41["EY"]["P"]], "x", color="black", ms=7, mew=1.5,
            label="tag 41 EY (D/C 1,78)")
    a2.set_xlabel("M [kN·m]")
    a2.set_title("Edificio B — muro crítico", fontsize=9)
    a2.legend(frameon=False, fontsize=7, loc="upper center",
              bbox_to_anchor=(0.5, -0.18), ncol=2)
    a2.grid(True, color="0.85", lw=0.5)
    del elA
    _guardar(fig, "final_pm.png")


# ---------------------------------------------------------------- viga 134
def fig_viga134(AR):
    """V(x) y M(x) exactos por tramo (coeficientes del contrato AR), con el
    salto de corte que introduce la viga secundaria en x = 5 m."""
    e = AR["elementos"]["134"]
    fig, (a1, a2) = plt.subplots(2, 1, figsize=(6.0, 3.8), sharex=True)
    mmax = 0.0
    for t in e["tramos"]:
        c, x0, L = t["coeficientes"], t["x0"], t["L"]
        xs = [L * i / 50 for i in range(51)]
        V = [c["Vz"] + c["Wz"] * x for x in xs]
        M = [c["My"] + c["Vz"] * x + c["Wz"] * x * x / 2 for x in xs]
        X = [x0 + x for x in xs]
        a1.plot(X, V, "-", color="black", lw=1.2)
        a1.fill_between(X, V, 0, color="0.85")
        a2.plot(X, M, "-", color="black", lw=1.2)
        a2.fill_between(X, M, 0, color="0.85")
        mmax = max(mmax, max(M))
    for a in (a1, a2):
        a.axhline(0, color="black", lw=0.6)
        a.grid(True, color="0.9", lw=0.5)
        a.axvline(5.0, color="0.5", lw=0.7, ls=":")
    a1.set_ylabel("V [kN]")
    a2.invert_yaxis()
    a2.set_ylabel("M [kN·m]")
    a2.set_xlabel("x desde el eje F [m]")
    a2.text(5.15, -60, "viga secundaria (x = 5 m)", fontsize=7)
    a1.set_title("Viga 134 (F–G / A3), caso GQ", fontsize=9)
    _guardar(fig, "final_viga134.png")


# ---------------------------------------------------------------- fotos AR
def fotos_bn():
    from PIL import Image
    for src, dst in (("semana06_ar_columna26.jpg", "final_ar_columna26.png"),
                     ("semana06_ar_viga134.jpg", "final_ar_viga134.png"),
                     ("semana06_ar_alineacion_col26.png", "final_ar_alineacion.png")):
        ruta = os.path.join(FIG, src)
        if os.path.exists(ruta):
            Image.open(ruta).convert("L").save(os.path.join(FIG, dst))
            print("  ->", os.path.join("reports", "fig", dst))


def main():
    A = _json("modelo_resultados.json")
    B = _json("modelo_resultados_b.json")
    S = _json("secciones_semana03.json")
    SOL = _json("edificio_solido.json")
    AR = _json("ar_elementos.json")
    print("Figuras del informe final (blanco y negro):")
    fig_modelo_3d(A, B)
    fig_derivas(A, B)
    fig_mphi(S)
    fig_pm(S, SOL, AR)
    fig_viga134(AR)
    fotos_bn()


if __name__ == "__main__":
    main()
