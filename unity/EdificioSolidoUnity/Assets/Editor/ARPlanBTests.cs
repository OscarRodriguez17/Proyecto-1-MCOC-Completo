using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MCOC.AR;
using System.IO;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Plan B a nivel de APP (Corrección 3, C; ajustada en la Corrección 4).
    //
    //  `ARColocacionTests` verifica la geometría pura. Esto verifica el
    //  CAMINO: que el botón «Colocar aquí» places el ancla 1,40 m bajo el
    //  teléfono, que los botones de 5 cm lo suban y lo bajen, y que al mover
    //  el piso no se pierda el elemento seleccionado ni cambie su tamaño.
    //
    //  Corrección 4: los botones de 5 cm ya NO escriben en
    //  `ancla.transform` (ARCore reescribe la pose del ARAnchor y el ajuste se
    //  perdería). Acumulan un offset que se aplica a la raíz VISUAL, y el piso
    //  efectivo es `ancla.position + (0, offsetPiso, 0)`.
    //
    //  Importa probarlo sin sesión AR: el Plan B existe precisamente para
    //  cuando no hay plano ni tracking.
    // =====================================================================

    public class ARPlanBTests
    {
        private const float TOL = 1e-4f;

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

        private static ARInspeccionApp Montar(out GameObject go)
        {
            go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());
            return app;
        }

        private static void Limpiar(ARInspeccionApp app, GameObject go)
        {
            var campo = typeof(ARInspeccionApp).GetField("ui",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ui = campo.GetValue(app) as ARInterfaz;
            if (ui != null && ui.root != null) Object.DestroyImmediate(ui.root);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ColocarAqui_Ancla1_40mBajoLaCamara()
        {
            GameObject go;
            var app = Montar(out go);

            Assert.IsNotNull(app.Camara, "El Plan B se apoya en la cámara del rig.");

            Vector3 camara = app.Camara.transform.position;
            Vector3 piso = app.ColocarAqui();

            Assert.AreEqual(camara.x, piso.x, TOL);
            Assert.AreEqual(camara.z, piso.z, TOL);
            Assert.AreEqual(camara.y - 1.40f, piso.y, TOL,
                            "El piso se supone 1,40 m por debajo del teléfono.");
            Assert.AreEqual(piso.y, app.PosicionAncla().y, TOL);
            Assert.AreEqual(0f, app.OffsetPiso(), TOL,
                            "Al colocar se empieza sin desplazamiento.");

            Limpiar(app, go);
        }

        /// <summary>
        /// Los botones de 5 cm mueven el PISO EFFECTIVO de 5 en 5 cm, pero NO
        /// escriben en `ancla.transform`: la pose del ARAnchor la reescribe
        /// ARCore en cuanto el subsystem la actualiza.
        /// </summary>
        [Test]
        public void PisoMasYPisoMenos_MuevenElPisoDe5En5cmSinTocarElAncla()
        {
            GameObject go;
            var app = Montar(out go);

            Vector3 piso = app.ColocarAqui();
            Vector3 anclaFija = app.PosicionAncla();

            Vector3 mas = app.DesplazarPiso(1);
            Assert.AreEqual(piso.y + 0.05f, mas.y, TOL, "«Piso +5 cm» sube 5 cm.");
            Assert.AreEqual(piso.x, mas.x, TOL, "Sólo cambia la altura.");
            Assert.AreEqual(piso.z, mas.z, TOL);
            Assert.AreEqual(anclaFija.y, app.PosicionAncla().y, TOL,
                            "El ancla NO se mueve: es ARCore quien la escribe.");
            Assert.AreEqual(0.05f, app.OffsetPiso(), TOL);
            Assert.AreEqual(0.05f, RaizDeDiagramas(app).localPosition.y, TOL,
                            "El desplazamiento se aplica a la raíz visual.");

            Vector3 menos = app.DesplazarPiso(-1);
            Assert.AreEqual(piso.y, menos.y, TOL, "«Piso −5 cm» deshace lo anterior.");
            Assert.AreEqual(piso.x, menos.x, TOL);
            Assert.AreEqual(piso.z, menos.z, TOL);
            Assert.AreEqual(0f, app.OffsetPiso(), TOL);
            Assert.AreEqual(anclaFija.y, app.PosicionAncla().y, TOL,
                            "El ancla sigue donde estaba.");

            Limpiar(app, go);
        }

        /// <summary>
        /// Mover el piso no puede perder el elemento: tras desplazar, el tag
        /// seleccionado sigue siendo el 14 y la escala sigue siendo 1:1.
        /// </summary>
        [Test]
        public void DesplazarElPiso_NoPierdeElElementoNiCambiaSuEscala()
        {
            GameObject go;
            var app = Montar(out go);

            app.ColocarAqui();
            app.DesplazarPiso(1);
            app.DesplazarPiso(1);

            Assert.AreEqual(14, app.TagSeleccionado,
                            "El Plan B no debe cambiar el elemento visible.");
            Assert.AreEqual(0.10f, app.OffsetPiso(), TOL, "Dos pasos son 10 cm.");

            // El diagrama cuelga del ANCLA, no del app: por eso se busca por
            // reflexión y no con transform.Find("Diagramas").
            var raiz = RaizDeDiagramas(app);
            Assert.IsNotNull(raiz, "Debe existir la raíz de diagramas.");
            Assert.AreEqual(Vector3.one, raiz.localScale, "Escala 1:1, siempre.");
            // El desplazamiento va SÓLO en la vertical de la raíz: nada de x ni
            // de z, porque el cuadre del piso no debe mover el elemento de lado.
            Assert.AreEqual(0f, raiz.localPosition.x, TOL);
            Assert.AreEqual(0f, raiz.localPosition.z, TOL);
            Assert.AreEqual(0.10f, raiz.localPosition.y, TOL,
                            "La raíz visual lleva los 10 cm de desplazamiento.");
            Assert.AreEqual(raiz.localPosition.y, app.OffsetPiso(), TOL);

            Limpiar(app, go);
        }

        /// <summary>
        /// El desplazamiento se APLICA al cambiar de tag y al restablecer, no
        /// sólo al pulsar el botón: si no, el cuadre del piso se perdería en
        /// cuanto se eligiera otro elemento.
        /// </summary>
        [Test]
        public void CambiarDeTagYRestablecer_MantienenElDesplazamientoDelPiso()
        {
            GameObject go;
            var app = Montar(out go);

            app.ColocarAqui();
            app.DesplazarPiso(1);

            var campo = typeof(ARInspeccionApp).GetMethod("Seleccionar",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            campo.Invoke(app, new object[] { 134 });
            Assert.AreEqual(0.05f, RaizDeDiagramas(app).localPosition.y, TOL,
                            "Elegir otro elemento no puede perder el cuadre del piso.");

            var restablecer = typeof(ARInspeccionApp).GetMethod("Restablecer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            restablecer.Invoke(app, null);
            Assert.AreEqual(0.05f, RaizDeDiagramas(app).localPosition.y, TOL,
                            "«Restablecer» es sobre el elemento, no sobre el piso.");

            Limpiar(app, go);
        }

        /// <summary>
        /// Retirar el ancla devuelve el desplazamiento a cero: si no, el
        /// siguiente «Colocar aquí» salía corrido el número de pasos pulsados.
        /// </summary>
        [Test]
        public void QuitarAncla_PuestaElDesplazamientoACero()
        {
            GameObject go;
            var app = Montar(out go);

            app.ColocarAqui();
            app.DesplazarPiso(1);
            app.DesplazarPiso(-1);
            app.DesplazarPiso(-1);
            Assert.AreEqual(-0.05f, app.OffsetPiso(), TOL);

            QuitarAncla(app);

            Assert.AreEqual(0f, app.OffsetPiso(), TOL,
                            "Sin ancla no hay piso que cuadrar: offset a cero.");
            Assert.AreEqual(Vector3.zero, app.PosicionAncla());
            Assert.AreEqual(Vector3.zero, app.PosicionPiso());
            Assert.Less(Vector3.Distance(RaizDeDiagramas(app).localPosition, Vector3.zero),
                        TOL, "La raíz vuelve al origen.");

            // Y volver a colocar arranca limpio, con el diagrama intacto: la
            // raíz de diagramas cuelga del ancla, así que si al destruir el
            // ancla no se sacara antes, se perdía entera.
            app.ColocarAqui();
            Assert.AreEqual(0f, app.OffsetPiso(), TOL);
            var raiz = RaizDeDiagramas(app);
            Assert.IsNotNull(raiz, "El diagrama no puede perderse al re-colocar.");
            Assert.IsTrue(raiz.parent != null && raiz.parent.name == "Ancla",
                          "La raíz debe volver a colgar del ancla nuevo.");

            Limpiar(app, go);
        }

        private static void QuitarAncla(ARInspeccionApp app)
        {
            var metodo = typeof(ARInspeccionApp).GetMethod("QuitarAncla",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            metodo.Invoke(app, null);
        }

        private static Transform RaizDeDiagramas(ARInspeccionApp app)
        {
            var campo = typeof(ARInspeccionApp).GetField("raizVisual",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var go = campo.GetValue(app) as GameObject;
            return go != null ? go.transform : null;
        }

        /// <summary>Sin ancla no hay nada que mover, y la app lo dice.</summary>
        [Test]
        public void DesplazarPiso_SinAncla_NoRevienta()
        {
            GameObject go;
            var app = Montar(out go);

            Assert.AreEqual(Vector3.zero, app.PosicionAncla(),
                            "Todavía no se ha colocado nada.");
            Assert.DoesNotThrow(() => app.DesplazarPiso(1));
            Assert.AreEqual(0f, app.OffsetPiso(), TOL,
                            "Sin ancla el desplazamiento no se acumula: sería un error fantasma.");

            Limpiar(app, go);
        }

        /// <summary>
        /// Los tres botones del Plan B existen y están enganchados: sin esto,
        /// «Colocar aquí» sería un botón que no hace nada.
        /// </summary>
        [Test]
        public void LosBotonesDelPlanBDisparanSusAcciones()
        {
            GameObject go;
            var app = Montar(out go);

            var campo = typeof(ARInspeccionApp).GetField("ui",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ui = campo.GetValue(app) as ARInterfaz;

            Assert.IsNotNull(ui.btnColocarAqui, "Falta el botón «Colocar aquí».");
            Assert.IsNotNull(ui.btnPisoMas, "Falta «Piso +5 cm».");
            Assert.IsNotNull(ui.btnPisoMenos, "Falta «Piso −5 cm».");

            float antes = app.Camara.transform.position.y - 1.40f;

            ui.btnColocarAqui.onClick.Invoke();
            Assert.AreEqual(antes, app.PosicionAncla().y, TOL,
                            "El botón «Colocar aquí» debe colocar el ancla estimado.");
            Assert.AreEqual(0f, app.OffsetPiso(), TOL);

            ui.btnPisoMas.onClick.Invoke();
            Assert.AreEqual(antes + 0.05f, app.PosicionPiso().y, TOL,
                            "El botón «Piso +5 cm» debe subir el piso 5 cm.");
            Assert.AreEqual(antes, app.PosicionAncla().y, TOL,
                            "El ancla no se toca: el piso sube por el offset.");

            ui.btnPisoMenos.onClick.Invoke();
            Assert.AreEqual(antes, app.PosicionPiso().y, TOL,
                            "El botón «Piso −5 cm» debe bajar el piso 5 cm.");

            Limpiar(app, go);
        }
    }
}
