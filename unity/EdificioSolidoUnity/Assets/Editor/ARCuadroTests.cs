using System.Collections.Generic;
using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Encuadre por 4 esquinas y seguimiento (Corrección 8, partes 5a y 5b).
    //
    //  El usuario marca las 4 esquinas de la cara del elemento real y los
    //  diagramas se dibujan DENTRO de ese recuadro: columnas en vertical (i
    //  abajo), vigas en horizontal (i a la izquierda del que mira), con las x
    //  del modelo en la misma fracción de la longitud del recuadro.
    // =====================================================================

    public class ARCuadroTests
    {
        private const float TOL = 2e-3f;
        private static ARRaiz _raiz;

        private static ARRaiz Raiz()
        {
            if (_raiz == null)
            {
                string ruta = Path.Combine(Application.streamingAssetsPath, ARCargador.Archivo);
                Assert.IsTrue(File.Exists(ruta), "No se encuentra " + ruta);
                _raiz = ARCargador.DesdeJson(File.ReadAllText(ruta));
            }
            return _raiz;
        }

        private static ARInspeccionApp Montar()
        {
            var go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());
            // Cámara a 1,40 m mirando a +Z (derecha = +X).
            app.Camara.transform.position = new Vector3(0f, 1.4f, -5f);
            app.Camara.transform.rotation = Quaternion.identity;
            return app;
        }

        private static void Limpiar(ARInspeccionApp app)
        {
            if (app.Interfaz != null && app.Interfaz.root != null) Object.DestroyImmediate(app.Interfaz.root);
            Object.DestroyImmediate(app.gameObject);
        }

        private static T Campo<T>(ARInspeccionApp app, string nombre) where T : class
        {
            return typeof(ARInspeccionApp).GetField(nombre,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(app) as T;
        }

        private static Transform Contenedor(ARInspeccionApp app, int tag)
        {
            return Campo<Dictionary<int, GameObject>>(app, "contenedores")[tag].transform;
        }

        private static ARGeometriaElemento Geo(ARInspeccionApp app, int tag)
        {
            return Campo<Dictionary<int, ARGeometriaElemento>>(app, "geoPorTag")[tag];
        }

        private static Vector3 Mundo(ARInspeccionApp app, int tag, Vector3 local)
        {
            return Contenedor(app, tag).TransformPoint(local);
        }

        private static ARTrazo Trazo(ARGeometriaElemento g, string etiqueta)
        {
            foreach (var t in g.trazos) if (t.etiqueta == etiqueta) return t;
            return null;
        }

        // Cara de una columna 0,70 × 3,00 en el plano z = 0 (vista desde −Z).
        private static readonly Vector3[] CaraColumna =
        {
            new Vector3(0.35f, 3f, 0f), new Vector3(-0.35f, 0f, 0f),
            new Vector3(-0.35f, 3f, 0f), new Vector3(0.35f, 0f, 0f)
        };

        // Costado de una viga 9,30 × 0,80 entre caras de columnas, en z = 0. La
        // primera esquina está mal marcada a propósito (9,65 en vez de 10,35).
        private static readonly Vector3[] CostadoViga =
        {
            new Vector3(9.65f, 3.96f, 0f), new Vector3(10.35f, 3.16f, 0f),
            new Vector3(19.65f, 3.16f, 0f), new Vector3(10.35f, 3.96f, 0f)
        };

        // ------------------------- geometría pura --------------------------

        [Test]
        public void Columna_RecuadroVertical_iAbajo_LadoPositivoALaIzquierda()
        {
            var q = ARCuadro.Desde4Puntos(CaraColumna, false, Vector3.right, Vector3.forward);
            Assert.IsTrue(q.valido, q.error);
            Assert.AreEqual(1f, Vector3.Dot(q.u, Vector3.up), TOL, "Columna: el elemento va vertical.");
            Assert.AreEqual(-1f, q.w.x, TOL, "Los valores se abren hacia la izquierda del que mira.");
            Assert.AreEqual(3f, q.largo, TOL);
            Assert.AreEqual(0.70f, q.ancho, TOL);
            Assert.Less(Vector3.Distance(q.centro, new Vector3(0f, 1.5f, 0f)), TOL);
        }

        [Test]
        public void Viga_RecuadroHorizontal_iALaIzquierdaDelQueMira()
        {
            var pts = new[] { CostadoViga[1], CostadoViga[2],
                              new Vector3(19.65f, 3.96f, 0f), CostadoViga[3] };
            var q = ARCuadro.Desde4Puntos(pts, true, Vector3.right, Vector3.forward);
            Assert.IsTrue(q.valido, q.error);
            Assert.AreEqual(1f, q.u.x, TOL, "Mirando a +Z, i queda a la izquierda: u = +X.");
            Assert.AreEqual(1f, q.w.y, TOL, "Costado de la viga: recuadro vertical.");
            Assert.IsFalse(q.caraInferior);
            Assert.AreEqual(9.30f, q.largo, TOL);
            Assert.AreEqual(0.80f, q.ancho, TOL);
            Assert.Less(Vector3.Distance(q.centro, new Vector3(15f, 3.56f, 0f)), TOL);

            // Vista desde el otro lado: i sigue a la izquierda del que mira.
            var q2 = ARCuadro.Desde4Puntos(pts, true, Vector3.left, Vector3.back);
            Assert.AreEqual(-1f, q2.u.x, TOL);
        }

        [Test]
        public void Viga_PorSuCaraInferior_RecuadroHorizontal()
        {
            var pts = new[]
            {
                new Vector3(10.35f, 3.16f, -0.3f), new Vector3(19.65f, 3.16f, 0.3f),
                new Vector3(19.65f, 3.16f, -0.3f), new Vector3(10.35f, 3.16f, 0.3f)
            };
            var q = ARCuadro.Desde4Puntos(pts, true, Vector3.right, Vector3.forward);
            Assert.IsTrue(q.valido, q.error);
            Assert.IsTrue(q.caraInferior);
            Assert.AreEqual(1f, q.w.z, TOL, "«Arriba» del diagrama = el lado lejano de la cara inferior.");
            Assert.AreEqual(0.60f, q.ancho, TOL);
        }

        [Test]
        public void ElOrdenDeLasEsquinas_NoImporta()
        {
            var a = ARCuadro.Desde4Puntos(CaraColumna, false, Vector3.right, Vector3.forward);
            var otro = new[] { CaraColumna[3], CaraColumna[0], CaraColumna[1], CaraColumna[2] };
            var b = ARCuadro.Desde4Puntos(otro, false, Vector3.right, Vector3.forward);
            Assert.Less(Vector3.Distance(a.centro, b.centro), TOL);
            Assert.AreEqual(a.largo, b.largo, TOL);
            Assert.AreEqual(a.ancho, b.ancho, TOL);
        }

        [Test]
        public void VigaEnDiagonal_ElEjeSigueALasEsquinas()
        {
            Quaternion r = Quaternion.Euler(0f, -30f, 0f);
            var pts = new Vector3[4];
            for (int k = 0; k < 4; k++) pts[k] = r * (CostadoViga[k] - new Vector3(15f, 0f, 0f));
            var q = ARCuadro.Desde4Puntos(pts, true, Vector3.right, Vector3.forward);
            Vector3 esperado = r * Vector3.right;
            Assert.Greater(Vector3.Dot(q.u, esperado), 0.999f);
        }

        [Test]
        public void RecuadroDemasiadoChico_NoVale()
        {
            var pts = new[] { Vector3.zero, new Vector3(0.05f, 0f, 0f), new Vector3(0f, 0.1f, 0f), Vector3.zero };
            var q = ARCuadro.Desde4Puntos(pts, false, Vector3.right, Vector3.forward);
            Assert.IsFalse(q.valido);
            Assert.IsFalse(string.IsNullOrEmpty(q.error));
        }

        [Test]
        public void PlanoProvisional_ConDosEsquinasDeAbajo_DaLasDeArriba()
        {
            var dos = new List<Vector3> { new Vector3(-0.35f, 0f, 0f), new Vector3(0.35f, 0f, 0f) };
            Vector3 pp, n, p;
            Assert.IsTrue(ARCuadro.PlanoProvisional(dos, Vector3.forward, out pp, out n));
            // Desde la cámara a 5 m, hacia la esquina de arriba a la derecha.
            Vector3 o = new Vector3(0f, 1.4f, -5f);
            Vector3 hacia = new Vector3(0.35f, 3f, 0f) - o;
            Assert.IsTrue(ARCuadro.InterseccionPlano(o, hacia, pp, n, out p));
            Assert.Less(Vector3.Distance(p, new Vector3(0.35f, 3f, 0f)), TOL);
        }

        // ------------------------- en la app -------------------------------

        private static void Encuadrar(ARInspeccionApp app, int tag, IList<Vector3> esquinas,
                                      OrigenCuadro origen = OrigenCuadro.Profundidad)
        {
            app.ElegirElemento(tag);
            app.IniciarEncuadre();
            Assert.AreEqual(ModoMarcado.Cuadro, app.Modo);
            for (int k = 0; k < esquinas.Count; k++)
            {
                Assert.AreEqual(k, app.EsquinasMarcadas);
                StringAssert.Contains("Esquina " + (k + 1) + " de 4", app.Mensaje);
                app.MarcarEsquina(esquinas[k], origen);
            }
        }

        [Test]
        public void Columna26_DiagramasDentroDelRecuadro_iAbajo_jArriba()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);
            Assert.IsTrue(app.TieneEncuadre(26));
            Assert.Less(Vector3.Distance(app.PosicionAncla(), new Vector3(0f, 1.5f, 0f)), TOL,
                        "El ancla va al centro del recuadro.");

            var g = Geo(app, 26);
            Vector3 i = Mundo(app, 26, g.puntoI), j = Mundo(app, 26, g.puntoJ);
            Assert.Less(Vector3.Distance(i, new Vector3(0f, 0f, 0f)), TOL, "i al pie, al centro de la cara.");
            Assert.Less(Vector3.Distance(j, new Vector3(0f, 3f, 0f)), TOL, "j arriba.");

            // Todos los diagramas (N, V, M) quedan dentro del recuadro.
            foreach (var t in g.trazos)
            {
                // El P–M va AFUERA, al lado del recuadro (no es un diagrama del elemento).
                if (t.tipo == ARTipoTrazo.PM || t.tipo == ARTipoTrazo.Demanda) continue;
                if (t.etiqueta == "eje P" || t.etiqueta == "eje M") continue;
                foreach (var p in t.puntos)
                {
                    Vector3 m = Mundo(app, 26, p);
                    Assert.LessOrEqual(Mathf.Abs(m.x), 0.35f + TOL, t.etiqueta + " se sale por el ancho.");
                    Assert.GreaterOrEqual(m.y, -TOL, t.etiqueta + " se sale por abajo.");
                    Assert.LessOrEqual(m.y, 3f + TOL, t.etiqueta + " se sale por arriba.");
                    Assert.AreEqual(0f, m.z, TOL, "Todo sobre la cara marcada.");
                }
            }
            StringAssert.Contains("Encuadre listo", app.Mensaje);
            Limpiar(app);
        }

        [Test]
        public void Viga134_ProporcionesDelModelo_YMomentoPositivoHaciaAbajo()
        {
            var app = Montar();
            Encuadrar(app, 134, new[] { CostadoViga[1], CostadoViga[2],
                                        new Vector3(19.65f, 3.96f, 0f), CostadoViga[3] });
            var g = Geo(app, 134);
            var m = Trazo(g, "M_xz");
            Assert.IsNotNull(m, "Falta el diagrama M_xz.");

            // Eje cero de la franja del M: es la recta de sus extremos (primer y último punto).
            Vector3 c0 = Mundo(app, 134, m.puntos[0]);
            Vector3 c1 = Mundo(app, 134, m.puntos[m.puntos.Count - 1]);
            Assert.AreEqual(10.35f, c0.x, TOL, "i en el borde izquierdo del recuadro.");
            Assert.AreEqual(19.65f, c1.x, TOL, "j en el borde derecho.");

            // Máximo positivo (+77,2 kN·m en x = 4,735 de 10 m) → 47,35 % del recuadro, y hacia ABAJO.
            var el = Raiz().elementos["134"];
            int kMax = 0;
            var vals = el.diagramas["M_xz"].valores;
            for (int k = 1; k < vals.Length; k++) if (vals[k] > vals[kMax]) kMax = k;
            Vector3 pMax = Mundo(app, 134, m.puntos[kMax + 1]);   // +1: el primer punto es el eje cero
            float fx = (float)(el.x[kMax] / el.L);
            Assert.AreEqual(10.35f + fx * 9.30f, pMax.x, TOL, "La x del modelo cae en la misma fracción.");
            Assert.Less(pMax.y, c0.y, "M > 0 (tracción inferior) se dibuja hacia abajo.");

            // En el apoyo i el momento es negativo: hacia arriba.
            Vector3 pI = Mundo(app, 134, m.puntos[1]);
            Assert.Greater(pI.y, c0.y);
            Limpiar(app);
        }

        [Test]
        public void Amplitud_YCambioDeElemento_ConservanElRecuadro()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna);
            var cara14 = new Vector3[4];
            for (int k = 0; k < 4; k++) cara14[k] = CaraColumna[k] + new Vector3(-10f, 0f, 2f);
            Encuadrar(app, 14, cara14);

            app.ElegirElemento(26);
            typeof(ARInspeccionApp).GetMethod("CambiarAmplitud",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(app, new object[] { 1.25f });
            Assert.IsTrue(app.TieneEncuadre(26), "«Amplitud +» redibuja dentro del mismo recuadro.");
            var g = Geo(app, 26);
            Assert.Less(Vector3.Distance(Mundo(app, 26, g.puntoJ), new Vector3(0f, 3f, 0f)), TOL);

            app.ElegirElemento(14);
            var g14 = Geo(app, 14);
            Assert.Less(Vector3.Distance(Mundo(app, 14, g14.puntoI), new Vector3(-10f, 0f, 2f)), TOL,
                        "La columna 14 sigue en SU recuadro.");
            Limpiar(app);
        }

        [Test]
        public void QuitarAncla_VuelveAlDibujoDelModelo()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna);
            typeof(ARInspeccionApp).GetMethod("QuitarAncla",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(app, null);
            Assert.IsFalse(app.TieneEncuadre(26));
            Assert.AreEqual(3.96f, Geo(app, 26).puntoJ.y, TOL, "Sin recuadro, la columna vuelve a 3,96 m.");
            Limpiar(app);
        }

        // ---------------- Cambio 01: recuadro plano y de frente ----------------

        [Test]
        public void LasEsquinas_QuedanEnUnPlanoDeFrenteALaCamara()
        {
            var app = Montar();
            // Cámara mirando 30° a la derecha de +Z.
            app.Camara.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Vector3 f = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
            app.ElegirElemento(26);
            app.IniciarEncuadre();
            Vector3 p0 = new Vector3(1f, 0f, 4f);
            app.MarcarEsquina(p0, OrigenCuadro.Profundidad);
            // Las otras tres "medidas" a distintas profundidades: deben ir al mismo plano.
            Vector3 r = Vector3.Cross(Vector3.up, f).normalized;
            app.MarcarEsquina(p0 + r * 0.7f + f * 1.5f);
            app.MarcarEsquina(p0 + r * 0.7f + Vector3.up * 3f - f * 2f);
            app.MarcarEsquina(p0 + Vector3.up * 3f + f * 0.8f);
            Assert.IsTrue(app.TieneEncuadre(26));
            var q = app.Encuadre(26);
            Assert.AreEqual(0.70f, q.ancho, TOL);
            Assert.AreEqual(3f, q.largo, TOL);
            Assert.AreEqual(0f, Vector3.Dot(q.centro - p0, f), TOL,
                            "Todo el recuadro a la misma distancia que la 1ª esquina.");
            Assert.Greater(Vector3.Dot(q.n, -f), 0.999f, "El recuadro mira hacia la cámara.");
            Limpiar(app);
        }

        [Test]
        public void EsquinasDeArriba_SeProyectanEnLaCaraDeLasDeAbajo()
        {
            var app = Montar();
            app.ElegirElemento(26);
            app.IniciarEncuadre();
            app.MarcarEsquina(new Vector3(-0.35f, 0f, 0f), OrigenCuadro.Plano);
            app.MarcarEsquina(new Vector3(0.35f, 0f, 0f), OrigenCuadro.Plano);
            // Arriba "medidas" 2 m detrás de la columna (lo que pasaba en terreno).
            app.MarcarEsquina(new Vector3(0.35f, 3f, 2f), OrigenCuadro.Profundidad);
            app.MarcarEsquina(new Vector3(-0.35f, 3f, -1.5f), OrigenCuadro.Profundidad);
            Assert.IsTrue(app.TieneEncuadre(26));
            var q = app.Encuadre(26);
            Assert.AreEqual(0.70f, q.ancho, TOL, "El recuadro queda del ancho de la cara.");
            Assert.AreEqual(3f, q.largo, TOL);
            Assert.AreEqual(0f, q.centro.z, TOL, "Y sobre la cara, no detrás.");
            Limpiar(app);
        }

        [Test]
        public void RecuadroAbsurdo_PideRepetir()
        {
            var app = Montar();
            app.ElegirElemento(26);
            app.IniciarEncuadre();
            app.MarcarEsquina(new Vector3(-0.35f, 0f, 0f));
            app.MarcarEsquina(new Vector3(0.35f, 0f, 0f));
            app.MarcarEsquina(new Vector3(0.35f, 50f, 0f));
            app.MarcarEsquina(new Vector3(-0.35f, 50f, 0f));
            Assert.AreEqual(ModoMarcado.Cuadro, app.Modo);
            Assert.AreEqual(0, app.EsquinasMarcadas, "Se vuelve a empezar.");
            StringAssert.Contains("se perdió", app.Mensaje);
            app.MarcarEsquina(CaraColumna[0]);
            app.MarcarEsquina(CaraColumna[1]);
            app.MarcarEsquina(CaraColumna[2]);
            app.MarcarEsquina(CaraColumna[3]);
            Assert.IsTrue(app.TieneEncuadre(26));
            Limpiar(app);
        }

        [Test]
        public void ElDiagrama_QuedaVertical_AunqueARCoreInclineElAncla()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna);
            var ancla = Campo<Object>(app, "ancla") as Component;
            var raiz = Campo<GameObject>(app, "raizVisual").transform;
            ancla.transform.rotation = Quaternion.Euler(6f, 30f, 2f);   // ARCore lo inclinó
            app.NivelarRaiz();
            Assert.Greater(Vector3.Dot(raiz.up, Vector3.up), 0.9999f, "La vertical es la de la gravedad.");
            Vector3 f = raiz.forward; f.y = 0f;
            Assert.Greater(Vector3.Dot(f.normalized, Quaternion.Euler(0f, 30f, 0f) * Vector3.forward), 0.9999f,
                           "Conserva el giro en planta del ancla.");
            Assert.Less(Vector3.Distance(raiz.position, ancla.transform.position), TOL);
            Limpiar(app);
        }

        [Test]
        public void ElAnillo_SeVeDelMismoTamanoCercaOLejos()
        {
            float r2 = ARInspeccionApp.EscalaReticula(2f) * ARInspeccionApp.RadioReticula;
            float r10 = ARInspeccionApp.EscalaReticula(10f) * ARInspeccionApp.RadioReticula;
            Assert.AreEqual(r2 / 2f, r10 / 10f, 1e-5f, "Radio proporcional a la distancia.");
            Assert.AreEqual(2f * ARInspeccionApp.RadioReticulaRelativo, r2, 1e-5f);
        }

        [Test]
        public void LosRotulos_DelRecuadro_EstanImpresosYCaben()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna);
            var q = app.Encuadre(26);
            foreach (var m in Geo(app, 26).marcas)
            {
                if (string.IsNullOrEmpty(m.texto)) continue;
                Assert.Greater(m.normalPlano.sqrMagnitude, 0.5f, "Rótulo sin plano: " + m.texto);
                Assert.Greater(Vector3.Dot(m.normalPlano, q.n), 0.999f, "En el plano del recuadro.");
                if (m.tipo == ARTipoTrazo.Normal || m.tipo == ARTipoTrazo.Cortante || m.tipo == ARTipoTrazo.Momento)
                    Assert.LessOrEqual(0.55f * m.alturaLetra * m.texto.Length, q.ancho + 1e-3f,
                                       "No cabe en el ancho: " + m.texto);
            }
            Limpiar(app);
        }

        [Test]
        public void LosRotulos_NoGiranNiCambianDeTamanoConLaCamara()
        {
            var app = Montar();
            app.ElegirElemento(14);
            app.Camara.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            app.IniciarMarcadoBase();
            app.MarcarPunto(new Vector3(3f, 0f, -1f));      // dibujo 1:1 con rótulos
            var c = Campo<Dictionary<int, GameObject>>(app, "contenedores")[14].transform;
            var bbs = c.GetComponentsInChildren<ARBillboard>(true);
            if (bbs.Length == 0) Assert.Pass("Sin fuente en este Editor: no hay rótulos 3D.");
            var antes = new Quaternion[bbs.Length];
            for (int k = 0; k < bbs.Length; k++)
            {
                Assert.IsTrue(bbs[k].congelado, "Al colocar, el rótulo queda fijo.");
                antes[k] = bbs[k].transform.rotation;
            }
            // El usuario se mueve y gira: los rótulos no.
            app.Camara.transform.position = new Vector3(8f, 1.4f, 5f);
            app.Camara.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            var lu = typeof(ARBillboard).GetMethod("LateUpdate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int k = 0; k < bbs.Length; k++)
            {
                lu.Invoke(bbs[k], null);
                Assert.Less(Quaternion.Angle(antes[k], bbs[k].transform.rotation), 1e-3f);
                Assert.AreEqual(1f, bbs[k].transform.localScale.x, 1e-5f);
            }
            Limpiar(app);
        }

        [Test]
        public void AlTerminar_ElMenuSePliega_YElBotonLoDespliega()
        {
            var app = Montar();
            var ui = app.Interfaz;
            Assert.IsTrue(ui.ControlesVisibles);
            Encuadrar(app, 26, CaraColumna);
            Assert.IsFalse(ui.ControlesVisibles, "Con el diagrama puesto, el menú no lo tapa.");
            ui.btnMenu.onClick.Invoke();
            Assert.IsTrue(ui.ControlesVisibles);
            ui.btnMenu.onClick.Invoke();
            Assert.IsFalse(ui.ControlesVisibles);
            Limpiar(app);
        }

        [Test]
        public void PM_SoloElExtremoQueGobiernaLlevaRotulo()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna);
            int conTexto = 0, demandas = 0;
            foreach (var m in Geo(app, 26).marcas)
            {
                if (m.tipo != ARTipoTrazo.Demanda) continue;
                demandas++;
                if (!string.IsNullOrEmpty(m.texto)) conTexto++;
            }
            Assert.AreEqual(2, demandas, "Siguen las dos marcas (i y j).");
            Assert.AreEqual(1, conTexto, "Pero un solo rótulo: no se enciman.");
            Limpiar(app);
        }

        [Test]
        public void EsquinasSinMedir_LoAvisa()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna, OrigenCuadro.Estimado);
            StringAssert.Contains("sin medir", app.Mensaje);
            Limpiar(app);
        }

        [Test]
        public void CambiarDeElemento_DescartaLasEsquinasAMedias()
        {
            var app = Montar();
            app.ElegirElemento(26);
            app.IniciarEncuadre();
            app.MarcarEsquina(CaraColumna[0]);
            app.MarcarEsquina(CaraColumna[1]);
            app.ElegirElemento(134);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);
            Assert.AreEqual(0, app.EsquinasMarcadas);
            Limpiar(app);
        }

        [Test]
        public void EsquinaEstimada_UsaLasAlturasDelModelo()
        {
            var app = Montar();
            app.ElegirElemento(26);
            Vector3 p;
            OrigenCuadro o;
            var arriba = new Ray(new Vector3(0f, 1.4f, 0f), new Vector3(0f, 1f, 1f).normalized);
            Assert.IsTrue(app.PuntoCuadroDesdeRayo(arriba, 1.4f, out p, out o));
            Assert.AreEqual(OrigenCuadro.Estimado, o);
            Assert.AreEqual(3.96f, p.y, TOL, "Mirando arriba: la cabeza de la columna del modelo.");
            var abajo = new Ray(new Vector3(0f, 1.4f, 0f), new Vector3(0f, -1f, 1f).normalized);
            Assert.IsTrue(app.PuntoCuadroDesdeRayo(abajo, 1.4f, out p, out o));
            Assert.AreEqual(0f, p.y, TOL, "Mirando abajo: el piso estimado.");
            Limpiar(app);
        }

        // ------------------------- seguimiento (5b) -------------------------

        [Test]
        public void SinSeguimiento_SeOculta_YAlVolverReapareceEnSuLugar()
        {
            var app = Montar();
            Encuadrar(app, 26, CaraColumna);
            var raiz = Campo<GameObject>(app, "raizVisual");
            var g = Geo(app, 26);
            Vector3 j0 = Mundo(app, 26, g.puntoJ);

            app.AplicarSeguimiento(false, 0.3f);
            Assert.IsTrue(raiz.activeSelf, "Un instante sin seguimiento no basta (sin parpadeos).");
            app.AplicarSeguimiento(false, 0.3f);
            Assert.IsFalse(raiz.activeSelf, "Sin seguimiento, el diagrama se oculta: no queda flotando mal.");
            Assert.IsTrue(app.OcultoPorSeguimiento);
            StringAssert.Contains("apunta de nuevo", app.Mensaje);

            app.AplicarSeguimiento(true, 0.016f);
            Assert.IsTrue(raiz.activeSelf);
            Assert.IsFalse(app.OcultoPorSeguimiento);
            Assert.Less(Vector3.Distance(Mundo(app, 26, g.puntoJ), j0), TOL, "Y vuelve exactamente a su recuadro.");
            Limpiar(app);
        }
    }
}
