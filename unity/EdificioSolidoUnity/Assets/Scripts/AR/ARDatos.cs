using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace MCOC.AR
{
    // =====================================================================
    //  Contrato de datos AR (Semana 06)
    //  Refleja EXACTAMENTE results/ar_elementos.json, que escribe
    //  src/ar/exportar_ar.py. El teléfono NO calcula esfuerzos: solo lee
    //  estos diagramas ya muestreados y los dibuja.
    // =====================================================================

    /// <summary>Raíz del contrato. `elementos` viene indexado por tag en texto.</summary>
    public class ARRaiz
    {
        public int version;
        public string edificio;
        public string caso;

        [JsonProperty("caso_descripcion")]
        public string casoDescripcion;

        public ARUnidades unidades;
        public string coordenadas;
        public ARConvenciones convenciones;
        public string fuente;

        [JsonProperty("elementos")]
        public Dictionary<string, ARElemento> elementos =
            new Dictionary<string, ARElemento>();

        /// <summary>Elementos ordenados por tag ascendente (determinista para el menú).</summary>
        public List<ARElemento> EnOrden()
        {
            var claves = new List<string>(elementos.Keys);
            claves.Sort((a, b) => int.Parse(a).CompareTo(int.Parse(b)));
            var lista = new List<ARElemento>(claves.Count);
            foreach (var k in claves) lista.Add(elementos[k]);
            return lista;
        }
    }

    public class ARUnidades
    {
        public string longitud;
        public string fuerza;
        public string momento;
    }

    /// <summary>Convenciones de signo/dibujo, mostradas en pantalla para trazabilidad.</summary>
    public class ARConvenciones
    {
        public string x;
        public string N;
        public string M_xz;
        public string M_xy;
        public string V;
        public string dibujo;
    }

    public class ARElemento
    {
        public int tag;
        public string tipo;

        [JsonProperty("tipo_modelo")]
        public string tipoModelo;

        public string seccion;
        public string material;
        public double L;
        public string ubicacion;
        public ARExtremos extremos;
        public AREjesLocales ejes_locales;
        public double[] x;
        public Dictionary<string, ARDiagrama> diagramas;
        public ARPrincipal principal;

        [JsonProperty("coeficientes_extremo_i")]
        public ARCoeficientes coeficientesExtremoI;

        public ARAnclaje anclaje;

        public ARPm pm;   // ausente en vigas
    }

    public class ARExtremos
    {
        public ARExtremo i;
        public ARExtremo j;
    }

    public class ARExtremo
    {
        public int nodo;

        [JsonProperty("xyz_opensees")]
        public double[] xyzOpensees;

        [JsonProperty("xyz_unity")]
        public double[] xyzUnity;

        public string etiqueta;
        public string restriccion;
    }

    public class AREjesLocales
    {
        public AREjes opensees;
        public AREjes unity;
    }

    public class AREjes
    {
        public double[] x;
        public double[] y;
        public double[] z;
    }

    /// <summary>Diagrama muestreado. `lado_positivo` da el sentido de dibujo del valor positivo.</summary>
    public class ARDiagrama
    {
        public double[] valores;
        public double i;
        public double j;

        [JsonProperty("max_abs")]
        public ARPico maxAbs;

        [JsonProperty("max_pos")]
        public ARPico maxPos;

        [JsonProperty("max_neg")]
        public ARPico maxNeg;

        [JsonProperty("lado_positivo")]
        public ARLadoPositivo ladoPositivo;
    }

    public class ARPico
    {
        public double valor;
        public double x;
    }

    /// <summary>
    /// Sentido en que se dibuja un valor positivo. Viene precalculado por el
    /// exportador, así que la app NUNCA reinterpreta signos: sólo lo aplica.
    /// </summary>
    public class ARLadoPositivo
    {
        [JsonProperty("vector_opensees")]
        public double[] vectorOpensees;

        [JsonProperty("vector_unity")]
        public double[] vectorUnity;

        public string texto;
    }

    public class ARPrincipal
    {
        public string M;
        public string V;
        public string plano;
    }

    public class ARCoeficientes
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

    /// <summary>
    /// Punto del PISO bajo el centro del elemento, tal como lo calcula
    /// `src/ar/exportar_ar.py::_anclaje`. La app resta `puntoUnity` a toda la
    /// geometría: el elemento queda centrado en el ancla y a su altura real
    /// sobre ese piso (la viga 134 a 3,96 m; las columnas desde el piso).
    /// </summary>
    public class ARAnclaje
    {
        [JsonProperty("punto_opensees")]
        public double[] puntoOpensees;

        [JsonProperty("punto_unity")]
        public double[] puntoUnity;

        [JsonProperty("z_piso")]
        public double zPiso;

        [JsonProperty("altura_sobre_piso")]
        public ARAltura alturaSobrePiso;

        public string nota;
    }

    /// <summary>Altura de cada extremo del elemento sobre el piso del ancla.</summary>
    public class ARAltura
    {
        public double i;
        public double j;
    }

    /// <summary>Diagrama P–M de balanceo (sólo columnas).</summary>
    public class ARPm
    {
        public string seccion;
        public ARPmEnvolvente envolvente;
        public ARPunto balanceado;
        public ARPmDemanda demanda;
        public string gobierna;

        [JsonProperty("M_capacidad_en_P")]
        public double mCapacidadEnP;

        public double DC;
        public string nota;
    }

    public class ARPmEnvolvente
    {
        public double[] P;
        public double[] M;
    }

    public class ARPunto
    {
        public double P;
        public double M;
    }

    public class ARPmDemanda
    {
        public ARPunto i;
        public ARPunto j;
    }

    // =====================================================================
    //  Carga
    // =====================================================================

    public static class ARCargador
    {
        public const string Archivo = "ar_elementos.json";

        /// <summary>
        /// Ruta esperada en el editor / escritorio.
        /// En Android NO usar: streamingAssetsPath vive dentro del APK y
        /// File.ReadAllText falla. Usar Cargar().
        /// </summary>
        public static string RutaEscritorio =>
            Path.Combine(Application.streamingAssetsPath, Archivo);

        /// <summary>
        /// Lee el contrato desde StreamingAssets de forma compatible con
        /// Android (dentro del APK hay que pasar por UnityWebRequest).
        /// </summary>
        public static IEnumerator Cargar(Action<ARRaiz> alOk, Action<string> alError)
        {
            string ruta = RutaEscritorio;

            if (!ruta.Contains("://"))
            {
                if (!File.Exists(ruta))
                {
                    alError($"No se encuentra {Archivo} en StreamingAssets ({ruta}).");
                    yield break;
                }
                ARRaiz raiz = null;
                string error = null;
                try
                {
                    raiz = DesdeJson(File.ReadAllText(ruta));
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                if (raiz != null) alOk(raiz); else alError(error);
                yield break;
            }

            using (var req = UnityWebRequest.Get(ruta))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    alError($"Fallo al leer {Archivo}: {req.error}");
                    yield break;
                }
                ARRaiz raiz = null;
                string error = null;
                try
                {
                    raiz = DesdeJson(req.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                if (raiz != null) alOk(raiz); else alError(error);
            }
        }

        /// <summary>Deserializa y valida lo mínimo imprescindible.</summary>
        public static ARRaiz DesdeJson(string json)
        {
            var raiz = JsonConvert.DeserializeObject<ARRaiz>(json);
            if (raiz == null) throw new Exception("JSON vacío o inválido.");
            if (raiz.elementos == null || raiz.elementos.Count == 0)
                throw new Exception("El contrato no trae elementos.");
            Validar(raiz);
            return raiz;
        }

        /// <summary>
        /// Comprueba que x y valores tengan la misma longitud en cada diagrama.
        /// Es el fallo silencioso más probable si el JSON se regenera a mano.
        /// </summary>
        public static void Validar(ARRaiz raiz)
        {
            foreach (var el in raiz.elementos.Values)
            {
                if (el.x == null || el.x.Length < 2)
                    throw new Exception($"Elemento {el.tag}: 'x' con menos de 2 puntos.");
                foreach (var kv in el.diagramas)
                {
                    var d = kv.Value;
                    if (d.valores == null)
                        throw new Exception($"Elemento {el.tag}, diagrama {kv.Key}: sin 'valores'.");
                    if (d.valores.Length != el.x.Length)
                        throw new Exception(
                            $"Elemento {el.tag}, diagrama {kv.Key}: " +
                            $"{d.valores.Length} valores para {el.x.Length} abscisas.");
                }
                if (el.anclaje == null)
                    throw new Exception($"Elemento {el.tag}: sin 'anclaje'.");
                if (el.anclaje.puntoUnity == null || el.anclaje.puntoUnity.Length != 3)
                    throw new Exception(
                        $"Elemento {el.tag}: 'anclaje.punto_unity' debe tener 3 componentes.");
                if (el.anclaje.alturaSobrePiso == null)
                    throw new Exception(
                        $"Elemento {el.tag}: 'anclaje.altura_sobre_piso' ausente.");
                if (el.pm != null)
                {
                    if (el.pm.envolvente == null ||
                        el.pm.envolvente.P == null ||
                        el.pm.envolvente.M == null ||
                        el.pm.envolvente.P.Length != el.pm.envolvente.M.Length)
                        throw new Exception($"Elemento {el.tag}: envolvente P–M descuadrada.");
                }
            }
        }
    }
}
