using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MCOC.EditorTools
{
    /// <summary>
    /// Builds Android de la escena AR y del visor sólido (Semana 05/06).
    ///
    ///   Tools/MCOC/Build Android AR     -> build/EdificioComplejo_MCOC_AR.apk
    ///       Escenas: AR_Inspeccion.unity (index 0, obligatory) + Main.unity
    ///       Es la que arranca en la cámara; Main queda como escena secundaria.
    ///
    ///   Tools/MCOC/Build Android visor  -> build/EdificioComplejo_MCOC.apk
    ///       Escenas: Main.unity (única). Sin sesión AR.
    ///
    /// NO usa batchmode: se compila interactivamente desde el editor, con los
    /// datos ya presentes en Assets/StreamingAssets (edificio_completo.json,
    /// edificio_solido.json y ar_elementos.json).
    ///
    /// Ajustes Android (hoja de prensa del APK):
    ///   - Identifier : com.mcoc.edificiocomplejo.ar  (AR)
    ///                  com.mcoc.edificiocomplejo     (visor)
    ///   - Min SDK    : 24 — lo exige ARCore
    ///   - Scripting  : IL2CPP, ARM64
    ///   - Gráficos   : OpenGLES3
    ///   - Orientación: ambas horizontales (la vista en el móvil es AR)
    /// </summary>
    public static class MCOCBuildAndroid
    {
        private const string MenuAr = "Tools/MCOC/Build Android AR";
        private const string MenuVisor = "Tools/MCOC/Build Android visor";

        private const string EscenaAr = "Assets/Scenes/AR_Inspeccion.unity";
        private const string EscenaVisor = "Assets/Scenes/Main.unity";

        private const string PaqueteAr = "com.mcoc.edificiocomplejo.ar";
        private const string PaqueteVisor = "com.mcoc.edificiocomplejo";
        private const string Version = "1.0";

        [MenuItem(MenuAr)]
        public static void BuildAr()
        {
            if (!File.Exists(EscenaAr))
            {
                Fallar("Falta la escena AR.\n" +
                       "Crea Assets/Scenes/AR_Inspeccion.unity con un GameObject " +
                       "que tenga el componente MCOC.AR.ARInspeccionApp.");
                return;
            }

            // El loader tiene que estar asignado para Android. Si no lo está,
            // se intenta registrar aquí: así el build es reproducible también
            // en batchmode, sin depender de que alguien haya pasado antes por
            // el menú de XR Plug-in Management.
            if (!MCOCXRSetup.TieneLoaderARCore(BuildTargetGroup.Android))
                MCOCXRSetup.ActivarArcoreAndroid();

            if (!MCOCXRSetup.TieneLoaderARCore(BuildTargetGroup.Android))
            {
                Fallar("El loader ARCore no queda asignado para Android. "
                       + "Revisa que com.unity.xr.arcore esté en Packages/manifest.json "
                       + "y ejecuta Tools/MCOC/Configurar AR.");
                return;
            }

            // La escena AR tiene que ser la index 0: es la que Unity carga
            // al arrancar, y sin cámara el resto no tiene sentido.
            Compilar(new[] { EscenaAr, EscenaVisor }, "EdificioComplejo_MCOC_AR.apk",
                     PaqueteAr, true);
        }

        [MenuItem(MenuVisor)]
        public static void BuildVisor()
        {
            if (!File.Exists(EscenaVisor))
            {
                Fallar("Falta la escena Assets/Scenes/Main.unity.\n" +
                       "Ejecuta primero 'Tools/MCOC/Preparar escena Main'.");
                return;
            }
            Compilar(new[] { EscenaVisor }, "EdificioComplejo_MCOC.apk", PaqueteVisor, false);
        }

        // -----------------------------------------------------------------

        private static void Compilar(string[] escenas, string nombreApk,
                                     string paquete, bool esAr)
        {
            PrepararPlayerSettings(paquete);

            // Guarda los cambios de la escena que esté abierta.
            if (EditorSceneManager.GetActiveScene().path == escenas[0])
                EditorSceneManager.SaveOpenScenes();

            var lista = new List<EditorBuildSettingsScene>();
            foreach (var e in escenas) lista.Add(new EditorBuildSettingsScene(e, true));
            EditorBuildSettings.scenes = lista.ToArray();

            string raiz = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dir = Path.Combine(raiz, "build");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, nombreApk);

            var report = BuildPipeline.BuildPlayer(
                escenas, apk, BuildTarget.Android, BuildOptions.None);

            if (report.summary.result == BuildResult.Succeeded)
            {
                long bytes = 0;
                try { bytes = new FileInfo(apk).Length; } catch { }
                Debug.Log($"[MCOC] APK: {apk}  (paquete {paquete}, " +
                          $"{escenas.Length} escena(s), minSdk 24, IL2CPP/ARM64, GLES3" +
                          (esAr ? ", escena AR index 0" : "") +
                          $", {bytes / 1024.0 / 1024.0:F2} MB).");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(apk);
            }
            else
            {
                Debug.LogError("[MCOC] Falló el build Android. Ver BuildReport en Console.");
                if (!Application.isBatchMode)
                    Dialog("Falló la compilación. Revisa la Consola (BuildReport).");
            }
        }

        /// <summary>
        /// Única fuente de verdad de los ajustes Android. Se llama
        /// <see cref="MCOCXRSetup.ConfigurarAndroid"/> para que el build y la
        /// configuración AR no puedan divergir.
        /// </summary>
        private static void PrepararPlayerSettings(string paquete)
        {
            var g = BuildTargetGroup.Android;
            MCOCXRSetup.ConfigurarAndroid();

            PlayerSettings.SetApplicationIdentifier(g, paquete);
            PlayerSettings.bundleVersion = Version;
        }

        private static void Dialog(string msg)
        {
            EditorUtility.DisplayDialog("MCOC Build Android", msg, "Aceptar");
        }

        /// <summary>
        /// Avisa de un problema previo al build. En batchmode el diálogo no se
        /// ve (y puede dejar Unity colgado), así que también va al log.
        /// </summary>
        private static void Fallar(string msg)
        {
            Debug.LogError("[MCOC] " + msg);
            if (!Application.isBatchMode) Dialog(msg);
        }
    }
}
