using System;
using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Apoyos de una viga (Corrección 6, parte 3b)
    //
    //  En terreno, marcar "el piso bajo el extremo de la viga" es impreciso:
    //  ese punto no tiene nada que lo identifique y la viga quedaba mal
    //  ubicada. En cambio, las COLUMNAS que la sostienen se ven claramente.
    //  La viga 134 va de la columna 14 (Eje F) a la 26 (Eje G): se marca el pie
    //  de cada columna y, como con «Marcar base», se entra media sección hacia
    //  su centro, que es donde llega el eje de la viga.
    //
    //  Esta clase busca en el contrato la columna bajo cada extremo de la viga
    //  (matemática pura, testeable sin teléfono).
    // =====================================================================

    public static class ARApoyos
    {
        /// <summary>Media sección si no se encuentra la columna de apoyo (columnas de 0,70 m).</summary>
        public const float MitadColumnaPorDefecto = 0.35f;

        /// <summary>Distancia horizontal máxima entre el extremo de la viga y el eje de la columna (m).</summary>
        public const float ToleranciaPlanta = 0.50f;

        /// <summary>Diferencia de altura máxima entre el extremo de la viga y la cabeza de la columna (m).</summary>
        public const float ToleranciaAltura = 0.60f;

        /// <summary>
        /// Columna del contrato cuya CABEZA coincide con el extremo i (o j) de la
        /// viga: misma posición en planta y cabeza a la altura del extremo. Null
        /// si no hay ninguna (el contrato sólo trae algunos elementos).
        /// </summary>
        public static ARElemento ColumnaBajoExtremo(ARRaiz datos, ARElemento viga, bool extremoI)
        {
            if (datos == null || datos.elementos == null || viga == null || viga.extremos == null)
                return null;
            ARExtremo e = extremoI ? viga.extremos.i : viga.extremos.j;
            if (e == null || e.xyzUnity == null || e.xyzUnity.Length < 3) return null;
            double ex = e.xyzUnity[0], ey = e.xyzUnity[1], ez = e.xyzUnity[2];

            ARElemento mejor = null;
            double mejorD = double.MaxValue;
            foreach (var kv in datos.elementos)
            {
                ARElemento c = kv.Value;
                if (c == null || c == viga || c.extremos == null) continue;
                if (c.tipo != "columna" && c.tipo != "muro") continue;
                ARExtremo cabeza = Cabeza(c);
                if (cabeza == null) continue;
                double dx = cabeza.xyzUnity[0] - ex, dz = cabeza.xyzUnity[2] - ez;
                double dPlanta = Math.Sqrt(dx * dx + dz * dz);
                double dAltura = Math.Abs(cabeza.xyzUnity[1] - ey);
                if (dPlanta > ToleranciaPlanta || dAltura > ToleranciaAltura) continue;
                if (dPlanta < mejorD)
                {
                    mejorD = dPlanta;
                    mejor = c;
                }
            }
            return mejor;
        }

        /// <summary>Extremo superior (cabeza) de una columna: el de mayor altura.</summary>
        private static ARExtremo Cabeza(ARElemento c)
        {
            ARExtremo a = c.extremos.i, b = c.extremos.j;
            bool okA = a != null && a.xyzUnity != null && a.xyzUnity.Length >= 3;
            bool okB = b != null && b.xyzUnity != null && b.xyzUnity.Length >= 3;
            if (okA && okB) return a.xyzUnity[1] >= b.xyzUnity[1] ? a : b;
            return okA ? a : (okB ? b : null);
        }

        /// <summary>Media sección de la columna de apoyo (para entrar desde la cara al eje).</summary>
        public static float MitadApoyo(ARElemento columna)
        {
            if (columna == null) return MitadColumnaPorDefecto;
            float m = ARColocacion.MitadSeccion(columna.seccion);
            return m > 0f ? m : MitadColumnaPorDefecto;
        }

        /// <summary>"la columna 14" o, si no se conoce, "la columna de apoyo".</summary>
        public static string NombreApoyo(ARElemento columna)
        {
            return columna != null ? "la columna " + columna.tag : "la columna de apoyo";
        }
    }
}
