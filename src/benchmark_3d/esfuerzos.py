# -*- coding: utf-8 -*-
"""esfuerzos.py — Pasada dedicada de diagramas de esfuerzos en vigas (Edificio A).

Paridad con `edificio_b/esfuerzos.py`: por cada caso (G/Q/GQ/EX/EY) construye
un modelo nuevo (construir_con_voladizo), aplica las cargas como carga
distribuida de ELEMENTO en las vigas y extrae `localForce` para exportar, por
viga y caso, los coeficientes mínimos que permiten evaluar N(x), V(x) y M(x)
en cualquier punto x ∈ [0, L].

Para cada viga se exporta (bloque esfuerzos del JSON de A, dentro de cada caso):

    { "L":…, "N":Ni, "Vy":Vyi, "Vz":Vzi, "T":Ti, "My":Myi, "Mz":Mzi,
      "Wy":0.0, "Wz":-w }

Fórmulas de evaluación en x ∈ [0, L] (las respeta correr_esfuerzos y las blinda
tests/test_esfuerzos.py):

    N(x)  = -N                  (constante)
    Vz(x) = Vz + Wz·x
    My(x) = My + Vz·x + Wz·x²/2
    Vy(x) = Vy + Wy·x
    Mz(x) = Mz + Vy·x + Wy·x²/2

Cargas por caso (los qG/qQ salen de cargas.distribuir_tributaria, misma fuente
que _exportar_cargas; pp_viga = A_pp·rho_pp por viga):
  - G : w = qG + pp_viga          (viga)  + PP de columnas/muros/aspas al nodo
          superior (A_pp·rho_pp·L, solo G y GQ; igual que cargas_gravedad)
  - Q : w = qQ                    (viga)  (sin peso propio, igual que en analizar)
  - GQ: w = qG + qQ + pp_viga     (viga)  + PP de columnas/muros/aspas
  - EX/EY: w = 0                  (solo el patrón sísmico patron_sismico)

Solver idéntico a analizar.py: BandGen/RCM/Transformation/LoadControl/Linear.

Cierre por viga y caso (equilibrio del elemento ya cargado): de `localForce`
salen Vzi, Myi en i y Vzj, Myj en j con
    Vz(L) = Vzi - w·L = -Vzj      y    My(L) = Myi + Vzi·L - w·L²/2 = -Myj
(verificado aquí y en el test de cierre con tol < 1e-6·max(1,|valor|)).

Unidades: m, kN.  ADITIVO: no toca geometría, IDs, construir.py ni el análisis
nodal existente (los resultados de analizar.py no cambian).
"""
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import openseespy.opensees as ops

import datos_edificio as d
import cargas
from analizar import construir_con_voladizo

CASOS = ("G", "Q", "GQ", "EX", "EY")


# --------------------------------------------------------------- solver común
def _resolver():
    ops.system('BandGen')
    ops.numberer('RCM')
    ops.constraints('Transformation')
    ops.integrator('LoadControl', 1.0)
    ops.algorithm('Linear')
    ops.analysis('Static')
    ok = ops.analyze(1)
    if ok != 0:
        raise RuntimeError("El análisis no convergió (matriz singular).")


def _long(n1, n2):
    x1, y1, z1 = ops.nodeCoord(n1)
    x2, y2, z2 = ops.nodeCoord(n2)
    return ((x2 - x1) ** 2 + (y2 - y1) ** 2 + (z2 - z1) ** 2) ** 0.5


# ------------------------------------------------------------- clasificación
def _tipo_y_area(modelo, dat=d):
    """tag -> 'viga'|'pilar'|'muro'|'aspa' y (A_pp, rho_pp) por elemento.

    Solo vigas_x/vigas_y se tratan como viga (se les extrae diagrama);
    columnas/muros/aspas aportan su peso propio nodal (G y GQ). El área de
    autonivel de los muros se computa como t·L (igual que cargas.py).
    """
    tipo, area, rho = {}, {}, {}
    A_v = dat.sec_viga()[0]
    for cat in ("vigas_x", "vigas_y"):
        for v in modelo[cat]:
            tag = v["tag"]
            tipo[tag] = "viga"
            area[tag] = v.get("A_pp", A_v)
            rho[tag] = v.get("rho_pp", dat.GAMMA_C)
    A_c = dat.sec_columna()[0]
    for col in modelo["columns"]:
        tag = col["tag"]
        tipo[tag] = "pilar"
        area[tag] = col.get("A_pp", A_c)
        rho[tag] = col.get("rho_pp", dat.GAMMA_C)
    for muro in modelo["walls"]:
        tag = muro["tag"]
        tipo[tag] = "muro"
        area[tag] = muro["t"] * muro["L"]
        rho[tag] = dat.GAMMA_C
    for aspa in modelo.get("aspas", []):
        tag = aspa["tag"]
        tipo[tag] = "aspa"
        area[tag] = aspa["A_pp"]
        rho[tag] = aspa["rho_pp"]
    return tipo, area, rho


# ------------------------------------------------------------------- cargas
def _w_viga(caso, qG, qQ, pp):
    """Carga uniforme [kN/m] a aplicar como -beamUniform en la viga `e`."""
    if caso == "G":
        return qG + pp
    if caso == "Q":
        return qQ
    if caso == "GQ":
        return qG + qQ + pp
    return 0.0          # EX / EY


def _correr_caso(caso, dat=d, offset=(0.0, 0.0)):
    """Construye un modelo nuevo, aplica las cargas del caso y resuelve.

    Devuelve (tipo, aplicada, w_por_viga). `aplicada` con la misma convención
    de analizar.py: fz magnitud positiva de carga vertical; fx/fy fuerza
    lateral. `w_por_viga` guarda la carga lineal aplicada a cada viga (para
    el Wz exportado).
    """
    M = construir_con_voladizo(dat, offset)
    tipo, area, rho = _tipo_y_area(M, dat)
    aplicada = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    w_por_viga = {}

    if caso in ("G", "Q", "GQ"):
        ops.timeSeries('Constant', 1)
        ops.pattern('Plain', 1, 1)
        conG = caso in ("G", "GQ")
        wG, _, _, _ = cargas.distribuir_tributaria(dat.Q_G, M, dat=dat)
        wQ, _, _, _ = cargas.distribuir_tributaria(dat.Q_Q, M, dat=dat)

        # vigas: carga distribuida de elemento (beamUniform 0.0, -w)
        for e in sorted(tipo):
            if tipo[e] != 'viga':
                continue
            pp = rho[e] * area[e]
            w = _w_viga(caso, wG.get(e, 0.0), wQ.get(e, 0.0), pp)
            w_por_viga[e] = w
            if abs(w) < 1e-12:
                continue
            L = _long(*ops.eleNodes(e))
            ops.eleLoad('-ele', e, '-type', '-beamUniform', 0.0, -w)
            aplicada["fz"] += w * L

        # columnas/muros/aspas: peso propio nodal al nodo SUPERIOR (solo G y GQ)
        if conG:
            for e in sorted(tipo):
                if tipo[e] not in ("pilar", "muro", "aspa"):
                    continue
                if not area[e]:
                    continue
                n1, n2 = ops.eleNodes(e)
                z1 = ops.nodeCoord(n1)[2]
                z2 = ops.nodeCoord(n2)[2]
                top = n1 if z1 > z2 else n2
                p = rho[e] * area[e] * _long(n1, n2)
                ops.load(top, 0.0, 0.0, -p, 0.0, 0.0, 0.0)
                aplicada["fz"] += p

    elif caso in ("EX", "EY"):
        pesos = cargas.pesos_por_nivel(M, dat=dat)
        dirn = "X" if caso == "EX" else "Y"
        v_base, _fuerzas = cargas.patron_sismico(dirn, M, pesos, dat=dat)
        if caso == "EX":
            aplicada["fx"] = v_base
        else:
            aplicada["fy"] = v_base

    _resolver()
    return tipo, aplicada, w_por_viga


# ------------------------------------------------------------ extracción
def _extraer(tipo, w_por_viga):
    """localForce por viga -> {str(e): {L,N,Vy,Vz,T,My,Mz,Wy,Wz}}."""
    out = {}
    for e in sorted(tipo):
        if tipo[e] != 'viga':
            continue
        n1, n2 = ops.eleNodes(e)
        L = _long(n1, n2)
        f = ops.eleResponse(e, 'localForce')
        if f is None or len(f) < 12:
            continue
        Ni, Vyi, Vzi, Ti, Myi, Mzi, _Nj, _Vyj, _Vzj, _Tj, _Myj, _Mzj = f
        w = w_por_viga.get(e, 0.0)
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


def _cierre_por_viga(tipo, bloque, w_por_viga):
    """Cierre Vz(L)=-Vzj y My(L)=-Myj por viga (para verificación)."""
    res = {}
    for k in sorted(bloque, key=int):
        b = bloque[k]
        e = int(k)
        w = w_por_viga.get(e, 0.0)
        L = b["L"]
        f = ops.eleResponse(e, 'localForce')
        if f is None or len(f) < 12:
            continue
        _Ni, _Vyi, _Vzi, _Ti, _Myi, _Mzi, _Nj, _Vyj, _Vzj, _Tj, _Myj, _Mzj = f
        VzL = b["Vz"] + b["Wz"] * L
        MyL = b["My"] + b["Vz"] * L + b["Wz"] * L * L / 2.0
        res[e] = {
            "dVz": abs(VzL - (-_Vzj)),
            "dMy": abs(MyL - (-_Myj)),
            "tolVz": 1e-6 * max(1.0, abs(_Vzj)),
            "tolMy": 1e-6 * max(1.0, abs(_Myj)),
        }
    return res


# ------------------------------------------------------------------- correr
def _correr_todo(verbose=True, dat=d, offset=(0.0, 0.0)):
    """Corre la pasada dedicada de diagramas para G/Q/GQ/EX/EY.

    Devuelve (por_caso, verif):
      por_caso: {caso: {str(e): {L,N,Vy,Vz,T,My,Mz,Wy,Wz}}}
      verif:    {caso: {aplicada, reacciones, e_fx/e_fy/e_fz, equilibrio,
                        n_vigas, cierre_ok, vigas_sin_cierre}}
    """
    por_caso = {}
    verif = {}
    ok_cierre = True
    ok_eq = True

    for caso in CASOS:
        tipo, aplicada, w_por_viga = _correr_caso(caso, dat=dat, offset=offset)

        # reacciones en la base
        ops.reactions()
        rx = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
        for nt in ops.getFixedNodes():
            r = ops.nodeReaction(nt)
            rx["fx"] += r[0]
            rx["fy"] += r[1]
            rx["fz"] += r[2]

        e_fx = abs(rx["fx"] + aplicada["fx"])
        e_fy = abs(rx["fy"] + aplicada["fy"])
        e_fz = abs(rx["fz"] - aplicada["fz"])
        eq = max(e_fx, e_fy, e_fz) < 1e-3
        ok_eq = ok_eq and eq

        bloque = _extraer(tipo, w_por_viga)
        cierre = _cierre_por_viga(tipo, bloque, w_por_viga)
        malos = [e for e, c in cierre.items()
                 if c["dVz"] > c["tolVz"] or c["dMy"] > c["tolMy"]]
        ok_c = not malos
        ok_cierre = ok_cierre and ok_c

        por_caso[caso] = bloque
        verif[caso] = {
            "aplicada": aplicada,
            "reacciones": rx,
            "e_fx": e_fx, "e_fy": e_fy, "e_fz": e_fz,
            "equilibrio": eq,
            "n_vigas": len(bloque),
            "cierre_ok": ok_c,
            "vigas_sin_cierre": malos,
        }

        if verbose:
            print(f"[ESFUERZOS] {caso:3s}: {len(bloque):3d} vigas | "
                  f"eq fx={e_fx:.2e} fy={e_fy:.2e} fz={e_fz:.2e} -> "
                  f"{'OK' if eq else 'REVISAR'} | cierre "
                  f"{'OK' if ok_c else 'FALLA (' + str(len(malos)) + ')'}")

    if verbose:
        print(f"[ESFUERZOS] RESUMEN: equilibrio={'OK' if ok_eq else 'FALLA'}  "
              f"cierre={'OK' if ok_cierre else 'FALLA'}")
    return por_caso, verif


def correr_esfuerzos(verbose=True, dat=d, offset=(0.0, 0.0)):
    """Corre la pasada dedicada de diagramas para G/Q/GQ/EX/EY.

    Devuelve {caso: {str(e): {L,N,Vy,Vz,T,My,Mz,Wy,Wz}}}. Además verifica (y
    reporta) el cierre por viga y caso y el equilibrio global de cada pasada.
    """
    por_caso, _ = _correr_todo(verbose=verbose, dat=dat, offset=offset)
    return por_caso


def verificar_diagramas(verbose=True, dat=d, offset=(0.0, 0.0)):
    """Corre la pasada y devuelve la verificación completa por caso
    (equilibrio global ΣR + Σaplicada ≈ 0 y cierre Vz(L)=-Vzj / My(L)=-Myj
    con tol 1e-6·max(1,|valor|)). Usado por tests/test_esfuerzos.py."""
    por_caso, verif = _correr_todo(verbose=verbose, dat=dat, offset=offset)
    return verif, por_caso


if __name__ == '__main__':
    correr_esfuerzos(verbose=True)