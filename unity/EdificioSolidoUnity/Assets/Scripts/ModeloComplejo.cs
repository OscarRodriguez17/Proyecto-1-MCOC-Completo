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
}