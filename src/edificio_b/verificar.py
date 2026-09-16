# -*- coding: utf-8 -*-
"""
verificar.py — Verificaciones numéricas del modelo (AGENTS.md):

  1. Equilibrio global:        ΣF + ΣR ≈ 0
  2. Principio de superposición: u(A+B) = u(A) + u(B)
  3. Test analítico Euler-Bernoulli de referencia (tolerancia < 1e-10)

Uso:
    python verificar.py
"""
import openseespy.opensees as ops
from .construir import build_model, _props
from . import cargas
from . import datos_edificio as D


# ---------------------------------------------------------------- 1) equilibrio
def verificar_equilibrio():
    M = build_model()
    ops.timeSeries('Constant', 1); ops.pattern('Plain', 1, 1)
    Wpp = cargas.peso_propio(M); Wsc = cargas.sobrecarga(M)
    ops.system('BandGeneral'); ops.numberer('RCM')
    ops.constraints('Transformation'); ops.integrator('LoadControl', 1.0)
    ops.algorithm('Linear'); ops.analysis('Static')
    assert ops.analyze(1) == 0, "análisis no convergió"
    ops.reactions()
    Rz = sum(ops.nodeReaction(n)[2] for n in M.base_nodes)
    resid = abs(-(Wpp + Wsc) + Rz)
    print(f"[1] Equilibrio Z: resid = {resid:.3e} kN  (P={Wpp+Wsc:,.0f} kN)")
    return resid < 1e-6 * (Wpp + Wsc)


# ------------------------------------------------------------ 2) superposición
def _resolver(cargas_nodales):
    M = build_model()
    ops.timeSeries('Constant', 1); ops.pattern('Plain', 1, 1)
    for n, fz in cargas_nodales.items():
        ops.load(n, 0, 0, fz, 0, 0, 0)
    ops.system('BandGeneral'); ops.numberer('RCM')
    ops.constraints('Transformation'); ops.integrator('LoadControl', 1.0)
    ops.algorithm('Linear'); ops.analysis('Static')
    ops.analyze(1)
    return M, {n: ops.nodeDisp(n)[2] for n in M.coord}


def verificar_superposicion():
    M = build_model()
    piso = sorted(n for k, s in M.by_level.items() if k >= 1 for n in s)
    nA, nB = piso[0], piso[len(piso) // 2]
    _, uA = _resolver({nA: -50.0})
    _, uB = _resolver({nB: -70.0})
    _, uAB = _resolver({nA: -50.0, nB: -70.0})
    err = max(abs(uAB[n] - (uA[n] + uB[n])) for n in uAB)
    print(f"[2] Superposición: error máx = {err:.3e} m")
    return err < 1e-9


# --------------------------------------------------- 3) Euler-Bernoulli (bench)
def test_euler_bernoulli():
    """Voladizo de 1 elemento con carga puntual P en la punta.
    Analítico: δ = P·L³ / (3·E·I). Tolerancia < 1e-10 (error relativo)."""
    ops.wipe(); ops.model('basic', '-ndm', 3, '-ndf', 6)
    L, b, h, P = 5.0, 0.30, 0.60, 10.0
    E = D.E_CONCRETO; G = D.G_CONCRETO
    A, Iy, Iz, J = _props(b, h)
    ops.node(1, 0, 0, 0); ops.node(2, L, 0, 0)
    ops.fix(1, 1, 1, 1, 1, 1, 1)
    ops.geomTransf('Linear', 1, 0, 0, 1)
    ops.element('elasticBeamColumn', 1, 1, 2, A, E, G, J, Iy, Iz, 1)
    ops.timeSeries('Constant', 1); ops.pattern('Plain', 1, 1)
    ops.load(2, 0, 0, -P, 0, 0, 0)          # carga en Z -> flexión sobre eje local y
    ops.system('BandGeneral'); ops.numberer('Plain')
    ops.constraints('Transformation'); ops.integrator('LoadControl', 1.0)
    ops.algorithm('Linear'); ops.analysis('Static'); ops.analyze(1)
    d_num = abs(ops.nodeDisp(2)[2])
    d_teo = P * L ** 3 / (3.0 * E * Iy)
    err = abs(d_num - d_teo) / d_teo
    print(f"[3] Euler-Bernoulli voladizo: δ_num={d_num:.6e}  δ_teo={d_teo:.6e}  "
          f"err_rel={err:.2e}")
    return err < 1e-10


if __name__ == '__main__':
    r1 = verificar_equilibrio()
    r2 = verificar_superposicion()
    r3 = test_euler_bernoulli()
    print("\nRESUMEN:", "OK" if all([r1, r2, r3]) else "FALLA",
          dict(equilibrio=r1, superposicion=r2, euler_bernoulli=r3))
