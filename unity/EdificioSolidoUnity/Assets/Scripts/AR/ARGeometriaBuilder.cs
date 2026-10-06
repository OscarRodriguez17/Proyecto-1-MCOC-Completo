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
        //  Dibujo DENTRO del recuadro de 4 esquinas (Corrección 8, parte 5a)
        //
        //  Todo en coordenadas del ancla, que va al CENTRO del recuadro y no
        //  rota: los ejes son los del mundo (u, w del recuadro).
        //   · El elemento ocupa el lado largo: i en −u·largo/2, j en +u·largo/2,
        //     y cada x del modelo cae en la MISMA fracción x/L del recuadro.
        //   · El ancho se reparte en tres franjas, una por diagrama (N, V y M
        //     del plano principal), cada una con su eje cero al centro: así no
        //     se enciman y ninguno se sale del recuadro.
        //   · El mayor |valor| de cada diagrama ocupa el 45 % del ancho de su
        //     franja (por la amplitud relativa: «Amplitud ±»).
        //   · El sentido positivo sale del exportador (lado_positivo): en vigas
        //     el M > 0 va hacia abajo (tracción inferior).
        // -----------------------------------------------------------------

        /// <summary>Fracción del semiancho de franja que ocupa el mayor valor (amplitud relativa 1).</summary>
        public const float LlenadoFranja = 0.90f;

        public static ARGeometriaElemento ConstruirEnCuadro(ARElemento el, ARCuadroGeom q,
                                                           ARGeometriaOpciones opt,
                                                           float amplitudRelativa = 1f)
        {
            if (opt == null) opt = new ARGeometriaOpciones();
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

            Vector3 u = q.u, w = q.w;
            float L = q.largo, B = q.ancho;
            Vector3 a = -u * (0.5f * L);
            Vector3 b = u * (0.5f * L);
            g.puntoI = a;
            g.puntoJ = b;
            g.ejeX = u;
            g.ladoPrincipal = w;

            // Recuadro marcado (se apaga con «Eje»).
            Vector3 c00 = a - w * (0.5f * B), c01 = a + w * (0.5f * B);
            Vector3 c10 = b - w * (0.5f * B), c11 = b + w * (0.5f * B);
            g.trazos.Add(Trazo(new[] { c00, c10, c11, c01, c00 }, opt.colorEje,
                               opt.anchoLinea * 0.5f, ARTipoTrazo.Eje, "recuadro"));
            g.marcas.Add(Marca(a - u * 0.04f, opt.colorEje, g.etiquetaI));
            g.marcas.Add(Marca(b + u * 0.04f, opt.colorEje, g.etiquetaJ));
            foreach (var m in g.marcas) { m.radio = 0.03f; m.alturaTexto = Vector3.up * 0.04f; }

            // Referencia del modelo que corresponde a +w en el recuadro.
            Vector3 refModelo = el.tipo == "viga" ? Vector3.up : LadoPrincipal(el);

            string nV = el.principal != null && !string.IsNullOrEmpty(el.principal.V) ? el.principal.V : "V_xz";
            string nM = el.principal != null && !string.IsNullOrEmpty(el.principal.M) ? el.principal.M : "M_xz";
            // Franjas de −w a +w. Viga (w = arriba): M abajo, V, N arriba.
            // Columna (w = izquierda del que mira): N a la derecha, V, M a la izquierda.
            var orden = el.tipo == "viga"
                ? new[] { nM, nV, "N" }
                : new[] { "N", nV, nM };
            float bw = B / orden.Length;
            for (int f = 0; f < orden.Length; f++)
            {
                float wc = -0.5f * B + bw * (f + 0.5f);
                AgregarEnFranja(g, el, orden[f], a, u, w, L, wc, bw, refModelo, opt, amplitudRelativa);
            }

            if (el.pm != null)
            {
                // P–M al lado del recuadro (afuera, por −w), al pie: P a lo largo de u.
                Vector3 origen = a - w * (0.5f * B + 0.12f);
                AgregarPMEn(g, el, opt, origen, u, -w);
            }
            return g;
        }

        private static void AgregarEnFranja(ARGeometriaElemento g, ARElemento el, string nombre,
                                            Vector3 a, Vector3 u, Vector3 w, float L,
                                            float wc, float bw, Vector3 refModelo,
                                            ARGeometriaOpciones opt, float amplitudRelativa)
        {
            ARDiagrama d;
            if (el.diagramas == null || !el.diagramas.TryGetValue(nombre, out d)) return;
            if (d == null || d.valores == null || el.x == null) return;

            ARTipoTrazo tipo = nombre == "N" ? ARTipoTrazo.Normal
                             : nombre.StartsWith("M") ? ARTipoTrazo.Momento : ARTipoTrazo.Cortante;
            Color color = tipo == ARTipoTrazo.Normal ? opt.colorN
                        : tipo == ARTipoTrazo.Momento ? opt.colorM : opt.colorV;

            float signo = 1f;
            if (d.ladoPositivo != null && d.ladoPositivo.vectorUnity != null)
            {
                float dot = Vector3.Dot(V3(d.ladoPositivo.vectorUnity).normalized, refModelo.normalized);
                if (dot <= -0.5f) signo = -1f;
            }

            double mx = 0.0;
            foreach (double v in d.valores) mx = Math.Max(mx, Math.Abs(v));
            float esc = mx < 1e-12 ? 0f
                      : (float)(LlenadoFranja * 0.5f * bw * Mathf.Max(0.05f, amplitudRelativa) / mx);
            double Lm = el.L > 1e-9 ? el.L : 1.0;

            Vector3 base0 = a + w * wc;
            // Eje cero de la franja.
            var cero = Trazo(new[] { base0, base0 + u * L }, color * 0.8f, opt.anchoLinea * 0.3f,
                             tipo, null);
            g.trazos.Add(cero);

            var puntos = new List<Vector3>(d.valores.Length + 2);
            puntos.Add(base0);
            for (int k = 0; k < d.valores.Length && k < el.x.Length; k++)
            {
                float fx = (float)(el.x[k] / Lm);
                puntos.Add(base0 + u * (fx * L) + w * (signo * (float)d.valores[k] * esc));
            }
            puntos.Add(base0 + u * L);
            var trazo = Trazo(puntos, color, opt.anchoLinea * 0.8f, tipo, nombre);
            trazo.principal = true;
            g.trazos.Add(trazo);

            g.marcas.Add(new ARMarca
            {
                posicion = base0 + u * (FraccionRotulo(tipo, true) * L),
                radio = 0.008f,
                color = color,
                texto = Resumen(d, nombre),
                alturaTexto = Vector3.up * 0.015f,
                tipo = tipo,
                principal = true
            });
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

            // N siempre es "principal"; en V y M lo decide el plano (forzarAncho).
            bool esPrincipal = tipo == ARTipoTrazo.Normal || forzarAncho;
            var trazo = Trazo(puntos, color,
                forzarAncho ? opt.anchoLineaPrincipal : opt.anchoLinea,
                tipo, etiqueta != null && etiqueta.Length > 0 ? nombre : null);
            trazo.principal = esPrincipal;
            g.trazos.Add(trazo);

            // Rótulo con los valores característicos. Cada diagrama lo pone a una
            // altura DISTINTA del elemento (antes todos iban al punto medio y se
            // encimaban): N al 20 %, V al 45 %, M al 70 %; los no principales un
            // poco más allá.
            int m = IndiceRotulo(puntos.Count, tipo, esPrincipal);
            g.marcas.Add(new ARMarca
            {
                posicion = puntos[m] + dir * 0.04f,
                radio = 0.012f,
                color = color,
                texto = Resumen(d, nombre),
                alturaTexto = dir * 0.05f,
                tipo = tipo,
                principal = esPrincipal
            });
        }

        /// <summary>Fracción de la longitud del elemento donde va el rótulo de cada diagrama.</summary>
        public static float FraccionRotulo(ARTipoTrazo tipo, bool principal)
        {
            float f;
            switch (tipo)
            {
                case ARTipoTrazo.Normal: f = 0.20f; break;
                case ARTipoTrazo.Cortante: f = 0.45f; break;
                case ARTipoTrazo.Momento: f = 0.70f; break;
                default: f = 0.50f; break;
            }
            return principal ? f : f + 0.12f;
        }

        private static int IndiceRotulo(int n, ARTipoTrazo tipo, bool principal)
        {
            int k = (int)Math.Round(FraccionRotulo(tipo, principal) * (n - 1));
            return Math.Max(0, Math.Min(n - 1, k));
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
            // Rótulo corto (Corrección 6, 3c): «M_xz  i −114.3 · j −159.6 · máx 159.6 kN·m».
            return nombre + "  i " + Fmt1(d.i) + " · j " + Fmt1(d.j)
                 + " · máx " + Fmt1(Math.Abs(d.maxAbs != null ? d.maxAbs.valor : 0.0)) + sufijo;
        }

        private static string Fmt1(double v)
        {
            return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Fmt(double v)
        {
            return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AgregarPM(ARGeometriaElemento g, ARElemento el,
                                      ARGeometriaOpciones opt, Vector3 a,
                                      Vector3 ex, Vector3 ey, Vector3 ez)
        {
            // P vertical (a lo largo del elemento, eje local x) y M horizontal.
            AgregarPMEn(g, el, opt, a + ez * (opt.carril * 2.2f), ex, ey);
        }

        /// <summary>Envolvente P–M con origen y ejes dados (P a lo largo de <paramref name="pDir"/>).</summary>
        private static void AgregarPMEn(ARGeometriaElemento g, ARElemento el, ARGeometriaOpciones opt,
                                        Vector3 origen, Vector3 pDir, Vector3 mDir)
        {
            ARPm pm = el.pm;
            if (pm == null || pm.envolvente == null || pm.envolvente.P == null) return;
            double[] P = pm.envolvente.P;
            double[] M = pm.envolvente.M;

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
                alturaTexto = Vector3.up * 0.05f,
                tipo = ARTipoTrazo.PM
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

            // Corrección 9 (6e): sólo el extremo que GOBIERNA lleva rótulo; los dos
            // rótulos (i y j) caían uno encima del otro.
            g.marcas.Add(new ARMarca
            {
                posicion = c,
                radio = r,
                color = col,
                texto = !gobierna ? null : "G" + pt.P.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                      + " | M" + pt.M.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                      + (gobierna ? "  (gobierna)" : ""),
                alturaTexto = Vector3.up * 0.04f,
                tipo = ARTipoTrazo.Demanda
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
                radio = 0.08f,
                color = c,
                texto = texto,
                alturaTexto = Vector3.up * 0.10f
            };
        }
    }
}
