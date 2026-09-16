"""
Tests del modelo benchmark 3D — Edificio de Ingenieria (OpenSeesPy).
Ejecutar: pytest tests/test_benchmark.py -v

Verificaciones exigidas en AGENTS.md:
  - Conservacion de areas tributarias.
  - Equilibrio global por caso.
  - Superposicion numerica R(G)+R(Q) == R(GQ).
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..",
                                "src", "benchmark_3d"))

import datos_edificio as d          # noqa: E402
import construir                    # noqa: E402
import cargas                       # noqa: E402
from analizar import correr_caso    # noqa: E402
import voladizos                    # noqa: E402


def _correr(caso):
    """Construye el modelo desde cero y corre un caso. Retorna resultados."""
    import openseespy.opensees as ops
    ops.wipe()
    modelo = construir.construir()
    return modelo, correr_caso(caso, modelo)


def _area_real():
    """Area de losa que recibe carga de gravedad (voladizo solo en anexo)."""
    xs = sorted(set(d.GRID_X.values()))
    ys = sorted(set(d.GRID_Y.values()))
    x_borde = d.GRID_X[d.ANEXO["x0"]]
    anexo_lv = d.anexo_levels()
    principal = x_borde * (ys[-1] - ys[0])
    voladizo = (xs[-1] - x_borde) * (ys[-1] - ys[0])
    n_vol = len([lv for lv in range(1, d.NLEV) if lv in anexo_lv])
    return principal * (d.NLEV - 1) + voladizo * n_vol


class TestGeometria:
    def test_reticula_x_50m(self):
        assert abs(max(d.GRID_X.values()) - min(d.GRID_X.values()) - 50.0) < 1e-9

    def test_voladizo_metalico_5m(self):
        assert abs(d.GRID_X[d.ANEXO["x1"]] - d.GRID_X[d.ANEXO["x0"]] - 5.0) < 1e-9

    def test_reticula_y_16_15m(self):
        assert abs(max(d.GRID_Y.values()) - min(d.GRID_Y.values()) - 16.15) < 1e-9

    def test_area_huella_total(self):
        assert abs(max(d.GRID_X.values()) * 16.15 - 50.0 * 16.15) < 1e-9

    def test_anexo_solo_2_niveles_superiores(self):
        assert d.anexo_levels() == {3, 4}

    def test_altura_piso_constante_salvo_primera(self):
        assert d.STORY_H[0] == 4.16
        for h in d.STORY_H[1:]:
            assert abs(h - 3.96) < 1e-9


class TestSecciones:
    def test_pilar_70x70(self):
        A, Iy, Iz, J = d.sec_columna()
        assert abs(A - 0.49) < 1e-12
        assert abs(Iy - 0.7 ** 4 / 12.0) < 1e-12
        assert abs(Iz - Iy) < 1e-12

    def test_viga_fuerte_vs_debil(self):
        _, _, i_strong, _ = d.sec_viga()
        _, i_weak, _, _ = d.sec_viga()
        assert i_strong > i_weak

    def test_j_muro_san_venant(self):
        _, _, _, J = d.sec_muro(0.30, 8.90, "Y")
        j_ref = (1.0 / 3.0) * 8.9 * 0.3 ** 3 * (1.0 - 0.63 * 0.3 / 8.9)
        assert abs(J - j_ref) < 1e-12
        assert J < 0.1

    def test_muro_resiste_y_tiene_inercia_grande_en_z_local(self):
        _, Iy_w, Iz_w, _ = d.sec_muro(0.20, 7.25, "Y")
        assert Iz_w > Iy_w

    def test_area_cortante_muro_5_6_de_a(self):
        Ay, Az = d.shear_area_muro(0.30, 8.90, "Y")
        assert abs(Ay - 5.0 / 6.0 * 0.30 * 8.90) < 1e-12
        assert abs(Az - Ay) < 1e-12

    def test_viga_compuesta_inercia_mayor_que_rectangular(self):
        A_c, _, I_c, _ = d.sec_viga()
        A_T, _, I_T, _ = d.sec_viga_compuesta(10.0, "T")
        assert I_T > I_c
        assert A_T > A_c

    def test_viga_compuesta_T_mayor_que_L(self):
        A_T, _, I_T, _ = d.sec_viga_compuesta(10.0, "T")
        A_L, _, I_L, _ = d.sec_viga_compuesta(10.0, "L")
        assert I_T > I_L
        assert A_T > A_L

    def test_viga_compuesta_ancho_efectivo_aci(self):
        A, _, I, _ = d.sec_viga_compuesta(10.0, "T")
        h_f, b_e = d.SLAB_H, d.BEAM_B + 2 * (9.3 / 4.0 - d.BEAM_B) / 2
        b_e_ef = (A - d.BEAM_B * d.BEAM_H) / h_f + d.BEAM_B
        assert abs(b_e_ef - 9.3 / 4.0) < 1e-9

    def test_viga_compuesta_respeta_sin_alas_viga_corta(self):
        A, _, _, _ = d.sec_viga_compuesta(2.31, "T")
        assert A >= d.BEAM_B * d.BEAM_H


class TestCargas:
    def test_conservacion_tributaria_g(self):
        modelo = construir.construir()
        _, _, _, transf = cargas.distribuir_tributaria(d.Q_G, modelo)
        esperado = d.Q_G * _area_real()
        assert abs(transf - esperado) < 1e-6

    def test_q_caso_exacto(self):
        modelo = construir.construir()
        _, tot = cargas.cargas_gravedad(d.Q_Q, modelo, incluir_pp=False)
        esperado = d.Q_Q * _area_real()
        assert abs(tot - esperado) < 1e-9

    def test_sismo_v_alfa_por_w(self):
        modelo = construir.construir()
        pesos = cargas.pesos_por_nivel(modelo)
        v_base, _ = cargas.patron_sismico("X", modelo, pesos)
        assert abs(v_base - d.ALPHA_EQ * sum(pesos.values())) < 1e-9


class TestEquilibrio:
    def test_caso_g(self):
        _, res = _correr("G")
        err = abs(res["reacciones"]["fz"] - res["aplicada"]["fz"])
        assert err < 1e-6

    def test_caso_ex(self):
        _, res = _correr("EX")
        rx = res["reacciones"]
        assert abs(rx["fx"] + res["aplicada"]["fx"]) < 1e-6
        assert abs(rx["fy"]) < 1e-6
        assert abs(rx["fz"]) < 1e-6

    def test_momento_volcantante_positivo(self):
        _, res = _correr("EX")
        assert res["aplicada"]["momento_volcante"] > 0.0


class TestSuperposicion:
    def test_rg_mas_rq_igual_rgq(self):
        _, g = _correr("G")
        _, q = _correr("Q")
        _, gq = _correr("GQ")
        nodos = set(gq["reacciones"]["por_nodo"].keys())
        max_dif = 0.0
        for nt in nodos:
            for i in (0, 1, 2):
                dif = abs(g["reacciones"]["por_nodo"][nt][i]
                          + q["reacciones"]["por_nodo"][nt][i]
                          - gq["reacciones"]["por_nodo"][nt][i])
                max_dif = max(max_dif, dif)
        assert max_dif < 1e-6


class TestReferenciasAnaliticas:
    """Comparaciones contra soluciones manuales (error objetivo < 1 %)."""

    def test_reaccion_total_gravedad(self):
        import openseespy.opensees as ops
        ops.wipe()
        modelo = construir.construir()
        _, tot_aplicada = cargas.cargas_gravedad(d.Q_G, modelo)
        ops.system("BandGen")
        ops.numberer("RCM")
        ops.constraints("Transformation")
        ops.integrator("LoadControl", 1.0)
        ops.algorithm("Linear")
        ops.analysis("Static")
        ok = ops.analyze(1)
        assert ok == 0, "Analisis fallo"
        ops.reactions()
        sum_rz = sum(ops.nodeReaction(nt)[2] for nt in ops.getFixedNodes())
        err = abs(sum_rz - tot_aplicada) / tot_aplicada
        assert err < 1e-6, f"Error reaccion total G: {err:.2e}"

    def test_viga_flecha_euler_bernoulli(self):
        import openseespy.opensees as ops
        ops.wipe()
        ops.model("basic", "-ndm", 3, "-ndf", 6)

        L = 6.0
        b, h = 0.30, 0.50
        A = b * h
        Iz = b * h ** 3 / 12.0
        E = 25.0e6
        G = E / (2.0 * (1.0 + 0.2))
        P = 60.0

        ops.node(1, 0.0, 0.0, 0.0)
        ops.node(2, L / 2, 0.0, 0.0)
        ops.node(3, L, 0.0, 0.0)

        ops.fix(1, 1, 1, 1, 1, 0, 0)
        ops.fix(3, 0, 1, 1, 1, 0, 0)

        ops.geomTransf("Linear", 1, 0.0, 0.0, 1.0)

        ops.element("elasticBeamColumn", 1, 1, 2, A, E, G, 1e6, Iz, 1e6, 1)
        ops.element("elasticBeamColumn", 2, 2, 3, A, E, G, 1e6, Iz, 1e6, 1)

        ops.timeSeries("Linear", 1)
        ops.pattern("Plain", 1, 1)
        ops.load(2, 0.0, 0.0, -P, 0.0, 0.0, 0.0)

        ops.constraints("Plain")
        ops.numberer("RCM")
        ops.system("BandGeneral")
        ops.algorithm("Linear")
        ops.integrator("LoadControl", 1.0)
        ops.analysis("Static")
        ok = ops.analyze(1)
        assert ok == 0

        uz = ops.nodeDisp(2, 3)
        delta_ref = P * L ** 3 / (48.0 * E * Iz)
        err = abs(abs(uz) - delta_ref) / delta_ref
        assert err < 1.0e-10, (
            f"Flecha: OS={abs(uz):.6e}, Ref={delta_ref:.6e}, err={err:.2e}")
        ops.wipe()

    def test_columna_carga_axial(self):
        import openseespy.opensees as ops
        ops.wipe()
        ops.model("basic", "-ndm", 3, "-ndf", 6)

        A_c, _, _, J_c = d.sec_columna()
        H = 4.0
        P = 200.0

        ops.node(1, 0.0, 0.0, 0.0)
        ops.node(2, 0.0, 0.0, H)

        ops.fix(1, 1, 1, 1, 1, 1, 1)

        ops.geomTransf("Linear", 1, 1.0, 0.0, 0.0)
        ops.element("elasticBeamColumn", 1, 1, 2, A_c, d.E_C, d.G_C,
                     J_c, 0.7**4/12, 0.7**4/12, 1)

        ops.timeSeries("Linear", 1)
        ops.pattern("Plain", 1, 1)
        ops.load(2, 0.0, 0.0, -P, 0.0, 0.0, 0.0)

        ops.constraints("Plain")
        ops.numberer("RCM")
        ops.system("BandGeneral")
        ops.algorithm("Linear")
        ops.integrator("LoadControl", 1.0)
        ops.analysis("Static")
        ok = ops.analyze(1)
        assert ok == 0

        ops.reactions()
        Rz = ops.nodeReaction(1, 3)
        err = abs(Rz - P) / P
        assert err < 1e-6, (
            f"Reaccion: OS={Rz:.2f}, Ref={P:.2f}, err={err:.2e}")
        ops.wipe()


class TestVoladizo:
    """Integracion del voladizo trasero (voladizos.py) sobre el modelo base."""

    def _modelo_con_voladizo(self):
        import openseespy.opensees as ops
        ops.wipe()
        modelo = construir.construir()
        extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
        return voladizos.integrar_voladizo(modelo, extra)

    def test_tags_por_encima_del_base(self):
        import openseespy.opensees as ops
        ops.wipe()
        modelo = construir.construir()
        base_n = max(ops.getNodeTags())
        base_e = max(ops.getEleTags())
        extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
        assert len(extra["nodos"]) > 0
        assert all(nd["tag"] > base_n for nd in extra["nodos"])
        nuevos = extra["nervaduras"] + extra["vigas_borde"] + extra["postes"]
        assert len(nuevos) > 0
        assert all(el["tag"] > base_e for el in nuevos)

    def test_equilibrio_g_con_voladizo(self):
        modelo = self._modelo_con_voladizo()
        res = correr_caso("G", modelo)
        err = abs(res["reacciones"]["fz"] - res["aplicada"]["fz"])
        assert err < 1e-6

    def test_peso_vigas_g_incluye_auto_peso_voladizo(self):
        modelo = self._modelo_con_voladizo()
        _, tot = cargas.cargas_gravedad(d.Q_G, modelo)
        _, base = cargas.cargas_gravedad(d.Q_G, construir.construir())
        assert tot > base

    def test_superposicion_con_voladizo(self):
        _, g = _correr_con_voladizo("G")
        _, q = _correr_con_voladizo("Q")
        _, gq = _correr_con_voladizo("GQ")
        nodos = set(gq["reacciones"]["por_nodo"].keys())
        max_dif = 0.0
        for nt in nodos:
            for i in (0, 1, 2):
                max_dif = max(max_dif, abs(
                    g["reacciones"]["por_nodo"][nt][i]
                    + q["reacciones"]["por_nodo"][nt][i]
                    - gq["reacciones"]["por_nodo"][nt][i]))
        assert max_dif < 1e-6


def _modelo_completo():
    """Voladizo trasero + aspa + voladizo metalico de piso 1 (eje F)."""
    import openseespy.opensees as ops
    ops.wipe()
    modelo = construir.construir()
    extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
    extra_er = voladizos.agregar_arriostramiento_voladizo(modelo=modelo,
                                                          extra=extra)
    extra_f = voladizos.agregar_voladizo_piso1_ejeF(modelo=modelo)
    completo = dict(extra)
    completo["aspas"] = extra_er.get("aspas", [])
    completo["vol_f"] = extra_f
    return voladizos.integrar_voladizo(modelo, completo)


def _modelo_con_voladizo():
    """Solo voladizo trasero (referencia para comparar pesos)."""
    import openseespy.opensees as ops
    ops.wipe()
    modelo = construir.construir()
    extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
    return voladizos.integrar_voladizo(modelo, extra)


class TestAspasYVoladizoF:
    """Arriostramiento (aspa) + voladizo metalico de piso 1 (eje F).

    La extension de hormigon trasera (base_ext/columna_ext) ya NO esta en el
    modelo: el plano 101 la rotula "RADIER SOBRE TERRENO" (losa apoyada en el
    suelo, estructuralmente separada).
    """

    def test_modelo_base_intacto_190_321(self):
        import openseespy.opensees as ops
        ops.wipe()
        construir.construir()
        assert len(ops.getNodeTags()) == 190
        assert len(ops.getEleTags()) == 321
        ops.wipe()

    def test_dos_aspas_con_longitud_y_geometria(self):
        import openseespy.opensees as ops
        ops.wipe()
        modelo = construir.construir()
        extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
        er = voladizos.agregar_arriostramiento_voladizo(modelo=modelo,
                                                        extra=extra)
        assert len(er["aspas"]) == 2              # ejes G y H
        for a in er["aspas"]:
            assert a["story"] == 3
            zlo = ops.nodeCoord(a["ni"])[2]
            zhi = ops.nodeCoord(a["nj"])[2]
            assert abs(zhi - zlo) > 3.0
            dx = abs(ops.nodeCoord(a["ni"])[0] - ops.nodeCoord(a["nj"])[0])
            dy = ops.nodeCoord(a["ni"])[1] - ops.nodeCoord(a["nj"])[1]
            assert dx < 1e-9                       # mismo eje X
            assert dy < 0.0                        # punta(-Y) -> A3(+Y)
            assert a["A_pp"] > 0.0
            assert a["rho_pp"] == d.GAMMA_STEEL
        ops.wipe()

    def test_voladizo_ejeF_rectangulo(self):
        import openseespy.opensees as ops
        ops.wipe()
        modelo = construir.construir()
        base_e = len(ops.getEleTags())
        f = voladizos.agregar_voladizo_piso1_ejeF(modelo=modelo)
        # rectangulo: 6 nodos (4 puntas + 2 raices de borde) y 12 elementos
        assert len(f["nodos"]) == 6
        assert len(f["vigas_y"]) == 4          # nervaduras F y borde, x2 niveles
        assert len(f["vigas_x"]) == 4          # vigas de punta + de raiz, x2
        assert len(f["poste"]) == 2            # poste en cada punta
        assert len(f["diagonal"]) == 2         # diagonal en cada nervadura
        puntas = [nd for nd in f["nodos"] if nd["rol"] == "punta_vol_f"]
        raices = [nd for nd in f["nodos"] if nd["rol"] == "raiz_vol_f"]
        assert len(puntas) == 4 and len(raices) == 2
        for nd in puntas:
            assert nd["lvl"] in (1, 2)
        pos = {(round(nd["x"], 9), round(nd["y"], 9), round(nd["z"], 9))
               for nd in puntas}
        assert pos == {(10.0, -4.30, d.LEVEL_Z[1]), (10.0, -4.30, d.LEVEL_Z[2]),
                       (17.5, -4.30, d.LEVEL_Z[1]), (17.5, -4.30, d.LEVEL_Z[2])}
        assert len(ops.getEleTags()) - base_e == 12
        assert all(el["tag"] > base_e for el in
                   f["vigas_y"] + f["vigas_x"] + f["poste"] + f["diagonal"])
        ops.wipe()

    def test_sin_roles_ni_geometria_de_extension(self):
        import openseespy.opensees as ops
        ops.wipe()
        modelo = construir.construir()
        extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
        extra_f = voladizos.agregar_voladizo_piso1_ejeF(modelo=modelo)
        completo = dict(extra)
        completo["vol_f"] = extra_f
        modelo = voladizos.integrar_voladizo(modelo, completo)
        for nd in modelo["voladizo"]["nodos"]:
            assert nd.get("rol") not in ("base_ext", "columna_ext")
            # la extension vieja tenia columnas en x 30/34.5/40.5 con y<0;
            # (los nodos del voladizo trasero reutilizan x 30/40 con y<0,
            #  y por eso solo se descarta la geometria exclusiva: x 34.5/40.5)
            if nd["y"] < 0.0:
                assert nd["x"] not in (34.5, 40.5)
        assert not any("extension" in str(k) for k in modelo["voladizo"])
        ops.wipe()

    def test_equilibrio_g_con_pipeline_completo(self):
        modelo = _modelo_completo()
        res = correr_caso("G", modelo)
        err = abs(res["reacciones"]["fz"] - res["aplicada"]["fz"])
        assert err < 1e-6

    def test_peso_total_mayor_que_solo_voladizo(self):
        full = _modelo_completo()
        _, w_full = cargas.cargas_gravedad(d.Q_G, full)
        vol = _modelo_con_voladizo()
        _, w_vol = cargas.cargas_gravedad(d.Q_G, vol)
        assert w_full > w_vol

    def test_pesos_por_nivel_suman_gravedad_total(self):
        modelo = _modelo_completo()
        niveles = cargas.pesos_por_nivel(modelo)
        _, tot = cargas.cargas_gravedad(d.Q_G, modelo)
        assert abs(sum(niveles.values()) - tot) < 1e-6 * max(tot, 1.0)
        assert len(modelo["aspas"]) == 4          # 2 aspas voladizo + 2 diagonales F
        ref = sum(a["rho_pp"] * a["A_pp"] * a["L"] for a in modelo["aspas"])
        assert ref > 0.0
        assert 3 in niveles and 4 in niveles


def _correr_con_voladizo(caso):
    import openseespy.opensees as ops
    ops.wipe()
    modelo = construir.construir()
    extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
    modelo = voladizos.integrar_voladizo(modelo, extra)
    return modelo, correr_caso(caso, modelo)


class TestTablaVerificacion:
    """Genera la tabla de verificacion para el reporte."""

    def test_tabla_equilibrio(self):
        tabla = []
        for caso in ("G", "Q", "GQ", "EX", "EY"):
            _, res = _correr(caso)
            rx = res["reacciones"]
            ap = res["aplicada"]
            for comp, r_val, a_val in [("Fx", rx["fx"], ap["fx"]),
                                        ("Fy", rx["fy"], ap["fy"]),
                                        ("Fz", rx["fz"], ap["fz"])]:
                if abs(a_val) > 1e-9:
                    if comp == "Fz":
                        error = abs(abs(r_val) - abs(a_val)) / abs(a_val)
                    else:
                        error = abs(r_val + a_val) / abs(a_val)
                else:
                    error = abs(r_val) if comp != "Fz" else abs(r_val - a_val)
                tabla.append((caso, comp, a_val, r_val, error))

        for caso, comp, a, r, err in tabla:
            assert err < 0.01, f"{caso}/{comp}: error {err:.3f} > 1%"

    def test_print_tabla(self, capsys):
        print("\n| Magnitud | Referencia | OpenSees | Error (%) |")
        print("|---|---|---|---|")
        for caso in ("G", "Q", "GQ", "EX", "EY"):
            _, res = _correr(caso)
            rx = res["reacciones"]
            ap = res["aplicada"]
            for comp, r_val, a_val in [("Fx", rx["fx"], ap["fx"]),
                                        ("Fy", rx["fy"], ap["fy"]),
                                        ("Fz", rx["fz"], ap["fz"])]:
                if abs(a_val) > 1e-9:
                    if comp == "Fz":
                        err_pct = abs(abs(r_val) - abs(a_val)) / abs(a_val) * 100
                    else:
                        err_pct = abs(r_val + a_val) / abs(a_val) * 100
                else:
                    err_pct = abs(r_val) * 100 if comp != "Fz" else abs(r_val - a_val) * 100
                ref_str = f"{a_val:+.2f}" if abs(a_val) > 1e-9 else "0.00"
                print(f"| {caso}-{comp} | {ref_str} | {r_val:+.2f} | {err_pct:.6f} |")
