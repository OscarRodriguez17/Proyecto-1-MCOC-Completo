using NUnit.Framework;
using MCOC.AR;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Contador de frames de cámara (Corrección 3, C).
    //
    //  Existía porque con la cámara clavada en (0,0,0) no se distinguía
    //  «la sesión AR no mueve la pose» de «la app no dibuja». El contador va
    //  por `Application.onBeforeRender`: si avanza con la pose en 0,0,0, el
    //  problema es el rig; si no avanza, la app ni siquiera dibuja.
    //
    //  `Contar` es público para poder avanzarlo a mano aquí, sin esperar
    //  frames reales ni sesión AR.
    // =====================================================================

    public class ARFramesCamaraTests
    {
        [SetUp]
        public void AntesDeCadaTest()
        {
            ARFramesCamara.Desactivar();
            ARFramesCamara.Reiniciar();
        }

        [TearDown]
        public void DespuesDeCadaTest()
        {
            ARFramesCamara.Desactivar();
            ARFramesCamara.Reiniciar();
        }

        [Test]
        public void EmpiezaACero()
        {
            Assert.AreEqual(0L, ARFramesCamara.Total);
            Assert.IsFalse(ARFramesCamara.Activo);
        }

        [Test]
        public void CuentaUnFramePorLlamada()
        {
            ARFramesCamara.Contar();
            Assert.AreEqual(1L, ARFramesCamara.Total);
            ARFramesCamara.Contar();
            ARFramesCamara.Contar();
            Assert.AreEqual(3L, ARFramesCamara.Total);
        }

        /// <summary>
        /// Activar es idempotente. Si `Awake` y `Preparar` lo activaran los
        /// dos, el callback quedaría suscrito dos veces y el contador
        /// advancedría al doble de los frames: un diagnóstico que miente.
        /// </summary>
        [Test]
        public void ActivarEsIdempotente_NoDuplicaLaSuscripcion()
        {
            ARFramesCamara.Activar();
            Assert.IsTrue(ARFramesCamara.Activo);

            // Diez activaciones seguidas y un solo frame: tiene que sumar 1.
            for (int i = 0; i < 10; i++) ARFramesCamara.Activar();
            ARFramesCamara.Contar();

            Assert.AreEqual(1L, ARFramesCamara.Total,
                            "La suscripción a onBeforeRender no puede duplicarse.");
        }

        [Test]
        public void ReiniciarVuelveACeroSinTocarLaSuscripcion()
        {
            ARFramesCamara.Activar();
            ARFramesCamara.Contar();
            ARFramesCamara.Contar();
            Assert.AreEqual(2L, ARFramesCamara.Total);

            ARFramesCamara.Reiniciar();

            Assert.AreEqual(0L, ARFramesCamara.Total);
            Assert.IsTrue(ARFramesCamara.Activo, "Reiniciar no desactiva el contador.");

            ARFramesCamara.Contar();
            Assert.AreEqual(1L, ARFramesCamara.Total);
        }

        [Test]
        public void DesactivarEsIdempotente()
        {
            ARFramesCamara.Activar();
            ARFramesCamara.Desactivar();
            ARFramesCamara.Desactivar();
            Assert.IsFalse(ARFramesCamara.Activo);
        }

        /// <summary>El texto en pantalla nombra el contador y el número.</summary>
        [Test]
        public void ElTextoMuestraElTotal()
        {
            ARFramesCamara.Reiniciar();
            Assert.AreEqual("Frames cámara: 0", ARFramesCamara.Texto());

            ARFramesCamara.Contar();
            Assert.AreEqual("Frames cámara: 1", ARFramesCamara.Texto());

            for (int i = 0; i < 1234; i++) ARFramesCamara.Contar();
            Assert.IsTrue(ARFramesCamara.Texto().Contains("1,235"),
                          "El total se agrupa con separador de miles: "
                          + ARFramesCamara.Texto());
        }
    }
}