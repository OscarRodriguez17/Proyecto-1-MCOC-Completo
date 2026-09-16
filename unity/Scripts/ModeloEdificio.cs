using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCOC.Unity
{
    public class ModeloEdificio
    {
        public string proyecto;
        public string unidades;
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
}
