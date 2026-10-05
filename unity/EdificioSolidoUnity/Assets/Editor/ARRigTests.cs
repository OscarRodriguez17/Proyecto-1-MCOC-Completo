using NUnit.Framework;
using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.ARFoundation;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Prueba del RIG AR (Corrección 3, A).
    //
    //  El punto de este test es la POSE: con `ARPoseDriver` la cámara se
    //  quedaba en (0,0,0) aunque la sesión arrancara. Ahora la lleva
    //  `TrackedPoseDriver`, y no basta con que el componente esté: hay que
    //  comprobar los tres valores que la mueven en un teléfono con ARCore.
    // =====================================================================

    public class ARRigTests
    {
        [Test]
        public void ARRigConstruyeRigCorrecto()
        {
            GameObject padre = new GameObject("TestPadre");
            var rig = ARRig.Construir(padre.transform);

            // Contar ARSessionOrigin hijos de padre
            int countOrigin = 0;
            ARSessionOrigin foundOrigin = null;
            for (int i = 0; i < padre.transform.childCount; i++)
            {
                var child = padre.transform.GetChild(i);
                var origin = child.GetComponent<ARSessionOrigin>();
                if (origin != null)
                {
                    countOrigin++;
                    foundOrigin = origin;
                }
            }
            Assert.AreEqual(1, countOrigin, "Debe haber exactamente 1 ARSessionOrigin");
            Assert.IsNotNull(foundOrigin);
            Assert.AreEqual(rig.origen, foundOrigin);

            // origen.camera == camara
            Assert.AreEqual(rig.origen.camera, rig.camara);

            // cámara tiene componentes
            Assert.IsNotNull(rig.camara.GetComponent<ARCameraManager>());
            Assert.IsNotNull(rig.camara.GetComponent<ARCameraBackground>());
            Assert.IsNotNull(rig.camara.GetComponent<TrackedPoseDriver>());

            // componentes en mismo GO que ARSessionOrigin
            Assert.IsNotNull(rig.origen.GetComponent<ARRaycastManager>());
            Assert.IsNotNull(rig.origen.GetComponent<ARAnchorManager>());
            Assert.IsNotNull(rig.origen.GetComponent<ARPlaneManager>());

            // cámara hija del origen
            Assert.AreEqual(rig.origen.transform, rig.camara.transform.parent);
            // ARInputManager en GO de ARSession
            Assert.IsNotNull(rig.sesion.GetComponent<ARInputManager>());

            var poseDriver = rig.camara.GetComponent<TrackedPoseDriver>();
            Assert.IsNotNull(poseDriver);
            Assert.IsTrue(poseDriver.enabled);

            Object.DestroyImmediate(padre);
        }

        /// <summary>
        /// Los tres ajustes que hacen que la cámara escriba la pose. Sin ellos
        /// el síntoma es exactamente el reportado: sesión viva, pose en 0,0,0.
        /// </summary>
        [Test]
        public void CamaraUsaTrackedPoseDriverConGenericXRDeviceYColorCamera()
        {
            GameObject padre = new GameObject("TestPadre");
            var rig = ARRig.Construir(padre.transform);

            // El componente viejo, responsable de la pose a (0,0,0), no debe
            // seguir en la cámara.
            Assert.IsNull(rig.camara.GetComponent<ARPoseDriver>(),
                          "ARPoseDriver está obsoleto desde AR Foundation 2.0 y "
                          + "dejaba la pose en (0,0,0).");

            var pose = rig.camara.GetComponent<TrackedPoseDriver>();
            Assert.IsNotNull(pose, "La pose la debe llevar TrackedPoseDriver.");
            Assert.AreEqual(TrackedPoseDriver.DeviceType.GenericXRDevice, pose.deviceType,
                            "GenericXRDevice es lo que declara un móvil.");
            Assert.AreEqual(TrackedPoseDriver.TrackedPose.ColorCamera, pose.poseSource,
                            "ColorCamera es la pose de la cámara trasera.");
            Assert.AreEqual(TrackedPoseDriver.UpdateType.UpdateAndBeforeRender, pose.updateType,
                            "Sin UpdateAndBeforeRender la imagen tiembla.");
            Assert.AreEqual(TrackedPoseDriver.TrackingType.RotationAndPosition,
                            pose.trackingType,
                            "Con RotationOnly la cámara se queda clavada en el origen.");

            Object.DestroyImmediate(padre);
        }

        /// <summary>
        /// `ConfigurarPose` es lo que se puede verificar sin sesión AR: se le
        /// pasa un TrackedPoseDriver cualquiera y deben quedar los tres valores.
        /// </summary>
        [Test]
        public void ConfigurarPoseAplicaLosTresValores()
        {
            var go = new GameObject("Camara");
            var pose = go.AddComponent<TrackedPoseDriver>();

            // Valores contrarios, para que un no-op sea detectable.
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
            pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            Assert.IsTrue(ARRig.ConfigurarPose(pose));

            Assert.AreEqual(TrackedPoseDriver.TrackingType.RotationAndPosition,
                            pose.trackingType);
            Assert.AreEqual(TrackedPoseDriver.UpdateType.UpdateAndBeforeRender,
                            pose.updateType);
            Assert.AreEqual(TrackedPoseDriver.DeviceType.GenericXRDevice,
                            pose.deviceType);
            Assert.AreEqual(TrackedPoseDriver.TrackedPose.ColorCamera,
                            pose.poseSource);

            Assert.IsFalse(ARRig.ConfigurarPose(null),
                           "Sin driver no se puede configurar la pose.");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Sólo queda una cámara habilitada: con dos, `Camera.main` puede ser la
        /// equivocada y el texto 3D se orienta hacia un punto que no se ve.
        ///
        /// Quien lo hace es `ARInspeccionApp` (su `DestroyOtherCameras`), no
        /// `ARRig`: el rig sólo añade su cámara. Por eso el test pasa por la
        /// app, que es el sitio donde vive esa decisión.
        /// </summary>
        [Test]
        public void LaAppDejaUnaSolaCamaraActiva()
        {
            var intrusa = new GameObject("CamaraIntrusa");
            intrusa.AddComponent<Camera>();

            GameObject go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();

            int habilitadas = 0;
            foreach (var c in Object.FindObjectsOfType<Camera>(true))
                if (c.enabled) habilitadas++;
            Assert.AreEqual(1, habilitadas);

            // Y la que queda es la del rig AR.
            Assert.IsTrue(app.Camara.enabled);

            Object.DestroyImmediate(intrusa);
            var campo = typeof(ARInspeccionApp).GetField("ui",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ui = campo.GetValue(app) as ARInterfaz;
            if (ui != null && ui.root != null) Object.DestroyImmediate(ui.root);
            Object.DestroyImmediate(go);
        }
    }
}