using System.Globalization;
using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Contador de frames de cámara (Corrección 3, C)
    //
    //  Diagnóstico que faltaba: con la cámara clavada en (0,0,0) no se
    //  distinguía «la sesión AR no mueve la pose» de «la app no dibuja».
    //  Este contador va por `Application.onBeforeRender`, que dispara una vez
    //  por frame PRESENTADO, así que separa las dos cosas de un vistazo:
    //  frames avanzando con pose en 0,0,0  =>  el rig no escribe la pose;
    //  frames en 0                          =>  la app ni siquiera dibuja.
    //
    //  Clase estática y con el callback público a propósito: se puede
    //  avanzar a mano desde un test de EditMode, sin sesión AR y sin
    //  esperar un frame real.
    // =====================================================================

    public static class ARFramesCamara
    {
        public static long Total { get; private set; }
        public static bool Activo { get; private set; }

        /// <summary>
        /// Empieza a contar. Es idempotente: llamarlo dos veces NO duplica la
        /// suscripción a <see cref="Application.onBeforeRender"/>, que es lo
        /// que Would romper el diagnóstico si `Awake` y `Start` se llamaran
        /// los dos.
        /// </summary>
        public static void Activar()
        {
            if (Activo) return;
            Application.onBeforeRender += Contar;
            Activo = true;
        }

        public static void Desactivar()
        {
            if (!Activo) return;
            Application.onBeforeRender -= Contar;
            Activo = false;
        }

        /// <summary>Vuelve el contador a cero sin tocar la suscripción.</summary>
        public static void Reiniciar()
        {
            Total = 0;
        }

        /// <summary>Callback de <see cref="Application.onBeforeRender"/>.</summary>
        public static void Contar()
        {
            Total++;
        }

        /// <summary>Etiqueta del contador, la que va en pantalla.</summary>
        public static string Texto()
        {
            return "Frames cámara: " + Total.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
