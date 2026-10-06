using System.Collections.Generic;
using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Encuadre por 4 esquinas (Corrección 8, parte 5a)
    //
    //  En terreno, el anillo amarillo (un punto del piso) no bastaba: el
    //  diagrama quedaba a la altura y con la luz del MODELO (3,96 m, 10 m) y no
    //  calzaba con el elemento real. Ahora el usuario marca las 4 ESQUINAS de la
    //  cara del elemento que ve (en cualquier orden) y los diagramas se dibujan
    //  DENTRO de ese recuadro:
    //   · a lo largo del lado largo va el elemento de i a j (proporciones de x
    //     respetadas: x/L del modelo → misma fracción del recuadro);
    //   · columnas: recuadro VERTICAL (i abajo, j arriba);
    //   · vigas: recuadro HORIZONTAL (i a la izquierda del que mira, j a la
    //     derecha), en su costado o en su cara inferior.
    //
    //  Esta clase sólo tiene la geometría pura del recuadro (testeable sin
    //  teléfono). El dibujo está en ARGeometriaBuilder.ConstruirEnCuadro.
    // =====================================================================

    /// <summary>De dónde salió una esquina del recuadro.</summary>
    public enum OrigenCuadro
    {
        Ninguno,
        Profundidad,          // ARCore Depth: la superficie real bajo la mira
        Plano,                // plano detectado por ARCore (piso, cielo)
        PlanoDelRecuadro,     // plano de las esquinas ya marcadas
        PuntoCaracteristico,  // punto de ARCore
        Estimado              // piso / cielo estimados con las alturas del modelo
    }

    /// <summary>Recuadro marcado por el usuario sobre la cara del elemento real.</summary>
    public struct ARCuadroGeom
    {
        /// <summary>Centro del recuadro (mundo). Ahí va el ancla.</summary>
        public Vector3 centro;
        /// <summary>Dirección del ELEMENTO (de i a j): vertical en columnas, horizontal en vigas.</summary>
        public Vector3 u;
        /// <summary>Dirección transversal dentro de la cara (hacia donde se dibujan los valores).</summary>
        public Vector3 w;
        /// <summary>Lado del recuadro a lo largo del elemento (m).</summary>
        public float largo;
        /// <summary>Lado transversal del recuadro (m).</summary>
        public float ancho;
        /// <summary>True si la viga se marcó por su cara inferior (recuadro horizontal).</summary>
        public bool caraInferior;
        public bool valido;
        public string error;
    }

    public static class ARCuadro
    {
        /// <summary>Lado mínimo a lo largo del elemento (m).</summary>
        public const float LargoMinimo = 0.30f;

        /// <summary>Lado transversal mínimo (m): con menos no caben los diagramas.</summary>
        public const float AnchoMinimo = 0.08f;

        /// <summary>Cuántas esquinas se marcan.</summary>
        public const int Esquinas = 4;

        /// <summary>
        /// Recuadro a partir de las 4 esquinas marcadas, en CUALQUIER orden.
        /// <paramref name="derechaCamara"/> y <paramref name="frenteCamara"/>
        /// fijan los sentidos: i a la izquierda en vigas, y en columnas el lado
        /// positivo del momento hacia la izquierda del que mira (como antes).
        /// </summary>
        public static ARCuadroGeom Desde4Puntos(IList<Vector3> p, bool esViga,
                                                Vector3 derechaCamara, Vector3 frenteCamara)
        {
            var q = new ARCuadroGeom();
            if (p == null || p.Count < Esquinas)
            {
                q.error = "Faltan esquinas.";
                return q;
            }

            Vector3 c = Vector3.zero;
            for (int k = 0; k < p.Count; k++) c += p[k];
            c /= p.Count;

            Vector3 derecha = ARColocacion.Horizontal(derechaCamara);
            if (derecha.sqrMagnitude < 1e-8f) derecha = Vector3.right;
            Vector3 frente = ARColocacion.Horizontal(frenteCamara);
            if (frente.sqrMagnitude < 1e-8f) frente = Vector3.Cross(derecha, Vector3.up);

            // Dirección horizontal dominante de las esquinas (PCA 2D en planta).
            Vector3 h = DireccionHorizontal(p, c, derecha);

            if (esViga)
            {
                q.u = h;
                if (Vector3.Dot(q.u, derecha) < 0f) q.u = -q.u;          // i a la izquierda
                Vector3 perp = Vector3.Cross(Vector3.up, q.u).normalized; // horizontal ⟂ a la viga
                float sv = 0f, sp = 0f;
                for (int k = 0; k < p.Count; k++)
                {
                    Vector3 d = p[k] - c;
                    sv += d.y * d.y;
                    float t = Vector3.Dot(d, perp);
                    sp += t * t;
                }
                if (sv >= sp)
                {
                    q.w = Vector3.up;                 // costado de la viga: recuadro vertical
                }
                else
                {
                    q.caraInferior = true;            // cara inferior: recuadro horizontal
                    q.w = Vector3.Dot(perp, frente) >= 0f ? perp : -perp;  // "arriba" = lado lejano
                }
            }
            else
            {
                q.u = Vector3.up;                     // columna: i abajo, j arriba
                q.w = Vector3.Dot(h, derecha) > 0f ? -h : h;   // hacia la izquierda del que mira
            }

            float umin = float.MaxValue, umax = float.MinValue;
            float wmin = float.MaxValue, wmax = float.MinValue;
            for (int k = 0; k < p.Count; k++)
            {
                Vector3 d = p[k] - c;
                float a = Vector3.Dot(d, q.u), b = Vector3.Dot(d, q.w);
                umin = Mathf.Min(umin, a); umax = Mathf.Max(umax, a);
                wmin = Mathf.Min(wmin, b); wmax = Mathf.Max(wmax, b);
            }
            q.largo = umax - umin;
            q.ancho = wmax - wmin;
            q.centro = c + q.u * (0.5f * (umin + umax)) + q.w * (0.5f * (wmin + wmax));

            if (q.largo < LargoMinimo)
            {
                q.error = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "El recuadro mide {0:0.00} m a lo largo del elemento: marca las esquinas en sus extremos.",
                    q.largo);
                return q;
            }
            if (q.ancho < AnchoMinimo)
            {
                q.error = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "El recuadro mide {0:0.00} m de ancho: marca las dos aristas de la cara.", q.ancho);
                return q;
            }
            q.valido = true;
            return q;
        }

        /// <summary>
        /// Dirección horizontal en que más se reparten los puntos (análisis de
        /// componentes principales en planta). Si están todos sobre una vertical,
        /// devuelve <paramref name="respaldo"/>.
        /// </summary>
        public static Vector3 DireccionHorizontal(IList<Vector3> p, Vector3 c, Vector3 respaldo)
        {
            float sxx = 0f, szz = 0f, sxz = 0f;
            for (int k = 0; k < p.Count; k++)
            {
                float dx = p[k].x - c.x, dz = p[k].z - c.z;
                sxx += dx * dx; szz += dz * dz; sxz += dx * dz;
            }
            if (sxx + szz < 1e-6f) return respaldo;
            float th = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
            return new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
        }

        /// <summary>
        /// Plano provisional con las esquinas ya marcadas, para estimar la
        /// siguiente cuando ARCore no mide nada bajo la mira: con 3 o más, el
        /// plano que pasa por las tres primeras; con 2, el plano VERTICAL que las
        /// contiene (cara de una columna o costado de una viga).
        /// </summary>
        public static bool PlanoProvisional(IList<Vector3> p, Vector3 frenteCamara,
                                            out Vector3 punto, out Vector3 normal)
        {
            punto = Vector3.zero;
            normal = Vector3.zero;
            if (p == null || p.Count < 2) return false;
            punto = p[0];
            if (p.Count >= 3)
            {
                Vector3 n3 = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                if (n3.sqrMagnitude > 1e-4f)
                {
                    normal = n3.normalized;
                    return true;
                }
            }
            Vector3 d = p[1] - p[0];
            Vector3 n = Vector3.Cross(d, Vector3.up);
            if (n.sqrMagnitude < 1e-4f)
            {
                // Las dos esquinas sobre una vertical: la cara mira hacia la cámara.
                n = ARColocacion.Horizontal(frenteCamara);
                if (n.sqrMagnitude < 1e-8f) return false;
            }
            normal = n.normalized;
            return true;
        }

        /// <summary>Amarillo (fiable) o naranjo (revisar), como el anillo del piso.</summary>
        public static bool EsSeguro(OrigenCuadro o)
        {
            return o == OrigenCuadro.Profundidad || o == OrigenCuadro.Plano ||
                   o == OrigenCuadro.PlanoDelRecuadro;
        }

        /// <summary>Texto corto para mostrar al usuario de dónde salió la esquina.</summary>
        public static string Describir(OrigenCuadro o)
        {
            switch (o)
            {
                case OrigenCuadro.Profundidad: return "profundidad medida";
                case OrigenCuadro.Plano: return "plano detectado";
                case OrigenCuadro.PlanoDelRecuadro: return "en el plano de las esquinas";
                case OrigenCuadro.PuntoCaracteristico: return "punto de referencia";
                case OrigenCuadro.Estimado: return "ESTIMADO (sin medir)";
                default: return "sin punto";
            }
        }

        // -----------------------------------------------------------------
        //  Corrección 9 (6a): sólo las 2 esquinas de ABAJO se miden. Las 2 de
        //  arriba se toman sobre el plano VERTICAL que pasa por las de abajo,
        //  así una esquina mal medida ya no puede irse metros al fondo.
        // -----------------------------------------------------------------

        /// <summary>Distancia entre las 2 esquinas de abajo: columna (cara) y viga (luz entre columnas).</summary>
        public const float BaseMinColumna = 0.15f, BaseMaxColumna = 2.5f;
        public const float BaseMinViga = 1.0f, BaseMaxViga = 25f;
        /// <summary>Medidas aceptables del recuadro terminado (m).</summary>
        public const float AltoMinColumna = 1.0f, AltoMaxColumna = 8f;
        public const float AnchoMaxCara = 2.5f, AnchoMinCara = 0.15f;

        /// <summary>
        /// Plano VERTICAL que contiene las dos esquinas de abajo. Si están una
        /// sobre otra (no debería), el plano mira hacia la cámara.
        /// </summary>
        public static bool PlanoVertical(Vector3 a, Vector3 b, Vector3 frenteCamara,
                                         out Vector3 punto, out Vector3 normal)
        {
            punto = a;
            Vector3 n = Vector3.Cross(b - a, Vector3.up);
            n.y = 0f;
            if (n.sqrMagnitude < 1e-6f)
            {
                n = ARColocacion.Horizontal(frenteCamara);
                if (n.sqrMagnitude < 1e-8f) { normal = Vector3.zero; return false; }
            }
            normal = n.normalized;
            return true;
        }

        /// <summary>Punto más cercano a <paramref name="p"/> sobre el plano (punto, normal).</summary>
        public static Vector3 ProyectarEnPlano(Vector3 p, Vector3 puntoPlano, Vector3 normal)
        {
            return p - normal * Vector3.Dot(p - puntoPlano, normal);
        }

        /// <summary>Error para el usuario si las 2 esquinas de abajo no tienen sentido; null si están bien.</summary>
        public static string ValidarBase(Vector3 a, Vector3 b, bool esViga)
        {
            float d = ARColocacion.DistanciaHorizontal(a, b);
            float min = esViga ? BaseMinViga : BaseMinColumna;
            float max = esViga ? BaseMaxViga : BaseMaxColumna;
            if (d >= min && d <= max) return null;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Las 2 esquinas de abajo quedaron a {0:0.00} m (esperado {1:0.##}–{2:0.##} m). " +
                "Marca de nuevo la segunda.", d, min, max);
        }

        /// <summary>Error para el usuario si el recuadro terminado es absurdo; null si está bien.</summary>
        public static string ValidarRecuadro(ARCuadroGeom q, bool esViga)
        {
            if (!q.valido) return q.error;
            if (!esViga && (q.largo < AltoMinColumna || q.largo > AltoMaxColumna))
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "La columna quedó de {0:0.00} m de alto. Marca de nuevo las 2 esquinas de arriba.", q.largo);
            if (q.ancho < AnchoMinCara || q.ancho > AnchoMaxCara)
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "El recuadro quedó de {0:0.00} m de ancho. Marca de nuevo las 2 esquinas de arriba.", q.ancho);
            return null;
        }

        /// <summary>Intersección de un rayo con un plano cualquiera (a menos de 25 m, hacia adelante).</summary>
        public static bool InterseccionPlano(Vector3 origen, Vector3 direccion,
                                             Vector3 puntoPlano, Vector3 normal, out Vector3 punto)
        {
            punto = Vector3.zero;
            Vector3 d = direccion.normalized;
            float den = Vector3.Dot(d, normal);
            if (Mathf.Abs(den) < 1e-3f) return false;
            float t = Vector3.Dot(puntoPlano - origen, normal) / den;
            if (t <= 0f || t > ARPiso.DistanciaMaxima) return false;
            punto = origen + d * t;
            return true;
        }
    }
}
