using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Marcar la viga APUNTANDO A ELLA (Corrección 7, parte 4b)
    //
    //  En terreno, para marcar la viga había que bajar el teléfono al piso
    //  (el pie de sus columnas), y con autos o gente al medio no se podía.
    //  Ahora se apunta la mira a la CARA INFERIOR de la viga, junto a cada
    //  columna de apoyo, y se toca.
    //
    //  ¿De dónde sale el punto del techo? Cadena de respaldos, igual que en
    //  ARPiso, del más fiable al menos:
    //   1) Profundidad de ARCore (Depth API): distancia real a lo que está
    //      bajo la mira, en cualquier superficie.
    //   2) Plano horizontal detectado ARRIBA del teléfono (cielo / viga).
    //   3) Punto característico de ARCore arriba del teléfono.
    //   4) Estimado: el rayo de la mira cortado con un plano horizontal a la
    //      altura que tiene la cara inferior de la viga en el MODELO sobre el
    //      piso (altura del nodo − peralte: 3,96 − 0,80 = 3,16 m).
    //
    //  Del punto de la cara inferior al EJE de la viga:
    //   · en planta, se marca junto a la cara de la columna; el eje de la
    //     columna (donde llega el nodo de la viga) está media sección más
    //     allá, hacia afuera de la viga (EjesDesdeCaras);
    //   · en altura, el ancla va al PISO: el conocido, o el que se deduce de
    //     la cara inferior medida (PisoDesdeInferior).
    //
    //  Esta clase guarda la matemática pura (testeable sin teléfono).
    // =====================================================================

    /// <summary>De dónde salió el punto de la cara inferior de la viga.</summary>
    public enum OrigenTecho
    {
        Ninguno,
        Profundidad,         // ARCore Depth: la superficie real bajo la mira
        Plano,               // plano horizontal detectado arriba del teléfono
        PuntoCaracteristico, // punto de ARCore arriba del teléfono
        Estimado             // rayo ∩ plano a la altura de la cara inferior del modelo
    }

    public static class ARTecho
    {
        /// <summary>Un punto del techo tiene que estar al menos esto sobre el teléfono (m).</summary>
        public const float MargenSobreCamara = 0.30f;

        /// <summary>Peralte supuesto si el nombre de la sección no lo trae (m).</summary>
        public const float PeraltePorDefecto = 0.80f;

        /// <summary>True si un punto a altura <paramref name="yPunto"/> puede ser la cara inferior de una viga.</summary>
        public static bool EsTecho(float yPunto, float yCamara)
        {
            return yPunto > yCamara + MargenSobreCamara;
        }

        /// <summary>
        /// Peralte (alto) de la viga, la SEGUNDA medida del nombre de la sección
        /// ("viga0.60x0.80" → 0,80 m). Si viene en cm (60x80) se pasa a m.
        /// </summary>
        public static float Peralte(string seccion)
        {
            if (string.IsNullOrEmpty(seccion)) return PeraltePorDefecto;
            var m = System.Text.RegularExpressions.Regex.Match(
                seccion, @"(\d+(?:[.,]\d+)?)\s*[xX]\s*(\d+(?:[.,]\d+)?)");
            if (!m.Success) return PeraltePorDefecto;
            float h;
            if (!float.TryParse(m.Groups[2].Value.Replace(',', '.'),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out h))
                return PeraltePorDefecto;
            if (h > 5f) h /= 100f;
            return Mathf.Clamp(h, 0.05f, 3f);
        }

        /// <summary>
        /// Altura de la cara inferior sobre el piso: la del nodo de la viga (nivel
        /// de losa) menos su peralte. Nunca negativa.
        /// </summary>
        public static float AlturaInferior(float alturaNodo, float peralte)
        {
            return Mathf.Max(0f, alturaNodo - peralte);
        }

        /// <summary>Altura de la cara inferior de la viga <paramref name="g"/> sobre su piso (m).</summary>
        public static float AlturaInferiorViga(ARGeometriaElemento g)
        {
            if (g == null) return 0f;
            float nodo = Mathf.Min(g.puntoI.y, g.puntoJ.y);
            return AlturaInferior(nodo, Peralte(g.seccion));
        }

        /// <summary>Piso que corresponde a una cara inferior medida a altura <paramref name="yInferior"/>.</summary>
        public static float PisoDesdeInferior(float yInferior, float alturaInferior)
        {
            return yInferior - alturaInferior;
        }

        /// <summary>
        /// Ejes de las columnas de apoyo a partir de los puntos marcados junto a
        /// sus caras: cada uno se corre <paramref name="mitadI"/> (o
        /// <paramref name="mitadJ"/>) metros hacia AFUERA de la viga, en la
        /// dirección horizontal i→j. La altura no cambia.
        /// </summary>
        public static void EjesDesdeCaras(Vector3 caraI, Vector3 caraJ, float mitadI, float mitadJ,
                                          out Vector3 ejeI, out Vector3 ejeJ)
        {
            Vector3 d = ARColocacion.Horizontal(caraJ - caraI);
            ejeI = caraI - d * Mathf.Max(0f, mitadI);
            ejeJ = caraJ + d * Mathf.Max(0f, mitadJ);
        }

        /// <summary>True si el punto se MIDIÓ en la superficie real (no es estimado).</summary>
        public static bool EsMedido(OrigenTecho o)
        {
            return o == OrigenTecho.Profundidad || o == OrigenTecho.Plano ||
                   o == OrigenTecho.PuntoCaracteristico;
        }

        /// <summary>Amarillo (fiable) o naranjo (revisar), como el anillo del piso.</summary>
        public static bool EsSeguro(OrigenTecho o)
        {
            return o == OrigenTecho.Profundidad || o == OrigenTecho.Plano;
        }

        /// <summary>Texto corto para mostrar al usuario de dónde salió el punto.</summary>
        public static string Describir(OrigenTecho o)
        {
            switch (o)
            {
                case OrigenTecho.Profundidad: return "profundidad medida";
                case OrigenTecho.Plano: return "cielo detectado";
                case OrigenTecho.PuntoCaracteristico: return "punto de referencia";
                case OrigenTecho.Estimado: return "altura ESTIMADA del modelo";
                default: return "sin punto";
            }
        }
    }
}
