using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Colocación MARCANDO el elemento real, a nivel de APP (Corrección 5).
    //
    //  En terreno el diagrama "se alejaba" del elemento: el ancla quedaba bajo
    //  el usuario («Colocar aquí») o donde apuntaba el centro de la pantalla,
    //  no en la base del elemento. Ahora el usuario marca en el piso:
    //   · viga: bajo el extremo i y bajo el extremo j;
    //   · columna: el pie de la cara que ve.
    //
    //  Estos tests recorren el MISMO camino que el toque en el teléfono
    //  (IniciarMarcado… → MarcarPunto), sin sesión AR, y miden sobre la
    //  jerarquía real ancla → raíz → contenedor.
    // =====================================================================

    public class ARMarcadoTests
    {
        private const float TOL = 1e-3f;

        private static ARRaiz _raiz;

        private static ARRaiz Raiz()
        {
            if (_raiz == null)
            {
                string ruta = Path.Combine(Application.streamingAssetsPath,
                                           ARCargador.Archivo);
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
            return app;
        }

        private static void Limpiar(ARInspeccionApp app)
        {
            if (app.Interfaz != null && app.Interfaz.root != null)
                Object.DestroyImmediate(app.Interfaz.root);
            Object.DestroyImmediate(app.gameObject);
        }

        private static T Campo<T>(ARInspeccionApp app, string nombre) where T : class
        {
            var f = typeof(ARInspeccionApp).GetField(nombre,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, "No existe el campo " + nombre);
            return f.GetValue(app) as T;
        }

        private static Transform Contenedor(ARInspeccionApp app)
        {
            var mapa = Campo<System.Collections.Generic.Dictionary<int, GameObject>>(app, "contenedores");
            GameObject go;
            Assert.IsTrue(mapa.TryGetValue(app.TagSeleccionado, out go) && go != null,
                          "No hay contenedor para el tag " + app.TagSeleccionado);
            return go.transform;
        }

        private static ARGeometriaElemento Geometria(ARInspeccionApp app)
        {
            var mapa = Campo<System.Collections.Generic.Dictionary<int, ARGeometriaElemento>>(app, "geoPorTag");
            ARGeometriaElemento g;
            Assert.IsTrue(mapa.TryGetValue(app.TagSeleccionado, out g) && g != null);
            return g;
        }

        private static Vector3 Extremo(ARInspeccionApp app, bool j)
        {
            var g = Geometria(app);
            return Contenedor(app).TransformPoint(j ? g.puntoJ : g.puntoI);
        }

        private static bool AnclaExiste(ARInspeccionApp app)
        {
            var f = typeof(ARInspeccionApp).GetField("ancla",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var v = f.GetValue(app) as Object;
            return v != null;
        }

        // -----------------------------------------------------------------
        //  Viga: dos toques en el piso
        // -----------------------------------------------------------------

        [TestCase(0f, 0f, TestName = "Viga134_Marcada_Recta_CamaraAlFrente")]
        [TestCase(37f, 0f, TestName = "Viga134_Marcada_Girada37")]
        [TestCase(0f, 123f, TestName = "Viga134_Marcada_CamaraMirandoAOtroLado")]
        [TestCase(-90f, 200f, TestName = "Viga134_Marcada_GiradaYCamaraGirada")]
        public void Viga134_DosToques_QuedaSobreLaRectaMarcada(float giroPuntos, float yawCamara)
        {
            var app = Montar();
            app.Camara.transform.rotation = Quaternion.Euler(0f, yawCamara, 0f);
            app.ElegirElemento(134);

            Quaternion q = Quaternion.Euler(0f, giroPuntos, 0f);
            Vector3 Pi = q * new Vector3(10f, 0f, 2f);
            Vector3 Pj = q * new Vector3(20f, 0f, 2f);

            app.IniciarMarcadoViga();
            Assert.AreEqual(ModoMarcado.ExtremoI, app.Modo);
            app.MarcarPunto(Pi);
            Assert.AreEqual(ModoMarcado.ExtremoJ, app.Modo);
            Assert.IsFalse(AnclaExiste(app), "Con un solo punto todavía no hay ancla.");
            app.MarcarPunto(Pj);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);
            Assert.IsTrue(AnclaExiste(app));
            Assert.IsTrue(app.TieneRotacionMarcada);

            // Se marcó el PIE de cada columna de apoyo (cara visible): el eje de la
            // viga está media sección (0,35 m) más adentro, en la dirección de la cámara.
            Vector3 f = Quaternion.Euler(0f, yawCamara, 0f) * Vector3.forward;
            Pi += f * 0.35f;
            Pj += f * 0.35f;

            // Ancla en el punto medio de los centros de las columnas.
            Vector3 medio = (Pi + Pj) * 0.5f;
            Vector3 ancla = app.PosicionAncla();
            Assert.AreEqual(medio.x, ancla.x, TOL);
            Assert.AreEqual(medio.y, ancla.y, TOL);
            Assert.AreEqual(medio.z, ancla.z, TOL);

            // Extremos reales: i sobre Pi, j sobre Pj (en planta), a 3,96 m.
            Vector3 i = Extremo(app, false);
            Vector3 j = Extremo(app, true);
            Vector3 recta = (Pj - Pi).normalized;
            Assert.Greater(Vector3.Dot((j - i).normalized, recta), 0.999f,
                "La viga debe quedar de Pi a Pj sin importar hacia dónde mira la cámara.");
            Assert.AreEqual(10f, (j - i).magnitude, TOL, "Escala 1:1.");
            Assert.AreEqual(Pi.x, i.x, 0.01f); Assert.AreEqual(Pi.z, i.z, 0.01f);
            Assert.AreEqual(Pj.x, j.x, 0.01f); Assert.AreEqual(Pj.z, j.z, 0.01f);
            Assert.AreEqual(3.96f, i.y - ancla.y, TOL);
            Assert.AreEqual(3.96f, j.y - ancla.y, TOL);

            StringAssert.Contains("Medido", app.Mensaje);
            StringAssert.DoesNotContain("revisa", app.Mensaje);

            Limpiar(app);
        }

        [Test]
        public void Viga134_Restablecer_VuelveALaRotacionMarcada()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoViga();
            app.MarcarPunto(new Vector3(0f, 0f, 0f));
            app.MarcarPunto(new Vector3(0f, 0f, 10f));   // recta hacia +Z

            // Un giro de dos dedos cualquiera…
            Contenedor(app).localRotation = Quaternion.Euler(0f, 50f, 0f) * Contenedor(app).localRotation;

            // …y «Restablecer» vuelve a lo marcado, no a la orientación por cámara.
            var m = typeof(ARInspeccionApp).GetMethod("Restablecer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            m.Invoke(app, null);

            Vector3 eje = (Extremo(app, true) - Extremo(app, false)).normalized;
            Assert.Greater(Vector3.Dot(eje, Vector3.forward), 0.999f);
            Limpiar(app);
        }

        [Test]
        public void Viga134_LuzMedidaMuyDistinta_Avisa()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoViga();
            app.MarcarPunto(new Vector3(0f, 0f, 0f));
            app.MarcarPunto(new Vector3(8f, 0f, 0f));    // 8 m contra L = 10 m (−20 %)
            Assert.IsTrue(AnclaExiste(app), "Se coloca igual: el aviso no bloquea.");
            StringAssert.Contains("revisa", app.Mensaje);
            Assert.AreEqual(10f, (Extremo(app, true) - Extremo(app, false)).magnitude, TOL,
                            "Aunque la medida no calce, la escala sigue 1:1.");
            Limpiar(app);
        }

        [Test]
        public void Viga134_PuntosDemasiadoCerca_PideMarcarDeNuevo()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoViga();
            app.MarcarPunto(new Vector3(1f, 0f, 1f));
            app.MarcarPunto(new Vector3(1.2f, 0f, 1f));
            Assert.AreEqual(ModoMarcado.ExtremoI, app.Modo, "Vuelve a pedir el extremo i.");
            Assert.IsFalse(AnclaExiste(app));
            Limpiar(app);
        }

        // -----------------------------------------------------------------
        //  Columna: un toque al pie de la cara visible
        // -----------------------------------------------------------------

        [TestCase(0f, TestName = "Columna14_Base_CamaraAMasZ")]
        [TestCase(90f, TestName = "Columna14_Base_CamaraAMasX")]
        [TestCase(-140f, TestName = "Columna14_Base_CamaraGirada")]
        public void Columna14_UnToque_BaseEnElCentroDeLaSeccion(float yawCamara)
        {
            var app = Montar();
            app.ElegirElemento(14);
            app.Camara.transform.rotation = Quaternion.Euler(20f, yawCamara, 0f); // algo hacia el piso

            Vector3 cara = new Vector3(3f, 0f, -1f);
            app.IniciarMarcadoBase();
            Assert.AreEqual(ModoMarcado.Base, app.Modo);
            app.MarcarPunto(cara);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);

            // El centro de la base está 0,35 m (media sección 0,70) más adentro,
            // en la dirección horizontal en que mira la cámara.
            Vector3 f = Quaternion.Euler(0f, yawCamara, 0f) * Vector3.forward;
            Vector3 esperado = cara + f * 0.35f;
            Vector3 b = Extremo(app, false);
            Vector3 h = Extremo(app, true);
            Assert.AreEqual(esperado.x, b.x, TOL);
            Assert.AreEqual(esperado.z, b.z, TOL);
            Assert.AreEqual(0f, b.y, TOL, "La base queda en el piso marcado.");
            Assert.AreEqual(3.96f, h.y, TOL, "La cabeza a 3,96 m.");
            Assert.AreEqual(b.x, h.x, TOL, "La columna queda vertical.");
            Assert.AreEqual(b.z, h.z, TOL);
            Assert.IsFalse(app.TieneRotacionMarcada,
                           "La columna se orienta por la cámara (tracción de M>0 a la izquierda).");

            // Y conserva la convención de lado: M > 0 hacia la izquierda del que mira.
            var g = Geometria(app);
            Vector3 lado = Contenedor(app).rotation * g.ladoPrincipal;
            Vector3 izquierda = -ARColocacion.Derecha(app.Camara.transform.forward);
            Assert.Greater(Vector3.Dot(lado.normalized, izquierda), 0.999f);
            Limpiar(app);
        }

        // -----------------------------------------------------------------
        //  Estados
        // -----------------------------------------------------------------

        [Test]
        public void CambiarDeElemento_CancelaElMarcado()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoViga();
            app.MarcarPunto(new Vector3(0f, 0f, 0f));
            Assert.AreEqual(ModoMarcado.ExtremoJ, app.Modo);

            app.ElegirElemento(14);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo,
                            "Lo marcado de la viga no vale para la columna.");
            Limpiar(app);
        }

        [Test]
        public void IniciarElModoEquivocado_SeCorrigeSolo()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoBase();      // es una viga: pasa a marcar extremos
            Assert.AreEqual(ModoMarcado.ExtremoI, app.Modo);
            app.CancelarMarcado();
            app.ElegirElemento(26);
            app.IniciarMarcadoViga();      // es una columna: pasa a marcar base
            Assert.AreEqual(ModoMarcado.Base, app.Modo);
            Limpiar(app);
        }

        [Test]
        public void MarcarSinModo_NoColocaNada()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.MarcarPunto(new Vector3(1f, 0f, 1f));
            Assert.IsFalse(AnclaExiste(app));
            StringAssert.Contains("Marcar", app.Mensaje);
            Limpiar(app);
        }

        [Test]
        public void QuitarAncla_BorraLaRotacionMarcada()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoViga();
            app.MarcarPunto(new Vector3(0f, 0f, 0f));
            app.MarcarPunto(new Vector3(10f, 0f, 0f));
            Assert.IsTrue(app.TieneRotacionMarcada);

            var m = typeof(ARInspeccionApp).GetMethod("QuitarAncla",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            m.Invoke(app, null);
            Assert.IsFalse(app.TieneRotacionMarcada);
            Assert.IsFalse(AnclaExiste(app));
            Limpiar(app);
        }
    }
}
