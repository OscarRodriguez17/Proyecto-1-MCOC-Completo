using NUnit.Framework;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;
using MCOC.EditorTools;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  XR de Android (Corrección 3).
    //
    //  Con XR Management 4.4.0 el asset maestro es
    //  `XRGeneralSettingsPerBuildTarget` (assembly de Editor) y los
    //  `XRGeneralSettings` de cada BuildTargetGroup son sub-assets dentro de
    //  él. Estos tests se escriben contra esa API real.
    // =====================================================================

    public class XRAndroidConfigTests
    {
        /// <summary>
        /// Se ejecuta el camino REAL de configuración (el mismo que el menú
        /// Tools/MCOC/Configurar AR) y luego se comprueba lo que dejó.
        /// Comprobar los settings sin pasar por AssignLoader no probaría nada:
        /// el loader es justo lo que hay que verificar.
        /// </summary>
        [OneTimeSetUp]
        public void ConfigurarXR()
        {
            Assert.IsTrue(MCOCXRSetup.ActivarArcoreAndroid(),
                          "No se pudo registrar ARCoreLoader para Android. "
                          + "Revisa que com.unity.xr.arcore esté en Packages/manifest.json.");
        }

        [Test]
        public void XRGeneralSettingsAndroidExiste_InitOnStartTrue_YTieneARCoreLoader()
        {
            var grupo = BuildTargetGroup.Android;
            XRGeneralSettings settings = MCOCXRSetup.GetOrCreateXRGeneralSettings(grupo);

            Assert.IsNotNull(settings, "XRGeneralSettings de Android debe existir");
            Assert.IsTrue(settings.InitManagerOnStart,
                          "InitManagerOnStart debe ser true para que AR arranque solo");
            Assert.IsNotNull(settings.Manager, "XRManagerSettings de Android debe existir");

            Assert.IsTrue(MCOCXRSetup.LoaderAsignado(settings.Manager, "ARCoreLoader"),
                          "El Manager debe contener un loader de tipo ARCoreLoader para Android");
        }

        [Test]
        public void ElMaestroQuedaRegistradoComoConfigObject()
        {
            MCOCXRSetup.GetOrCreatePerBuildTarget();

            bool ok = EditorBuildSettings.TryGetConfigObject(
                XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget maestro);

            Assert.IsTrue(ok,
                          "XR sólo encuentra los settings si están registrados en "
                          + "EditorBuildSettings con la clave k_SettingsKey.");
            Assert.IsNotNull(maestro);
            Assert.IsTrue(maestro.HasSettingsForBuildTarget(BuildTargetGroup.Android));
        }

        [Test]
        public void AndroidUsaIL2CPP_ARM64_YMinSdk24()
        {
            MCOCXRSetup.ConfigurarAndroid();

            Assert.AreEqual(ScriptingImplementation.IL2CPP,
                            PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android));
            Assert.AreEqual(AndroidArchitecture.ARM64,
                            PlayerSettings.Android.targetArchitectures);
            Assert.AreEqual(AndroidSdkVersions.AndroidApiLevel24,
                            PlayerSettings.Android.minSdkVersion);
        }
    }
}