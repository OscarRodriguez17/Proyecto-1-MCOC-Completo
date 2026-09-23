using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCOC.Unity
{
    public class ModeloComplejo
    {
        public string proyecto;
        public string unidades;
        public string nota;

        [JsonProperty("edificios")]
        public List<ModeloEdificio> edificios = new List<ModeloEdificio>();

        public TotalesModelo totales;
    }

    public class ModeloEdificio
    {
        public string proyecto;
        public string bloque;
        public string unidades;
        public OffsetModelo offset;
        public GeometriaModelo geometria;

        [JsonProperty("nodos")]
        public Dictionary<string, NodoModelo> nodos = new Dictionary<string, NodoModelo>();

        public List<ElementoModelo> elementos = new List<ElementoModelo>();

        [JsonProperty("cargas")]
        public CargasModelo cargas;

        [JsonProperty("apoyos")]
        public List<ApoyoModelo> apoyos = new List<ApoyoModelo>();

        [JsonProperty("resultados")]
        public Dictionary<string, ResultadoCaso> resultados = new Dictionary<string, ResultadoCaso>();

        /// <summary>
        /// Coeficientes de esfuerzos por viga y caso (pasada dedicada de
        /// diagramas del Edificio B): esfuerzos[caso][tag_de_viga] = {L, N, Vy,
        /// Vz, T, My, Mz, Wy, Wz}. Permiten evaluar N(x), V(x) y M(x) en
        /// cualquier x ∈ [0, L] con las fórmulas del enunciado.
        /// </summary>
        [JsonProperty("esfuerzos")]
        public Dictionary<string, Dictionary<string, EsfuerzosVigaModelo>> esfuerzos =
            new Dictionary<string, Dictionary<string, EsfuerzosVigaModelo>>();

        /// <summary>
        /// Bloque ADITIVO (Semana 04): fuerzas completas por elementTag de
        /// TODOS los elementos estructurales (columnas, muros, vigas y aspas)
        /// y por caso G/Q/GQ/EX/EY. Mismo esquema del bloque `esfuerzos` de la
        /// semana 03: {L, N, Vy, Vz, T, My, Mz, Wy, Wz} con la misma convencion
        /// (N > 0 = compresion en el JSON; N(x) = -N, momentos con reparticion
        /// cuadratica; Wz = -w solo en vigas). Lo escriben el cache
        /// `secciones_semana04.json` y el overlay `secciones/exportar_unity.py`.
        /// </summary>
        [JsonProperty("esfuerzos_completos")]
        public Dictionary<string, Dictionary<string, EsfuerzosVigaModelo>> esfuerzosCompletos =
            new Dictionary<string, Dictionary<string, EsfuerzosVigaModelo>>();

        /// <summary>
        /// Metadatos por elementTag (Semana 04): tipo de visor, seccion de
        /// catalogo, material, longitud, restriccion de cada extremo y ejes
        /// locales (SI) tal como los calculo el postprocesador Python.
        /// </summary>
        [JsonProperty("metadatos")]
        public Dictionary<string, MetadatoElemento> metadatos =
            new Dictionary<string, MetadatoElemento>();

        /// <summary>
        /// Bloque ADITIVO (Semana 03, motor de secciones): catalogo P–M / M–φ,
        /// demandas (P, M) por elemento y caso, y verificacion de superposicion.
        /// Lo traen el Edificio B y el Edificio A (overlay `secciones/exportar_unity.py`).
        /// </summary>
        [JsonProperty("secciones")]
        public SeccionesInteraccion secciones;
    }

    public class OffsetModelo
    {
        public double x;
        public double y;
    }

    public class GeometriaModelo
    {
        [JsonProperty("grid_x")]
        public Dictionary<string, double> grid_x = new Dictionary<string, double>();

        [JsonProperty("grid_y")]
        public Dictionary<string, double> grid_y = new Dictionary<string, double>();

        [JsonProperty("niveles_z")]
        public List<double> niveles_z = new List<double>();

        public SeccionesModelo secciones;
    }

    public class SeccionesModelo
    {
        public string pilar;
        public string viga;
        public List<MuroModelo> muros = new List<MuroModelo>();
    }

    public class MuroModelo
    {
        public string id;
        public string axis;
        public string y0;
        public string y1;
        public double t;
        public string y;
        public object x0;
        public object x1;
    }

    public class NodoModelo
    {
        public double x;
        public double y;
        public double z;
        public int nivel;
        public string rol;
    }

    public class ElementoModelo
    {
        public int tag;
        public string tipo;
        public int ni;
        public int nj;

        /// <summary>Etiqueta de seccion RC (p.ej. "col0.70x0.70", "muro0.60x2.91").
        /// La añade el overlay aditivo `secciones/exportar_unity.py`.</summary>
        public string seccion;
    }

    public class ApoyoModelo
    {
        public int tag;
        public string tipo;
        public List<int> constraint = new List<int>();
    }

    public class CargasModelo
    {
        public List<CargaVigaModelo> vigas = new List<CargaVigaModelo>();

        [JsonProperty("q_losa")]
        public CargasLosaModelo q_losa;

        [JsonProperty("peso_columnas_muros")]
        public double peso_columnas_muros;

        [JsonProperty("pesos_por_nivel")]
        public Dictionary<string, double> pesos_por_nivel = new Dictionary<string, double>();

        public Dictionary<string, CargaSismoModelo> sismo = new Dictionary<string, CargaSismoModelo>();
    }

    public class CargasLosaModelo
    {
        public double G;
        public double Q;
    }

    public class CargaVigaModelo
    {
        public int tag;
        public string tipo;
        public int nivel;
        public int ni;
        public int nj;
        public double L;
        public double qG;
        public double qQ;
        public double G;
        public double Q;
    }

    public class CargaSismoModelo
    {
        public double V;

        [JsonProperty("F_por_nivel")]
        public Dictionary<string, double> F_por_nivel = new Dictionary<string, double>();
    }

    public class ResultadoCaso
    {
        public FuerzasModelo aplicada;
        public FuerzasModelo reacciones_totales;

        [JsonProperty("desplazamientos_maestro")]
        public Dictionary<string, DesplazamientoNivel> desplazamientos_maestro = new Dictionary<string, DesplazamientoNivel>();
    }

    public class FuerzasModelo
    {
        public double fx;
        public double fy;
        public double fz;
    }

    public class DesplazamientoNivel
    {
        public double ux;
        public double uy;
        public double rz;
        public double z;
    }

    public class TotalesModelo
    {
        public TotalPorCaso G;
        public TotalPorCaso EX;
        public TotalPorCaso EY;
    }

    public class TotalPorCaso
    {
        public double fz_aplicada;
        public double fz_reaccion;
        public double fx_aplicada;
        public double fx_reaccion;
        public double fy_aplicada;
        public double fy_reaccion;
    }

    /// <summary>
    /// Coeficientes mínimos por viga y caso (JSON: bloque esfuerzos) para
    /// evaluar los diagramas en cualquier x∈[0,L]:
    ///   N(x)=-N ; Vz(x)=Vz+Wz·x ; My(x)=My+Vz·x+Wz·x²/2 ; Vy/Mz análogos.
    /// </summary>
    public class EsfuerzosVigaModelo
    {
        public double L;
        public double N;
        public double Vy;
        public double Vz;
        public double T;
        public double My;
        public double Mz;
        public double Wy;
        public double Wz;
    }

    // ------------------------------------------------------------------
    // Bloque ADITIVO de secciones (Semana 03). Convencion: P > 0 = compresion
    // [kN]; M = sqrt(My^2 + Mz^2) [kN*m].
    // ------------------------------------------------------------------
    public class SeccionesInteraccion
    {
        public string convencion;
        public string unidades;

        [JsonProperty("secciones")]
        public Dictionary<string, SeccionInteraccion> catalogo =
            new Dictionary<string, SeccionInteraccion>();

        public Dictionary<string, ElementoInteraccion> elementos =
            new Dictionary<string, ElementoInteraccion>();

        public SuperposicionInteraccion superposicion;
    }

    public class SeccionInteraccion
    {
        [JsonProperty("P0")]
        public double P0;

        [JsonProperty("P0_fibra")]
        public double P0_fibra;

        [JsonProperty("As")]
        public double As;

        public PuntoPM balanceado;
        public EnvolventePM envelope;
    }

    public class PuntoPM
    {
        public double P;
        public double M;
    }

    public class EnvolventePM
    {
        public List<double> P = new List<double>();
        public List<double> M = new List<double>();
        public List<double> EI0 = new List<double>();
        public List<string> MOTIVO = new List<string>();
    }

    /// <summary>
    /// Metadatos de un elemento estructural (Semana 04): lo que necesita el
    /// visor para rotular y orientar diagramas 3D sin tener que reinferir el
    /// modelo (restricciones y ejes locales ya vienen calculados en Python).
    /// </summary>
    public class MetadatoElemento
    {
        public string tipo;                 // visor: column / wall / vigas_x / vigas_y
        public string seccion;              // catalogo P–M (p.ej. "col0.70x0.70") o null
        public string material;             // "H30" / "G35"
        public double L;
        public NodosMetadato nodos;
        public EjesLocalesModal ejes_locales;
    }

    public class NodosMetadato
    {
        public int ni;
        public int nj;
        public string i;                    // restriccion extremo i
        public string j;                    // restriccion extremo j
    }

    public class EjesLocalesModal
    {
        public double[] x = new double[3];
        public double[] y = new double[3];
        public double[] z = new double[3];
    }

    public class ElementoInteraccion
    {
        public string seccion;
        public string tipo;
        public double cx;
        public double cy;

        [JsonProperty("demanda")]
        public Dictionary<string, PuntoPM> demanda = new Dictionary<string, PuntoPM>();
    }

    public class SuperposicionInteraccion
    {
        public int n_elementos;
        public double max_dP;
        public double max_dMy;
        public double max_dMz;
        public double max_dM;
    }
}