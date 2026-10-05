using System;
using System.Collections.Generic;
using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Construcción de la geometría (Semana 06)
    //
    //  Regla de oro: aquí NO se calcula ningún esfuerzo. Todo lo dibujado
    //  sale de los arrays `x` y `diagramas[*].valores` del contrato, y el
    //  sentido de dibujo de cada valor positivo lo fija el exportador en
    //  `lado_positivo.vector_unity`. La app no reinterpreta signos.
    // =====================================================================

    public static class ARGeometriaBuilder
    {
        private static Vector3 V3(double[] a)
        {
            if (a == null || a.Length < 3) return Vector3.zero;
            return new Vector3((float)a[0], (float)a[1], (float)a[2]);
        }

        public static List<ARGeometriaElemento> Construir(ARRaiz raiz, ARGeometriaOpciones opt)
        {
            if (opt == null) opt = new ARGeometriaOpciones();
            var salida = new List<ARGeometriaElemento>();
            if (raiz == null) return salida;
            foreach (var el in raiz.EnOrden()) salida.Add(ConstruirElemento(el, opt));
            return salida;
        }

        public static ARGeometriaElemento ConstruirElemento(ARElemento el, ARGeometriaOpciones opt)
        {
            if (opt == null) opt = new ARGeometriaOpciones();

            // 1:1. El desplazamiento NO es un ajuste: es el opuesto del punto de
            // anclaje que calculó el exportador. Restarlo deja el elemento
            // centrado en el ancla y a su altura real sobre ese piso.
            if (el.anclaje != null) opt.desplazamiento = -V3(el.anclaje.puntoUnity);
            Vector3 desp = opt.desplazamiento;

            var g = new ARGeometriaElemento
            {
                tag = el.tag,
                tipo = el.tipo,
                seccion = el.seccion,
                material = el.material,
                ubicacion = el.ubicacion,
                planoPrincipal = el.principal != null ? el.principal.plano : "-",
                etiquetaI = el.extremos != null && el.extremos.i != null ? el.extremos.i.etiqueta : "",
                etiquetaJ = el.extremos != null && el.extremos.j != null ? el.extremos.j.etiqueta : "",
                longitud = el.L,
                tienePm = el.pm != null
            };

            Vector3 a = V3(el.extremos.i.xyzUnity) + desp;
            Vector3 b = V3(el.extremos.j.xyzUnity) + desp;

            Vector3 ex = Eje(el, "x", b - a);
            Vector3 ey = Eje(el, "y", Perpendicular(ex));
            Vector3 ez = Eje(el, "z", Vector3.Cross(ex, ey));

            g.puntoI = a;
            g.puntoJ = b;
            g.ejeX = ex;
            g.ladoPrincipal = LadoPrincipal(el);

            g.trazos.Add(Trazo(new[] { a, b }, opt.colorEje, opt.anchoLinea * 0.8f,
                               ARTipoTrazo.Eje, "eje"));
            g.marcas.Add(Marca(a, opt.colorEje, g.etiquetaI));
            g.marcas.Add(Marca(b, opt.colorEje, g.etiquetaJ));

            // N: axial, dibujado desde el eje en la dirección local z.
            Agregar(g, el, "N", ez, Vector3.zero, 0.0, opt.colorN,
                    ARTipoTrazo.Normal, opt, "", a, ex);

            // Planos: en x-z los carriles van por el eje local y; en x-y por el z.
            string planoP = el.principal != null ? el.principal.M : "M_xz";
            string planoV = el.principal != null ? el.principal.V : "V_xz";

            AgregarPlano(g, el, "M_xz", a, ex, ey, ez, +1.0, opt, planoP);
            AgregarPlano(g, el, "V_xz", a, ex, ey, ez, -1.0, opt, planoV);
            AgregarPlano(g, el, "M_xy", a, ex, ez, ey, +1.0, opt, planoP);
            AgregarPlano(g, el, "V_xy", a, ex, ez, ey, -1.0, opt, planoV);

            if (el.pm != null) AgregarPM(g, el, opt, a, ex, ey, ez);

            return g;
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// Dirección hacia la que la app dibuja el momento PRINCIPAL &gt; 0
        /// (su lado de tracción). La fija el exportador; aquí sólo se copia.
        /// </summary>
        private static Vector3 LadoPrincipal(ARElemento el)
        {
            if (el.principal == null || el.diagramas == null) return Vector3.forward;
            ARDiagrama d;
            if (!el.diagramas.TryGetValue(el.principal.M, out d)) return Vector3.forward;
            if (d == null || d.ladoPositivo == null) return Vector3.forward;
            Vector3 v = V3(d.ladoPositivo.vectorUnity);
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
        }

        private static Vector3 Eje(ARElemento el, string nombre, Vector3 respaldo)
        {
            if (el.ejes_locales != null && el.ejes_locales.unity != null)
            {
                Vector3 v;
                switch (nombre)
                {
                    case "x": v = V3(el.ejes_locales.unity.x); break;
                    case "y": v = V3(el.ejes_locales.unity.y); break;
                    default: v = V3(el.ejes_locales.unity.z); break;
                }
                if (v.sqrMagnitude > 1e-8f) return v.normalized;
            }
            return respaldo.sqrMagnitude > 1e-8f ? respaldo.normalized : Vector3.right;
        }

        private static Vector3 Perpendicular(Vector3 v)
        {
            Vector3 p = Vector3.Cross(v, Vector3.up);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(v, Vector3.right);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.forward;
            return p.normalized;
        }

        private static void AgregarPlano(ARGeometriaElemento g, ARElemento el,
                                         string nombre, Vector3 origen, Vector3 ex,
                                         Vector3 carrilDir, Vector3 dirPorDefecto,
                                         double carril, ARGeometriaOpciones opt,
                                         string nombrePrincipal)
        {
            ARDiagrama d;
            if (el.diagramas == null || !el.diagramas.TryGetValue(nombre, out d)) return;
            bool principal = nombre == nombrePrincipal;
            Color color = nombre.StartsWith("M") ? opt.colorM : opt.colorV;
            Agregar(g, el, nombre, dirPorDefecto, carrilDir, carril, color,
                    nombre.StartsWith("M") ? ARTipoTrazo.Momento : ARTipoTrazo.Cortante,
                    opt, principal ? nombre : "", origen, ex, principal);
        }

        private static void Agregar(ARGeometriaElemento g, ARElemento el, string nombre,
                                    Vector3 dirPorDefecto, Vector3 carrilDir, double carril,
                                    Color color, ARTipoTrazo tipo, ARGeometriaOpciones opt,
                                    string etiqueta, Vector3 origen, Vector3 ex,
                                    bool forzarAncho = false)
        {
            ARDiagrama d;
            if (el.diagramas == null || !el.diagramas.TryGetValue(nombre, out d)) return;
            if (d.valores == null || el.x == null) return;

            Vector3 dir = dirPorDefecto;
            if (d.ladoPositivo != null && d.ladoPositivo.vectorUnity != null)
            {
                Vector3 v = V3(d.ladoPositivo.vectorUnity);
                if (v.sqrMagnitude > 1e-8f) dir = v.normalized;
            }

            float escala = Escala(d, opt.amplitud);

            var puntos = new List<Vector3>(d.valores.Length);
            for (int k = 0; k < d.valores.Length && k < el.x.Length; k++)
            {
                puntos.Add(origen
                           + ex * (float)el.x[k]
                           + carrilDir * (float)Math.Abs(carril) * opt.carril
                           + dir * (float)(d.valores[k] * escala));
            }
            if (puntos.Count < 2) return;

            g.trazos.Add(Trazo(puntos, color,
                forzarAncho ? opt.anchoLineaPrincipal : opt.anchoLinea,
                tipo, etiqueta != null && etiqueta.Length > 0 ? nombre : null));

            // Etiqueta con los valores característicos en el punto medio.
            int m = puntos.Count / 2;
            g.marcas.Add(new ARMarca
            {
                posicion = puntos[m] + dir * 0.04f,
                radio = 0.012f,
                color = color,
                texto = Resumen(d, nombre),
                alturaTexto = dir * 0.05f
            });
        }

        /// <summary>Escala lineal única del diagrama: el mayor valor ocupa `amplitud`.</summary>
        private static float Escala(ARDiagrama d, float amplitud)
        {
            double mx = 0.0;
            for (int k = 0; k < d.valores.Length; k++)
                mx = Math.Max(mx, Math.Abs(d.valores[k]));
            if (mx < 1e-12) return 0f;
            return (float)(amplitud / mx);
        }

        private static string Resumen(ARDiagrama d, string nombre)
        {
            string sufijo = nombre.StartsWith("M") ? " kN\u00b7m"
                          : nombre.StartsWith("V") ? " kN"
                          : " kN";
            return nombre + ": i=" + Fmt(d.i) + "  j=" + Fmt(d.j) + sufijo
                 + "  |max|=" + Fmt(Math.Abs(d.maxAbs != null ? d.maxAbs.valor : 0.0));
        }

        private static string Fmt(double v)
        {
            return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AgregarPM(ARGeometriaElemento g, ARElemento el,
                                      ARGeometriaOpciones opt, Vector3 a,
                                      Vector3 ex, Vector3 ey, Vector3 ez)
        {
            ARPm pm = el.pm;
            if (pm.envolvente == null || pm.envolvente.P == null) return;
            double[] P = pm.envolvente.P;
            double[] M = pm.envolvente.M;

            // P vertical (a lo largo del elemento, eje local x) y M horizontal.
            Vector3 pDir = ex;
            Vector3 mDir = ey;
            Vector3 origen = a + ez * (opt.carril * 2.2f);

            double maxP = 1e-9, maxM = 1e-9;
            for (int k = 0; k < P.Length; k++)
            {
                maxP = Math.Max(maxP, Math.Abs(P[k]));
                maxM = Math.Max(maxM, Math.Abs(M[k]));
            }
            float sp = (float)(opt.anchoPM * 0.65 / maxP);
            float sm = (float)(opt.anchoPM / maxM);

            var linea = new List<Vector3>();
            for (int k = 0; k < P.Length && k < M.Length; k++)
                linea.Add(origen + pDir * (float)(P[k] * sp) + mDir * (float)(M[k] * sm));
            if (linea.Count >= 2)
                g.trazos.Add(Trazo(linea, opt.colorPM, opt.anchoLinea * 1.2f,
                                   ARTipoTrazo.PM, "envolvente P-M"));

            // Eje P hasta el máximo.
            g.trazos.Add(Trazo(new[] { origen, origen + pDir * (float)(maxP * sp) },
                               opt.colorPM, opt.anchoLinea * 0.6f, ARTipoTrazo.Eje, "eje P"));
            g.trazos.Add(Trazo(new[] { origen, origen + mDir * (float)(maxM * sm) },
                               opt.colorPM, opt.anchoLinea * 0.6f, ARTipoTrazo.Eje, "eje M"));

            if (pm.demanda != null)
            {
                Demandar(g, opt, origen, pDir, mDir, sp, sm, pm.demanda.i, "i", pm.gobierna == "i");
                Demandar(g, opt, origen, pDir, mDir, sp, sm, pm.demanda.j, "j", pm.gobierna == "j");
            }

            g.marcas.Add(new ARMarca
            {
                posicion = origen + pDir * (float)(maxP * sp) + mDir * (float)(maxM * sm),
                radio = 0.01f,
                color = opt.colorPM,
                texto = "P-M " + pm.seccion + "   DC=" + Fmt(pm.DC)
                      + "   M(P)=" + Fmt(pm.mCapacidadEnP) + " kN\u00b7m",
                alturaTexto = Vector3.up * 0.05f
            });
        }

        private static void Demandar(ARGeometriaElemento g, ARGeometriaOpciones opt,
                                     Vector3 origen, Vector3 pDir, Vector3 mDir,
                                     float sp, float sm, ARPunto pt, string lado, bool gobierna)
        {
            if (pt == null) return;
            Vector3 c = origen + pDir * (float)(pt.P * sp) + mDir * (float)(pt.M * sm);
            float r = gobierna ? 0.035f : 0.022f;
            Color col = gobierna ? opt.colorDemanda : new Color(1f, 1f, 1f, 0.75f);

            g.marcas.Add(new ARMarca
            {
                posicion = c,
                radio = r,
                color = col,
                texto = "G" + pt.P.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                      + " | M" + pt.M.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                      + (gobierna ? "  (gobierna)" : ""),
                alturaTexto = Vector3.up * 0.04f
            });
        }

        // -----------------------------------------------------------------

        private static ARTrazo Trazo(List<Vector3> pts, Color c, float ancho,
                                     ARTipoTrazo tipo, string etiqueta)
        {
            return new ARTrazo
            {
                puntos = pts,
                color = c,
                ancho = ancho,
                tipo = tipo,
                etiqueta = etiqueta
            };
        }

        private static ARTrazo Trazo(Vector3[] pts, Color c, float ancho,
                                     ARTipoTrazo tipo, string etiqueta)
        {
            return Trazo(new List<Vector3>(pts), c, ancho, tipo, etiqueta);
        }

        private static ARMarca Marca(Vector3 p, Color c, string texto)
        {
            return new ARMarca
            {
                posicion = p,
                radio = 0.028f,
                color = c,
                texto = texto,
                alturaTexto = Vector3.up * 0.035f
            };
        }
    }
}
