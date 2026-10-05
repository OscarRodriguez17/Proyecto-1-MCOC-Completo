using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MCOC.AR
{
    /// <summary>
    /// Rig AR construido por código (Corrección 3, A).
    ///
    /// Cambio con respecto a la versión anterior: la pose de la cámara la lleva
    /// <see cref="TrackedPoseDriver"/> (com.unity.xr.legacyinputhelpers) y NO
    /// <c>ARPoseDriver</c>. <c>ARPoseDriver</c> está obsoleto desde AR
    /// Foundation 2.0 y, con ARCore + `UpdateAndBeforeRender`, dejaba la
    /// cámara en (0,0,0): <c>ARInputManager</c> sí estaba, la sesión sí
    /// arrancaba, pero nada escribía la pose en el transform de la cámara.
    ///
    /// La combinación que sí mueve la cámara en un teléfono con ARCore es
    /// exactamente la de los ejemplos de AR Foundation:
    ///
    ///   SetPoseSource(DeviceType.GenericXRDevice, TrackedPose.ColorCamera)
    ///   updateType   = UpdateAndBeforeRender
    ///   trackingType = RotationAndPosition
    ///
    /// - `GenericXRDevice` + `ColorCamera` es la pareja que declara el
    ///   dispositivo móvil (no un HMD ni un mando).
    /// - `UpdateAndBeforeRender` muestrea la pose dos veces por frame, lo que
    ///   quita el temblor de la imagen de fondo y del contenido superpuesto.
    /// - `RotationAndPosition` (y no `RotationOnly`) es imprescindible: con
    ///   sólo rotación el transform de la cámara se queda clavado en el
    ///   origen, que es exactamente el síntoma reportado.
    /// </summary>
    public class ARRig
    {
        public struct RigAR
        {
            public ARSession sesion;
            public ARSessionOrigin origen;
            public Camera camara;
            public ARRaycastManager raycastMgr;
            public ARAnchorManager anchorMgr;
            public ARPlaneManager planeMgr;
            public TrackedPoseDriver pose;
        }

        /// <summary>
        /// Aplica a la cámara los tres ajustes de pose. Se deja público y
        /// estático para que <see cref="ARRigTests"/> pueda comprobar sobre un
        /// `TrackedPoseDriver` cualquiera —sin sesión AR— que los valores son
        /// los que hacen falta en el teléfono.
        /// </summary>
        public static bool ConfigurarPose(TrackedPoseDriver pose)
        {
            if (pose == null) return false;
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            // SetPoseSource devuelve false si la pareja no es válida; se
            // ignora su valor y se leen después deviceType/poseSource, que son
            // las propiedades que el test verifica.
            pose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice,
                               TrackedPoseDriver.TrackedPose.ColorCamera);
            return pose.deviceType == TrackedPoseDriver.DeviceType.GenericXRDevice
                && pose.poseSource == TrackedPoseDriver.TrackedPose.ColorCamera;
        }

        public static RigAR Construir(Transform padre)
        {
            RigAR rig = new RigAR();

            GameObject sesionGO = new GameObject("AR Session");
            sesionGO.transform.SetParent(padre, false);
            rig.sesion = sesionGO.AddComponent<ARSession>();
            sesionGO.AddComponent<ARInputManager>();

            GameObject origenGO = new GameObject("AR Session Origin");
            origenGO.transform.SetParent(padre, false);
            rig.origen = origenGO.AddComponent<ARSessionOrigin>();
            rig.planeMgr = origenGO.AddComponent<ARPlaneManager>();
            rig.planeMgr.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            rig.raycastMgr = origenGO.AddComponent<ARRaycastManager>();
            rig.anchorMgr = origenGO.AddComponent<ARAnchorManager>();

            GameObject camaraGO = new GameObject("AR Camera");
            camaraGO.transform.SetParent(origenGO.transform, false);
            camaraGO.tag = "MainCamera";
            Camera camara = camaraGO.AddComponent<Camera>();
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = Color.black;
            camara.nearClipPlane = 0.05f;
            camara.farClipPlane = 200f;
            rig.camara = camara;

            camaraGO.AddComponent<ARCameraManager>();
            camaraGO.AddComponent<ARCameraBackground>();

            // Éste es el que mueve la cámara (antes era ARPoseDriver).
            rig.pose = camaraGO.AddComponent<TrackedPoseDriver>();
            ConfigurarPose(rig.pose);

            rig.origen.camera = rig.camara;

            sesionGO.SetActive(true);
            origenGO.SetActive(true);
            camaraGO.SetActive(true);

            return rig;
        }
    }
}
