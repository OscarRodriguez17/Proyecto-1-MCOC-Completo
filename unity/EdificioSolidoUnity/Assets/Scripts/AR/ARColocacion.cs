using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Colocación en el ancla (Semana 06)
    //
    //  Clase estática y SIN estado a propósito: la lógica que decide dónde
    //  queda cada extremo al colocar un elemento es verificable en el Editor,
    //  sin teléfono y sin sesión AR. El test de colocación la llama tal cual.
    //
    //  Reglas que implementa (fijas por la orden de la semana):
    //
    //   · Posición:    el ancla toma la POSICIÓN del impacto con el piso, pero
    //                  NO su rotación. La rotación del ancla es la calculada
    //                  aquí, y sólo en torno a la vertical.
    //   · Escala:      1:1 siempre (1 unidad = 1 m). Nada la modifica.
    //   · Orientación: f = frente de la cámara proyectado al plano horizontal,
    //                  r = Vector3.Cross(Vector3.up, f)  → r es la "derecha"
    //                  del espectador.
    //                  - Viga:  su eje local x (de i a j) se alinea con r, así
    //                    que el extremo i queda a la izquierda y el j a la
    //                    derecha (figura ar_ref_tag134.png).
    //                  - Columna o muro: el lado_positivo del momento principal
    //                    se alinea con −r. El plano del diagrama queda de frente
    //                    y la tracción de M > 0 apunta a la izquierda
    //                    (figura ar_ref_tag14.png).
    //
    //  Esta clase es el ÚNICO sitio donde se decide la orientación: la aplica
    //  la app SÓLO en el `localRotation` del CONTENEDOR del elemento, con la
    //  raíz de diagramas en `Quaternion.identity`. Poner el rumbo de la
    //  cámara además en la raíz aplicaba la orientación dos veces
    //  (Corrección 4, 1).
    // =====================================================================

    public static class ARColocacion
    {
        // =================================================================
        //  Plan B: colocar sin raycast (Corrección 3, C)
        //
        //  El raycast contra el plano horizontal falla cuando el teléfono no
        //  llega a detectar piso (sala a oscuras, moqueta, marble con el
        //  móvil pegado a la cara, luz rasante). Como la app tiene que poder
        //  coloque ALGO, se añade un plan B que NO depende de la
        //  detección: se supone que la cámara está a 1,40 m del piso —altura
        //  de una cámara de móvil sostenida a la altura de los ojos— y se
        //  ancla 1,40 m por debajo del teléfono.
        //
        //  Sobre ese ancla estimada, dos botones de 5 cm permiten cuadrar el
        //  piso a ojo. El ajuste NO se escribe en el ancla —ARCore reescribe
        //  la pose del ARAnchor y lo perdería— sino que se acumula como offset y
        //  se aplica a la raíz visual, que cuelga del ancla. Mover el piso NO
        //  cambia el tamaño del elemento (sigue 1:1) ni su orientación: sólo su
        //  altura sobre el piso.
        // =================================================================

        /// <summary>Altura supuesta de la cámara del móvil sobre el piso (m).</summary>
        public const float AlturaCamaraSobrePiso = 1.40f;

        /// <summary>Paso de los botones «Piso ±5 cm» (m).</summary>
        public const float PasoPiso = 0.05f;

        /// <summary>
        /// Punto del piso estimado bajo la cámara del teléfono: 1,40 m por
        /// debajo, conservando las coordenadas horizontales. No requiere
        /// tracking, plano ni ancla.
        /// </summary>
        public static Vector3 AnclaEstimadaBajoCamara(Vector3 posicionCamara)
        {
            return posicionCamara - Vector3.up * AlturaCamaraSobrePiso;
        }

        /// <summary>
        /// Desplaza el ancla un número entero de pasos de 5 cm en vertical.
        /// <paramref name="pasos"/> positivo sube el piso (+5 cm), negativo lo
        /// baja (−5 cm). Sólo cambia la altura: la escala del elemento sigue
        /// siendo 1:1.
        ///
        /// OJO: la app NO usa esto sobre `ancla.transform`. El `ARAnchor` de
        /// ARCore reescribe la pose de su GameObject, así que un ajuste hecho
        /// ahí se pierde; el desplazamiento se acumula con
        /// <see cref="AcumularPiso"/> y se aplica a la raíz visual. Esta función
        /// queda como la aritmética del desplazamiento, expresada sobre una
        /// posición, que es lo que hace verificable el paso de 5 cm.
        /// </summary>
        public static Vector3 DesplazarPiso(Vector3 posicionAncla, int pasos)
        {
            return posicionAncla + Vector3.up * (PasoPiso * pasos);
        }

        /// <summary>
        /// Offset de piso acumulado tras <paramref name="pasos"/> pasos de 5 cm.
        /// Es un simple suma sobre el offset anterior, y es lo que la app guarda:
        /// el desplazamiento es relativo al ancla, no absoluto (Corrección 4, 2).
        /// </summary>
        public static float AcumularPiso(float offsetActual, int pasos)
        {
            return offsetActual + PasoPiso * pasos;
        }

        /// <summary>Proyecta un vector al plano horizontal y lo normaliza.</summary>
        public static Vector3 Horizontal(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.zero;
        }

        /// <summary>
        /// "Derecha" del espectador: r = up × f con f el frente de la cámara
        /// projetado al piso. Es perpendicular a f y vive en el plano horizontal.
        /// </summary>
        public static Vector3 Derecha(Vector3 frenteCamara)
        {
            Vector3 f = Horizontal(frenteCamara);
            if (f.sqrMagnitude < 1e-8f) return Vector3.right;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            return r.sqrMagnitude > 1e-8f ? r.normalized : Vector3.right;
        }

        /// <summary>
        /// Giro en torno a la vertical (yaw) que lleva <paramref name="actual"/>
        /// hasta <paramref name="objetivo"/>. Ambos se proyectan a la horizontal:
        /// un elemento tumbado (una viga de entrepiso) no se puede orientar con
        /// la cámara si no se descarta la componente vertical.
        /// </summary>
        public static float Yaw(Vector3 actual, Vector3 objetivo)
        {
            Vector3 a = Horizontal(actual);
            Vector3 b = Horizontal(objetivo);
            if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) return 0f;
            float seno = Vector3.Cross(a, b).y;
            float cos = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
            return Mathf.Atan2(seno, cos) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Rotación inicial del elemento, la que se aplica en el momento de
        /// colocarlo y la que devuelve el botón «Restablecer».
        /// </summary>
        public static Quaternion RotacionInicial(ARGeometriaElemento g, Vector3 frenteCamara)
        {
            if (g == null) return Quaternion.identity;
            Vector3 r = Derecha(frenteCamara);
            // Viga: el eje del elemento va hacia la derecha del espectador.
            // Columna/muro: el lado de tracción de M > 0 va hacia la izquierda.
            Vector3 actual = g.esViga ? g.ejeX : g.ladoPrincipal;
            Vector3 objetivo = g.esViga ? r : -r;
            return Quaternion.Euler(0f, Yaw(actual, objetivo), 0f);
        }

        /// <summary>
        /// Posición mundial de un punto del elemento ya restado por el
        /// desplazamiento de anclaje: aquí sólo se rota y se traslada al ancla.
        /// Sin escala: 1 unidad = 1 m.
        /// </summary>
        public static Vector3 PuntoEnAncla(ARGeometriaElemento g, Quaternion rotacion,
                                            Vector3 posicionAncla, Vector3 puntoLocal)
        {
            return posicionAncla + rotacion * puntoLocal;
        }

        // ============ Colocación MARCANDO el elemento real (Corrección 5) ============
        // Columna: un punto al pie de la cara visible (BaseDesdeCara entra media sección).
        // Viga: dos puntos, bajo i y bajo j. Ancla en el punto medio, eje x del elemento
        // alineado con Pi→Pj. La escala NO se toca: la distancia sólo se compara con L.

        public const float ToleranciaLuz = 0.10f;
        public const float DistanciaMinimaPuntos = 0.50f;

        public struct ColocacionDosPuntos
        {
            public Vector3 posicion;     // punto medio horizontal, y = la menor
            public Quaternion rotacion;  // sólo en torno a la vertical
            public float distancia;      // distancia horizontal medida (m)
        }

        /// OJO: NO usar Horizontal() con PUNTOS: Horizontal normaliza y sólo sirve para direcciones.
        public static ColocacionDosPuntos PorDosPuntos(Vector3 Pi, Vector3 Pj, ARGeometriaElemento g)
        {
            float y = Mathf.Min(Pi.y, Pj.y);
            Vector3 a = new Vector3(Pi.x, y, Pi.z);
            Vector3 b = new Vector3(Pj.x, y, Pj.z);
            Vector3 ejeX = g != null ? g.ejeX : Vector3.right;
            var c = new ColocacionDosPuntos();
            c.posicion = (a + b) * 0.5f;
            c.rotacion = Quaternion.Euler(0f, Yaw(ejeX, b - a), 0f);
            c.distancia = DistanciaHorizontal(Pi, Pj);
            return c;
        }

        public static float DistanciaHorizontal(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static bool FueraDeTolerancia(float medida, float L, float tol = ToleranciaLuz)
        {
            if (L <= 1e-6f) return false;
            return Mathf.Abs(medida - L) / L > tol;
        }

        public static float DiferenciaPorcentual(float medida, float L)
        {
            if (L <= 1e-6f) return 0f;
            return (medida - L) / L * 100f;
        }

        /// "col_A_0.70x0.70" → 0,35 m (mitad de la primera medida). Acepta cm ("70x70"). Sin medidas → 0.
        public static float MitadSeccion(string seccion)
        {
            if (string.IsNullOrEmpty(seccion)) return 0f;
            var m = System.Text.RegularExpressions.Regex.Match(
                seccion, @"(\d+(?:[.,]\d+)?)\s*[xX]\s*(\d+(?:[.,]\d+)?)");
            if (!m.Success) return 0f;
            float b;
            if (!float.TryParse(m.Groups[1].Value.Replace(',', '.'),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out b))
                return 0f;
            if (b > 5f) b /= 100f;
            return Mathf.Clamp(b * 0.5f, 0f, 1.5f);
        }

        /// Centro de la base de una columna desde el punto tocado al pie de su cara visible:
        /// entra mitadSeccion metros en la dirección horizontal en que mira la cámara.
        public static Vector3 BaseDesdeCara(Vector3 puntoCara, Vector3 frenteCamara, float mitadSeccion)
        {
            Vector3 f = Horizontal(frenteCamara);
            if (f.sqrMagnitude < 1e-8f || mitadSeccion <= 0f) return puntoCara;
            return puntoCara + f * mitadSeccion;
        }
    }
}
