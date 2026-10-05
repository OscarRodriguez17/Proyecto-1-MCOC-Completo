using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace MCOC.EditorTools
{
    // =====================================================================
    //  Configuración de XR para Android (Corrección 3).
    //
    //  API de XR Management 4.4.0, que es la instalada. Dos cosas que
    //  confunden y que aquí están resueltas:
    //
    //  · `XRGeneralSettingsPerBuildTarget` (namespace `UnityEditor.XR.Management`,
    //    assembly de Editor) es el asset MAESTRO. Los `XRGeneralSettings` de
    //    cada BuildTargetGroup son SUB-ASSETS dentro de él, no assets sueltos.
    //  · `XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(g)`
    //    devuelve el `XRGeneralSettings` de ese grupo, NO el maestro.
    //
    //  Crear un `XRGeneralSettings.asset` suelto dispara el postprocesador
    //  `XRGeneralSettingsUpgrade` de 4.4.0, que intenta migrarlo y revienta
    //  con ArgumentNullException si el Manager aún es null. Por eso aquí se
    //  crea directamente el asset maestro y no hay asset suelto que migrar.
    // =====================================================================

    public static class MCOCXRSetup
    {
        private const string Menu = "Tools/MCOC/Configurar AR";
        private const string LoaderArcore = "ARCoreLoader";
        private const string XRSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";

        [MenuItem(Menu)]
        public static void Configurar()
        {
            bool ok = ActivarArcoreAndroid();
            ConfigurarAndroid();
            AssetDatabase.SaveAssets();
            if (ok)
                Debug.Log("[MCOC] AR listo: loader ARCoreLoader + Initialize XR on Startup para Android.");
            else
                Debug.LogWarning("[MCOC] ARCoreLoader no se pudo activar. Revisa que el paquete com.unity.xr.arcore está instalado en Packages/manifest.json.");
        }

        /// <summary>
        /// Asset maestro de XR, creado si no existe. Se registra en
        /// EditorBuildSettings con la clave `k_SettingsKey`, que es lo que XR
        /// lee al arrancar.
        /// </summary>
        public static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTarget()
        {
            var maestro = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XRSettingsPath);

            if (maestro == null)
                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                                                      out maestro);

            if (maestro == null)
            {
                var dir = Path.GetDirectoryName(XRSettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                maestro = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                maestro.name = "XRGeneralSettingsPerBuildTarget";
                AssetDatabase.CreateAsset(maestro, XRSettingsPath);
                AssetDatabase.SaveAssets();
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, maestro, true);
            return maestro;
        }

        /// <summary>
        /// `XRGeneralSettings` de un BuildTargetGroup, con su `XRManagerSettings`,
        /// creándolos como sub-assets del maestro si no estaban.
        /// </summary>
        public static XRGeneralSettings GetOrCreateXRGeneralSettings(BuildTargetGroup grupo)
        {
            var maestro = GetOrCreatePerBuildTarget();

            XRGeneralSettings settings = maestro.SettingsForBuildTarget(grupo);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                settings.name = grupo + " Settings";
                AssetDatabase.AddObjectToAsset(settings, XRSettingsPath);
                AssetDatabase.SaveAssets();
                maestro.SetSettingsForBuildTarget(grupo, settings);
            }

            if (settings.Manager == null)
            {
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = grupo + " Providers";
                AssetDatabase.AddObjectToAsset(manager, XRSettingsPath);
                AssetDatabase.SaveAssets();
                settings.Manager = manager;
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(maestro);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// `XRManagerSettings.loaders` está obsoleto en 4.x; la lista buena es
        /// `activeLoaders`.
        /// </summary>
        public static bool LoaderAsignado(XRManagerSettings manager, string tipo)
        {
            if (manager == null) return false;
            var loaders = manager.activeLoaders;
            if (loaders == null) return false;
            for (int i = 0; i < loaders.Count; i++)
                if (loaders[i] != null && loaders[i].GetType().Name == tipo)
                    return true;
            return false;
        }

        public static bool TieneLoaderARCore(BuildTargetGroup grupo)
        {
            try
            {
                var maestro = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XRSettingsPath);
                if (maestro == null)
                    EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out maestro);
                if (maestro == null) return false;
                var settings = maestro.SettingsForBuildTarget(grupo);
                if (settings == null || settings.Manager == null) return false;
                return LoaderAsignado(settings.Manager, LoaderArcore);
            }
            catch { return false; }
        }

        /// <summary>
        /// Paso real de configuración de XR: crea los settings de Android,
        /// activa `InitManagerOnStart` y registra ARCoreLoader.
        ///
        /// Es público porque el test de EditMode tiene que recorrer ESTE camino,
        /// no uno paralelo: si el test sólo creara los settings sin asignar el
        /// loader, comprobaría algo que nadie ejecuta.
        /// </summary>
        public static bool ActivarArcoreAndroid()
        {
            var grupo = BuildTargetGroup.Android;
            try
            {
                XRGeneralSettings settings = GetOrCreateXRGeneralSettings(grupo);
                settings.InitManagerOnStart = true;
                XRManagerSettings manager = settings.Manager;
                if (manager == null) return false;

                bool asignado = XRPackageMetadataStore.AssignLoader(manager, LoaderArcore, grupo);
                if (!asignado) Debug.LogWarning("[MCOC] XRPackageMetadataStore.AssignLoader devolvió false.");
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(manager);
                AssetDatabase.SaveAssets();
                return asignado;
            }
            catch (Exception ex) { Debug.LogWarning("[MCOC] ActivarArcoreAndroid: " + ex.Message); return false; }
        }

        public static void ConfigurarAndroid()
        {
            var g = BuildTargetGroup.Android;
            PlayerSettings.SetApplicationIdentifier(g, "com.mcoc.edificiocomplejo.ar");
            PlayerSettings.SetScriptingBackend(g, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.blitType = AndroidBlitType.Never;
            PlayerSettings.Android.optimizedFramePacing = false;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        [MenuItem(Menu + "/Verificar paquete")]
        public static void VerificarPaquete()
        {
            bool hay = false;
            try
            {
                var settings = GetOrCreateXRGeneralSettings(BuildTargetGroup.Android);
                hay = XRPackageMetadataStore.AssignLoader(settings.Manager, LoaderArcore,
                                                          BuildTargetGroup.Android);
                EditorUtility.SetDirty(settings.Manager);
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MCOC] VerificarPaquete: " + ex.Message);
            }
            Debug.Log(hay ? "[MCOC] com.unity.xr.arcore disponible y loader registrado."
                          : "[MCOC] com.unity.xr.arcore NO disponible: revisa Packages/manifest.json.");
        }
    }
}