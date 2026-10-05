using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Punto del piso para marcar (Corrección 6, parte 3a)
    //
    //  En terreno el anillo amarillo "se perdía": sólo aparecía cuando ARCore
    //  detectaba un plano justo en el centro de la pantalla, y en hormigón liso
    //  o apuntando lejos eso falla a menudo. Ahora el punto sale de una cadena
    //  de respaldos, de más a menos confiable:
    //
    //   1) Plano detectado por ARCore (PlaneWithinPolygon), SÓLO si está bajo
    //      el teléfono: ARCore también detecta cielos y mesas altas.
    //   2) El último piso conocido, extendido como plano horizontal infinito:
    //      el rayo de la cámara se intersecta con y = altura de ese piso.
    //   3) Puntos característicos de ARCore cercanos a la altura del piso.
    //   4) Piso estimado: 1,40 m bajo el teléfono (o el último piso conocido).
    //
    //  Esta clase guarda la matemática pura (testeable sin teléfono).
    // =====================================================================

    /// <summary>De dónde salió el punto del piso que se va a marcar.</summary>
    public enum OrigenPunto
    {
        Ninguno,
        PisoDetectado,      // plano de ARCore bajo el teléfono
        PisoExtendido,      // último piso conocido, prolongado como plano infinito
        PuntoCaracteristico,// punto de ARCore cercano a la altura del piso
        PisoEstimado        // 1,40 m bajo el teléfono
    }

    public static class ARPiso
    {
        /// <summary>Altura supuesta del teléfono sobre el piso cuando no se conoce el piso (m).</summary>
        public const float AlturaCamaraPorDefecto = 1.40f;

        /// <summary>Un piso tiene que estar al menos esto bajo el teléfono (descarta cielos, mesas).</summary>
        public const float MargenBajoCamara = 0.50f;

        /// <summary>Tolerancia de altura para aceptar un punto característico como piso (m).</summary>
        public const float ToleranciaAlturaPiso = 0.30f;

        /// <summary>No se marca más lejos que esto (m): con el rayo casi horizontal el error crece mucho.</summary>
        public const float DistanciaMaxima = 25f;

        /// <summary>True si un punto a altura <paramref name="yPunto"/> puede ser piso visto desde la cámara.</summary>
        public static bool EsPiso(float yPunto, float yCamara)
        {
            return yPunto < yCamara - MargenBajoCamara;
        }

        /// <summary>
        /// True si un punto característico está a la altura del piso conocido
        /// (o, sin piso conocido, al menos bajo el teléfono).
        /// </summary>
        public static bool CercaDelPiso(float yPunto, float yCamara, bool hayPisoConocido, float yPiso)
        {
            if (!EsPiso(yPunto, yCamara)) return false;
            return !hayPisoConocido || Mathf.Abs(yPunto - yPiso) <= ToleranciaAlturaPiso;
        }

        /// <summary>Altura del piso a usar cuando no hay nada detectado en el centro de la pantalla.</summary>
        public static float AlturaPisoEstimada(float yCamara, bool hayPisoConocido, float yPisoConocido)
        {
            return hayPisoConocido ? yPisoConocido : yCamara - AlturaCamaraPorDefecto;
        }

        /// <summary>
        /// Intersección de un rayo con el plano horizontal y = <paramref name="yPlano"/>.
        /// Falla si el rayo no baja hacia el plano o si el punto queda a más de
        /// <see cref="DistanciaMaxima"/> m del origen.
        /// </summary>
        public static bool InterseccionPlanoHorizontal(Vector3 origen, Vector3 direccion,
                                                       float yPlano, out Vector3 punto)
        {
            punto = Vector3.zero;
            Vector3 d = direccion.normalized;
            float dy = d.y;
            float alto = yPlano - origen.y;           // < 0 si el plano está bajo el origen
            if (Mathf.Abs(dy) < 1e-4f) return false;   // rayo horizontal
            float t = alto / dy;
            if (t <= 0f) return false;                 // el plano queda detrás / el rayo sube
            if (t > DistanciaMaxima) return false;     // demasiado lejos: poco preciso
            punto = origen + d * t;
            punto.y = yPlano;
            return true;
        }

        /// <summary>Texto corto para mostrar al usuario de dónde salió el punto.</summary>
        public static string Describir(OrigenPunto o)
        {
            switch (o)
            {
                case OrigenPunto.PisoDetectado: return "piso detectado";
                case OrigenPunto.PisoExtendido: return "piso detectado (extendido)";
                case OrigenPunto.PuntoCaracteristico: return "punto de referencia";
                case OrigenPunto.PisoEstimado: return "piso ESTIMADO (1,40 m bajo el teléfono)";
                default: return "sin piso";
            }
        }
    }
}
