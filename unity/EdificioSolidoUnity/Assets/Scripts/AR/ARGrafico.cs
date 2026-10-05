using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Panel 2D de diagramas (Corrección 6, parte 3d)
    //
    //  Respaldo legible del AR: un recuadro FIJO en la pantalla con los
    //  diagramas N, V y M del plano principal del elemento elegido, como la
    //  figura de referencia (reports/fig/ar_ref_tag*.png). Se lee siempre,
    //  aunque el AR no calce perfecto con el elemento real.
    //
    //  Esta clase dibuja el gráfico en una textura (píxeles) a partir de los
    //  MISMOS valores muestreados del contrato que usa el AR: no recalcula nada.
    //  Convención de la figura de referencia: en VIGAS el momento positivo se
    //  dibuja hacia ABAJO (tracción en la fibra inferior).
    // =====================================================================

    public static class ARGrafico
    {
        public const int AnchoPorDefecto = 720;
        public const int AltoPorDefecto = 480;

        /// <summary>Un diagrama listo para dibujar.</summary>
        public class Serie
        {
            public string nombre;      // "N", "V_xz", "M_xz"...
            public string unidad;      // "kN" o "kN·m"
            public double[] x;         // posiciones a lo largo del elemento (m)
            public double[] v;         // valores
            public bool invertir;      // true: positivo hacia abajo (M en vigas)
            public double i, j, maxAbs;
            public Color32 color;
        }

        /// <summary>N, V y M del plano principal del elemento (en ese orden).</summary>
        public static List<Serie> Series(ARElemento el)
        {
            var r = new List<Serie>();
            if (el == null || el.diagramas == null || el.x == null) return r;
            string v = el.principal != null && !string.IsNullOrEmpty(el.principal.V) ? el.principal.V : "V_xz";
            string m = el.principal != null && !string.IsNullOrEmpty(el.principal.M) ? el.principal.M : "M_xz";
            bool viga = el.tipo == "viga";
            Agregar(r, el, "N", "kN", false, new Color32(77, 158, 242, 255));
            Agregar(r, el, v, "kN", false, new Color32(250, 173, 46, 255));
            Agregar(r, el, m, "kN·m", viga, new Color32(237, 69, 64, 255));
            return r;
        }

        private static void Agregar(List<Serie> r, ARElemento el, string nombre, string unidad,
                                    bool invertir, Color32 color)
        {
            ARDiagrama d;
            if (!el.diagramas.TryGetValue(nombre, out d) || d == null || d.valores == null) return;
            double mx = 0.0;
            foreach (double a in d.valores) mx = Math.Max(mx, Math.Abs(a));
            r.Add(new Serie
            {
                nombre = nombre, unidad = unidad, x = el.x, v = d.valores, invertir = invertir,
                i = d.i, j = d.j, maxAbs = mx, color = color
            });
        }

        /// <summary>Texto de cada fila: «M_xz [kN·m]   i −114.3   j −159.6   máx 159.6».</summary>
        public static string Leyenda(Serie s)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} [{1}]   i {2:0.#}   j {3:0.#}   máx {4:0.#}{5}",
                s.nombre, s.unidad, s.i, s.j, s.maxAbs,
                s.invertir ? "   (M>0 hacia abajo)" : "");
        }

        /// <summary>
        /// Píxel de (x, v) dentro de un área [x0, y0, ancho, alto] (origen abajo-izquierda,
        /// como las texturas de Unity). x=0 → borde izquierdo; x=L → borde derecho.
        /// v=0 → centro; v=+vmax → arriba (o abajo si <paramref name="invertir"/>).
        /// </summary>
        public static void APixel(double x, double v, double L, double vmaxAbs,
                                  int x0, int y0, int ancho, int alto, bool invertir,
                                  out int px, out int py)
        {
            double fx = L > 1e-9 ? x / L : 0.0;
            fx = Math.Max(0.0, Math.Min(1.0, fx));
            px = x0 + (int)Math.Round(fx * (ancho - 1));
            double fy = vmaxAbs > 1e-9 ? v / vmaxAbs : 0.0;
            if (invertir) fy = -fy;
            int cy = y0 + alto / 2;
            py = cy + (int)Math.Round(fy * (alto / 2 - 2));
        }

        /// <summary>
        /// Dibuja las tres filas (N arriba, V al medio, M abajo) en <paramref name="destino"/>
        /// (se crea si es null o si cambia el tamaño). Devuelve la textura.
        /// </summary>
        public static Texture2D Dibujar(ARElemento el, Texture2D destino = null,
                                        int ancho = AnchoPorDefecto, int alto = AltoPorDefecto)
        {
            if (destino == null || destino.width != ancho || destino.height != alto)
            {
                destino = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
                destino.filterMode = FilterMode.Bilinear;
                destino.wrapMode = TextureWrapMode.Clamp;
            }
            var buf = new Color32[ancho * alto];
            var fondo = new Color32(12, 18, 28, 215);
            for (int k = 0; k < buf.Length; k++) buf[k] = fondo;

            var series = Series(el);
            double L = el != null && el.L > 0 ? el.L : 1.0;
            int filas = 3;
            int altoFila = alto / filas;
            const int margenX = 24, margenArriba = 34, margenAbajo = 8;  // arriba queda la leyenda

            for (int f = 0; f < series.Count && f < filas; f++)
            {
                Serie s = series[f];
                int y0 = alto - (f + 1) * altoFila + margenAbajo;
                int h = altoFila - margenArriba - margenAbajo;
                int x0 = margenX, w = ancho - 2 * margenX;

                // Eje cero y bordes del elemento (i, j).
                var gris = new Color32(150, 150, 160, 255);
                Linea(buf, ancho, alto, x0, y0 + h / 2, x0 + w - 1, y0 + h / 2, gris, 1);
                Linea(buf, ancho, alto, x0, y0, x0, y0 + h - 1, gris, 1);
                Linea(buf, ancho, alto, x0 + w - 1, y0, x0 + w - 1, y0 + h - 1, gris, 1);

                int n = Math.Min(s.x.Length, s.v.Length);
                int pxA = 0, pyA = 0;
                for (int k = 0; k < n; k++)
                {
                    int px, py;
                    APixel(s.x[k], s.v[k], L, s.maxAbs, x0, y0, w, h, s.invertir, out px, out py);
                    if (k > 0) Linea(buf, ancho, alto, pxA, pyA, px, py, s.color, 3);
                    pxA = px; pyA = py;
                }
            }

            destino.SetPixels32(buf);
            destino.Apply(false);
            return destino;
        }

        /// <summary>Línea gruesa por Bresenham sobre el buffer (con recorte).</summary>
        private static void Linea(Color32[] buf, int ancho, int alto,
                                  int x0, int y0, int x1, int y1, Color32 c, int grosor)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int r = grosor / 2;
            while (true)
            {
                for (int ox = -r; ox <= r; ox++)
                    for (int oy = -r; oy <= r; oy++)
                    {
                        int x = x0 + ox, y = y0 + oy;
                        if (x >= 0 && x < ancho && y >= 0 && y < alto) buf[y * ancho + x] = c;
                    }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
