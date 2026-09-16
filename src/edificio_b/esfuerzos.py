# -*- coding: utf-8 -*-
"""
esfuerzos.py — Pasada DEDICADA de diagramas de esfuerzos en vigas (Edificio B).

Es INDEPENDIENTE del análisis nodal existente (analizar.py): por cada caso
(G/Q/GQ/EX/EY) construye un modelo nuevo (build_model), aplica las cargas como
carga distribuida de ELEMENTO en las vigas y extrae `localForce` para exportar,
por viga y caso, los coeficientes mínimos que permiten evaluar N(x), V(x) y
M(x) en cualquier punto x ∈ [0, L] de la viga.

Para cada viga se exporta (bloque esfuerzos del JSON de B, dentro de cada caso):

    { "L":…, "N":Ni, "Vy":Vyi, "Vz":Vzi, "T":Ti, "My":Myi, "Mz":Mzi,
      "Wy":0.0, "Wz":-w }

Fórmulas de evaluación en x ∈ [0, L] (las respeta correr_esfuerzos y las blinda
tests/test_edificio_b/test_esfuerzos.py):

    N(x)  = -N                  (constante)
    Vz(x) = Vz + Wz·x
    My(x) = My + Vz·x + Wz·x²/2
    Vy(x) = Vy + Wy·x
    Mz(x) = Mz + Vy·x + Wy·x²/2

Cargas por caso (los qG/qQ salen de tributario.tributaria_por_viga):
  - G : w = qG + γ·A            (viga)  + PP de columnas/muros al nodo superior
  - Q : w = qQ                  (viga)  (sin peso propio, igual que en analizar)
  - GQ: w = qG + qQ + γ·A       (viga)  + PP de columnas/muros al nodo superior
  - EX/EY: w = 0                (solo el patrón sísmico sismo.patron_sismico)

Solver idéntico a analizar.py: BandGeneral/RCM/Transformation/Linear.

Cierre por viga y caso (equilibrio del elemento ya cargado): de `localForce`
salen Vzi, Myi en i y Vzj, Myj en j con
    Vz(L) = Vzi - w·L = -Vzj      y    My(L) = Myi + Vzi·L - w·L²/2 = -Myj
(verificado aquí y en el test de cierre con tol < 1e-6·max(1,|valor|)).

Unidades: m, kN.  ADITIVO: no toca geometría, IDs, construir.py, verificar.py
ni el análisis nodal existente.
"""
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import openseespy.opensees as ops
from .construir import build_model
from . import cargas
from . import tributario
from . import sismo
from . import datos_edificio as D

CASOS = ("G", "Q", "GQ", "EX", "EY")


# --------------------------------------------------------------- solver común
def _resolver():
    ops.system('BandGeneral')
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


# ------------------------------------------------------------------- cargas
def _w_viga(caso, info, A):
    """Carga uniforme [kN/m] a aplicar como -beamUniform en la viga `e`."""
    qG = info.get("qG", 0.0)
    qQ = info.get("qQ", 0.0)
    gammaA = cargas.GAMMA_HORMIGON * A
    if caso == "G":
        return qG + gammaA
    if caso == "Q":
        return qQ
    if caso == "GQ":
        return qG + qQ + gammaA
    return 0.0          # EX / EY


def _correr_caso(caso):
    """Construye un modelo nuevo, aplica las cargas del caso y resuelve.

    Devuelve (M, aplicada, w_por_viga). `aplicada` con la misma convención de
    analizar.py: fz magnitud positiva de carga vertical; fx/fy fuerza lateral.
    `w_por_viga` guarda la carga lineal aplicada a cada viga para el Wz.
    """
    M = build_model()
    vigas_c = {v["tag"]: v for v in tributario.tributaria_por_viga(M)}
    aplicada = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    w_por_viga = {}

    if caso in ("G", "Q", "GQ"):
        ops.timeSeries('Constant', 1)
        ops.pattern('Plain', 1, 1)
        conG = caso in ("G", "GQ")

        # vigas: carga distribuida de elemento (beamUniform 0.0, -w)
        for e in sorted(M.tipo):
            if M.tipo[e] != 'viga':
                continue
            A = M.area.get(e, 0.0)
            info = vigas_c.get(e, {})
            w = _w_viga(caso, info, A)
            w_por_viga[e] = w
            if abs(w) < 1e-12:
                continue
            L = _long(*ops.eleNodes(e))
            ops.eleLoad('-ele', e, '-type', '-beamUniform', 0.0, -w)
            aplicada["fz"] += w * L

        # columnas/muros: peso propio nodal al nodo SUPERIOR (solo G y GQ)
        if conG:
            for e in sorted(M.tipo):
                if M.tipo[e] not in ("pilar", "muro"):
                    continue
                A = M.area.get(e, 0.0)
                if not A:
                    continue
                n1, n2 = ops.eleNodes(e)
                z1 = ops.nodeCoord(n1)[2]
                z2 = ops.nodeCoord(n2)[2]
                top = n1 if z1 > z2 else n2
                p = cargas.GAMMA_HORMIGON * A * _long(n1, n2)
                ops.load(top, 0.0, 0.0, -p, 0.0, 0.0, 0.0)
                aplicada["fz"] += p

    elif caso in ("EX", "EY"):
        pesos = sismo.pesos_por_nivel(M)
        dirn = "X" if caso == "EX" else "Y"
        v_base, _fuerzas = sismo.patron_sismico(dirn, M, pesos)
        if caso == "EX":
            aplicada["fx"] = v_base
        else:
            aplicada["fy"] = v_base

    _resolver()
    return M, aplicada, w_por_viga


# ------------------------------------------------------------ extracción
def _extraer(M, w_por_viga):
    """localForce por viga -> {str(e): {L,N,Vy,Vz,T,My,Mz,Wy,Wz}}."""
    out = {}
    for e in sorted(M.tipo):
        if M.tipo[e] != 'viga':
            continue
        n1, n2 = ops.eleNodes(e)
        L = _long(n1, n2)
        f = ops.eleResponse(e, 'localForce')
        if f is None or len(f) < 12:
            continue
        Ni, Vyi, Vzi, Ti, Myi, Mzi, Nj, Vyj, Vzj, Tj, Myj, Mzj = f
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


def _cierre_por_viga(bloque, w_por_viga):
    """Cierre Vz(L)=-Vzj y My(L)=-Myj por viga (para verificación)."""
    res = {}
    for k in sorted(bloque, key=int):
        b = bloque[k]
        e = int(k)
        w = w_por_viga.get(e, 0.0)
        L = b["L"]
        n1, n2 = ops.eleNodes(e)
        f = ops.eleResponse(e, 'localForce')
        if f is None or len(f) < 12:
            continue
        _Ni, _Vyi, _Vzi, _Ti, _Myi, _Mzi, _Nj, _Vyj, Vzj, _Tj, Myj, _Mzj = f
        VzL = b["Vz"] + b["Wz"] * L
        MyL = b["My"] + b["Vz"] * L + b["Wz"] * L * L / 2.0
        res[int(k)] = {
            "dVz": abs(VzL - (-Vzj)),
            "dMy": abs(MyL - (-Myj)),
            "tolVz": 1e-6 * max(1.0, abs(Vzj)),
            "tolMy": 1e-6 * max(1.0, abs(Myj)),
        }
    return res


# ------------------------------------------------------------------- correr
def _correr_todo(verbose=True):
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
        M, aplicada, w_por_viga = _correr_caso(caso)

        # reacciones en la base
        ops.reactions()
        rx = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
        for nt in M.base_nodes:
            r = ops.nodeReaction(nt)
            rx["fx"] += r[0]
            rx["fy"] += r[1]
            rx["fz"] += r[2]

        e_fx = abs(rx["fx"] + aplicada["fx"])
        e_fy = abs(rx["fy"] + aplicada["fy"])
        e_fz = abs(rx["fz"] - aplicada["fz"])
        eq = max(e_fx, e_fy, e_fz) < 1e-3
        ok_eq = ok_eq and eq

        bloque = _extraer(M, w_por_viga)
        cierre = _cierre_por_viga(bloque, w_por_viga)
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


def correr_esfuerzos(verbose=True):
    """Corre la pasada dedicada de diagramas para G/Q/GQ/EX/EY.

    Devuelve {caso: {str(e): {L,N,Vy,Vz,T,My,Mz,Wy,Wz}}}. Además verifica (y
    reporta) el cierre por viga y caso y el equilibrio global de cada pasada.
    """
    por_caso, _ = _correr_todo(verbose=verbose)
    return por_caso


def verificar_diagramas(verbose=True):
    """Corre la pasada y devuelve la verificación completa por caso
    (equilibrio global ΣR + Σaplicada ≈ 0 y cierre Vz(L)=-Vzj / My(L)=-Myj
    con tol 1e-6·max(1,|valor|)). Usado por tests/test_edificio_b/."""
    por_caso, verif = _correr_todo(verbose=verbose)
    return verif, por_caso


if __name__ == '__main__':
    correr_esfuerzos(verbose=True)