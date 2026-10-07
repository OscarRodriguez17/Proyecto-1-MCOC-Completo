using System.Collections.Generic;
using UnityEngine;

namespace MCOC.AR
{
    // =====================================================================
    //  Tipos de dibujo (Semana 06)
    // =====================================================================

    public enum ARTipoTrazo
    {
        Eje,        // eje del elemento
        Normal,     // N
        Cortante,   // V_xz / V_xy
        Momento,    // M_xz / M_xy
        PM,         // envolvente P-M de balanceo
        Demanda     // marca del punto de demanda (P, M)
    }

    /// <summary>Polilínea con estilo. Los puntos salen en el espacio local del ancla.</summary>
    public class ARTrazo
    {
        public List<Vector3> puntos = new List<Vector3>();
        public Color color = Color.white;
        public float ancho = 0.005f;
        public ARTipoTrazo tipo = ARTipoTrazo.Eje;
        public string etiqueta;
        /// <summary>
        /// True si pertenece al plano PRINCIPAL del elemento (o es N/eje/P-M). Lo
        /// fija el builder; la app lo usa para el toggle «No principal»
        /// (Corrección 6, parte 3c: antes se deducía del texto y salía al revés).
        /// </summary>
        public bool principal = true;
    }

    /// <summary>Marca (cruz 3D) en un punto notable: extremo o demanda.</summary>
    public class ARMarca
    {
        public Vector3 posicion;
        public float radio = 0.03f;
        public Color color = Color.white;
        public string texto;
        public Vector3 alturaTexto = Vector3.zero;
        /// <summary>A qué diagrama pertenece: el rótulo se oculta con su toggle.</summary>
        public ARTipoTrazo tipo = ARTipoTrazo.Eje;
        /// <summary>True si es del plano principal (ver ARTrazo.principal).</summary>
        public bool principal = true;

        // --- Cambio 01: rótulos "impresos" sobre el recuadro ---
        /// <summary>
        /// Si no es cero, el rótulo va FIJO sobre el plano cuya normal (hacia el que
        /// mira) es ésta: no gira con la cámara ni cambia de tamaño.
        /// </summary>
        public Vector3 normalPlano = Vector3.zero;
        /// <summary>Alto de la letra en metros (0 = el de siempre).</summary>
        public float alturaLetra = 0f;
        /// <summary>True: el texto se centra en su posición (si no, empieza ahí).</summary>
        public bool centrado = false;
    }

    /// <summary>Todo lo dibujable de un elemento.</summary>
    public class ARGeometriaElemento
    {
        public int tag;
        public string tipo;
        public string seccion;
        public string material;
        public string ubicacion;
        public string planoPrincipal;
        public string etiquetaI;
        public string etiquetaJ;
        public double longitud;
        public bool tienePm;

        // --- colocación (todo ya en coordenadas del ancla, 1 unidad = 1 m) ---
        /// <summary>Extremo i con el desplazamiento ya restado.</summary>
        public Vector3 puntoI;
        /// <summary>Extremo j con el desplazamiento ya restado.</summary>
        public Vector3 puntoJ;
        /// <summary>Eje local x del elemento (de i hacia j), tal como lo exporta el contrato.</summary>
        public Vector3 ejeX = Vector3.right;
        /// <summary>
        /// `lado_positivo` del momento principal, ya en coords Unity. Es la
        /// dirección hacia la que se dibuja M &gt; 0 (su lado de tracción).
        /// </summary>
        public Vector3 ladoPrincipal = Vector3.forward;

        public bool esViga
        {
            get { return tipo == "viga"; }
        }

        public List<ARTrazo> trazos = new List<ARTrazo>();
        public List<ARMarca> marcas = new List<ARMarca>();
    }

    /// <summary>
    /// Parámetros de presentación. `amplitud` es la altura visual que alcanza
    /// el mayor valor de CADA diagrama, de modo que las magnitudes relativas
    /// dentro de un diagrama se conservan (la escala es lineal y única).
    /// </summary>
    public class ARGeometriaOpciones
    {
        public float amplitud = 0.45f;
        public float carril = 0.28f;
        public float anchoPM = 0.70f;

        /// <summary>
        /// Lo sobrescribe `ARGeometriaBuilder.ConstruirElemento` con
        /// −anclaje.punto_unity en cada elemento: el desplazamiento NUNCA es un
        /// ajuste del usuario, sale del contrato, y por eso la escala se
        /// mantiene 1:1 (1 unidad = 1 m).
        /// </summary>
        public Vector3 desplazamiento = Vector3.zero;

        public Color colorEje = new Color(0.75f, 0.75f, 0.78f, 1f);
        public Color colorN = new Color(0.30f, 0.62f, 0.95f, 1f);
        public Color colorV = new Color(0.98f, 0.68f, 0.18f, 1f);
        public Color colorM = new Color(0.93f, 0.27f, 0.25f, 1f);
        public Color colorPM = new Color(0.62f, 0.35f, 0.92f, 1f);
        public Color colorDemanda = new Color(0.20f, 0.85f, 0.55f, 1f);
        public Color colorTexto = Color.white;

        public float anchoLinea = 0.025f;
        public float anchoLineaPrincipal = 0.04f;
    }
}
