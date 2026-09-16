using UnityEngine;

namespace MCOC.Unity
{
    public class OrbitCamera : MonoBehaviour
    {
        public Transform objetivo;
        public float distancia = 190f;
        public float rotVel = 0.25f;
        public float panVel = 0.35f;
        public float zoomVel = 60f;
        public float minDist = 2f;
        public float maxDist = 1200f;
        public float suavidad = 10f;
        public float velocidadVuelo = 0.8f;
        public bool mostrarPanel = true;

        [Header("Presets")]
        public float pitchPerspectiva = 30f;
        public float yawPerspectiva = 45f;
        public float pitchPlanta = 89.5f;
        public float pitchFrente = 6f;

        private Vector3 angulos = new Vector3(30f, 45f, 0f);
        private Vector3 centro = Vector3.zero;
        private UnityStickModel modelo;
        private bool inicio = true;

        private void Start()
        {
            angulos.Set(pitchPerspectiva, yawPerspectiva, 0f);
            modelo = Object.FindObjectOfType<UnityStickModel>();
            Snap();
        }

        private void LateUpdate()
        {
            ProcesarEntrada();
            AplicarCamera(false);
        }

        private void ProcesarEntrada()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) EnfocarBloque(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) EnfocarBloque(1);
            if (Input.GetKeyDown(KeyCode.V)) VistaPlanta();
            if (Input.GetKeyDown(KeyCode.F)) VistaFrente();
            if (Input.GetKeyDown(KeyCode.C)) VistaPerspectiva();
            if (Input.GetKeyDown(KeyCode.R)) Reset();

            Vector3 fwd = Quaternion.Euler(0f, angulos.y, 0f) * Vector3.forward;
            Vector3 der = Quaternion.Euler(0f, angulos.y, 0f) * Vector3.right;

            bool orbita = Input.GetMouseButton(1)
                          || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(0));
            if (orbita)
            {
                angulos.x += Input.GetAxis("Mouse Y") * rotVel;
                angulos.y -= Input.GetAxis("Mouse X") * rotVel;
                angulos.x = Mathf.Clamp(angulos.x, -89.5f, 89.5f);
            }

            if (Input.GetMouseButton(2))
            {
                float fac = distancia * 0.0012f;
                centro -= der * Input.GetAxis("Mouse X") * fac;
                centro += Vector3.up * Input.GetAxis("Mouse Y") * fac;
            }

            float rueda = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(rueda) > 0.001f)
            {
                distancia = Mathf.Clamp(distancia - rueda * zoomVel, minDist, maxDist);
            }

            float v = distancia * velocidadVuelo * Time.deltaTime;
            if (Input.GetKey(KeyCode.W)) centro += fwd * v;
            if (Input.GetKey(KeyCode.S)) centro -= fwd * v;
            if (Input.GetKey(KeyCode.D)) centro += der * v;
            if (Input.GetKey(KeyCode.A)) centro -= der * v;
            if (Input.GetKey(KeyCode.Q)) centro -= Vector3.up * v;
            if (Input.GetKey(KeyCode.E)) centro += Vector3.up * v;
        }

        private void AplicarCamera(bool snap)
        {
            Quaternion rot = Quaternion.Euler(angulos.x, angulos.y, 0f);
            Vector3 pos = centro + rot * (Vector3.back * distancia);
            Quaternion mirar = Quaternion.LookRotation(centro - pos, Vector3.up);
            if (snap || inicio)
            {
                transform.position = pos;
                transform.rotation = mirar;
                inicio = false;
            }
            else
            {
                float k = 1f - Mathf.Exp(-suavidad * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, pos, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, mirar, k);
            }
        }

        public void Snap() => AplicarCamera(true);

        public void EnfocarBloque(int idx)
        {
            if (modelo == null) modelo = Object.FindObjectOfType<UnityStickModel>();
            if (modelo == null) return;
            centro = modelo.CentroBloque(idx);
            float span = modelo.SpanBloque(idx);
            if (distancia < span * 1.6f) distancia = span * 1.6f;
            Snap();
        }

        public void VistaPlanta()
        {
            angulos.x = pitchPlanta;
        }

        public void VistaFrente()
        {
            angulos.x = pitchFrente;
            angulos.y = yawPerspectiva;
            if (distancia < 120f) distancia = 120f;
        }

        public void VistaPerspectiva()
        {
            angulos.x = pitchPerspectiva;
            angulos.y = yawPerspectiva;
        }

        public void Reset()
        {
            angulos.Set(pitchPerspectiva, yawPerspectiva, 0f);
            distancia = Mathf.Max(distancia, 190f);
            centro = Vector3.zero;
            Snap();
        }

        private Rect rectPanel = new Rect(Screen.width - 250, 16, 230, 150);

        private void OnGUI()
        {
            if (!mostrarPanel) return;
            rectPanel = GUILayout.Window(40321, rectPanel, DibujarPanel, "Camara");
        }

        private void DibujarPanel(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Bot. derecho: rotar | rueda: zoom");
            GUILayout.Label("Medio: pan | W/S/A/D: avanzar");
            GUILayout.Label("Q/E: subir/bajar | V: arriba | F: frente");
            GUILayout.Label("1/2: enfocar bloque 1/2");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Arriba")) VistaPlanta();
            if (GUILayout.Button("Frente")) VistaFrente();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enfocar B1")) EnfocarBloque(0);
            if (GUILayout.Button("Enfocar B2")) EnfocarBloque(1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}