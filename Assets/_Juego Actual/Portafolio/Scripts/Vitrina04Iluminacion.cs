using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace UdeM.Portafolio
{
    /// <summary>
    /// Vitrina de iluminacion sobre una copia de un nivel (sin su logica de juego, con su bake).
    /// Cada capa de la iluminacion se enciende y apaga por separado para ver que aporta:
    /// lightmaps horneados, luces en tiempo real, reflection probes, niebla, particulas y haces de
    /// AtmosferaEscena, la luz de personajes y el postproceso. B lo apaga todo y lo va encendiendo
    /// capa por capa (antes / despues). La camara es VueloLibre: se recorre el nivel a mano o con
    /// el recorrido automatico.
    /// Sin lightmaps los objetos horneados pasan a iluminarse como uno dinamico (luces en tiempo
    /// real y ambiente), que es como se verian si no se hubiera horneado.
    /// </summary>
    public class Vitrina04Iluminacion : VitrinaPortafolio
    {
        public VueloLibre camaraLibre;
        [Tooltip("Escenas de iluminacion para pasar de nivel con L (solo en el editor).")]
        public string[] niveles = { };
        [Tooltip("Segundos entre capa y capa en el antes / despues.")]
        [Min(0.2f)] public float pasoAntesDespues = 1.4f;

        struct Horneado
        {
            public Renderer render;
            public int indice;
            public Vector4 escala;
        }

        readonly List<Horneado> horneados = new List<Horneado>();
        readonly List<Light> lucesTiempoReal = new List<Light>();
        readonly List<Volume> volumenes = new List<Volume>();
        readonly List<ReflectionProbe> sondas = new List<ReflectionProbe>();
        AtmosferaEscena atmosfera;
        LuzPersonajes luzPersonajes;

        bool bake = true;
        bool tiempoReal = true;
        bool reflejos = true;
        bool niebla = true;
        bool efectos = true;
        bool personajes = true;
        bool post = true;

        protected override void Preparar()
        {
            Buscar();

            Registrar(TeclaNumero(1), "Iluminacion horneada (lightmaps)", () => Bake(!bake), () => bake);
            Registrar(TeclaNumero(2), "Luces en tiempo real", () => TiempoReal(!tiempoReal), () => tiempoReal);
            Registrar(TeclaNumero(3), "Reflection probes", () => Reflejos(!reflejos), () => reflejos);
            Registrar(TeclaNumero(4), "Niebla", () => Niebla(!niebla), () => niebla);
            if (atmosfera != null)
                Registrar(TeclaNumero(5), "Particulas y haces de luz", () => Efectos(!efectos), () => efectos);
            if (luzPersonajes != null)
                Registrar(TeclaNumero(6), "Luz de personajes", () => Personajes(!personajes), () => personajes);
            Registrar(TeclaNumero(7), "Postproceso", () => Post(!post), () => post);
            Registrar(Key.B, "Antes / despues", () => { StopAllCoroutines(); StartCoroutine(AntesDespues()); });
            if (camaraLibre != null)
            {
                Registrar(Key.Space, "Recorrido automatico", camaraLibre.AlternarRecorrido, () => camaraLibre.recorriendo);
            }
#if UNITY_EDITOR
            if (niveles.Length > 1)
                Registrar(Key.L, "Siguiente nivel", SiguienteNivel);
#endif
        }

        protected override string AyudaCamara =>
            camaraLibre != null ? "Clic der.: mirar   WASD, Q, E: mover   Shift: rapido" : null;

        void Buscar()
        {
            niebla = RenderSettings.fog;
            atmosfera = FindFirstObjectByType<AtmosferaEscena>();
            luzPersonajes = FindFirstObjectByType<LuzPersonajes>();

            foreach (Renderer render in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (render.lightmapIndex >= 0 && render.lightmapIndex < 0xFFFE)
                    horneados.Add(new Horneado { render = render, indice = render.lightmapIndex, escala = render.lightmapScaleOffset });
            }

            foreach (Light luz in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                bool dePersonajes = luzPersonajes != null && luz.transform.IsChildOf(luzPersonajes.transform);
                if (!dePersonajes && luz.enabled && luz.bakingOutput.lightmapBakeType != LightmapBakeType.Baked)
                    lucesTiempoReal.Add(luz);
            }

            // Solo los de este nivel: el filtro de color del menu tiene su propio Volume.
            foreach (Volume volumen in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volumen.gameObject.scene == gameObject.scene && volumen.enabled)
                    volumenes.Add(volumen);
            }

            sondas.AddRange(FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None));
        }

        protected override void Reiniciar()
        {
            StopAllCoroutines();
            Todo(true);
        }

        void Todo(bool encendido)
        {
            Bake(encendido);
            TiempoReal(encendido);
            Reflejos(encendido);
            Niebla(encendido);
            Efectos(encendido);
            Personajes(encendido);
            Post(encendido);
        }

        IEnumerator AntesDespues()
        {
            Todo(false);
            yield return new WaitForSecondsRealtime(pasoAntesDespues * 1.5f);

            System.Action[] capas =
            {
                () => Bake(true),
                () => TiempoReal(true),
                () => Reflejos(true),
                () => Niebla(true),
                () => Efectos(true),
                () => Personajes(true),
                () => Post(true)
            };
            for (int i = 0; i < capas.Length; i++)
            {
                capas[i]();
                yield return new WaitForSecondsRealtime(pasoAntesDespues);
            }
        }

        #region Capas

        void Bake(bool encendido)
        {
            bake = encendido;
            for (int i = 0; i < horneados.Count; i++)
            {
                Horneado h = horneados[i];
                if (h.render == null)
                    continue;
                h.render.lightmapIndex = encendido ? h.indice : -1;
                h.render.lightmapScaleOffset = h.escala;
            }
        }

        void TiempoReal(bool encendido)
        {
            tiempoReal = encendido;
            for (int i = 0; i < lucesTiempoReal.Count; i++)
            {
                if (lucesTiempoReal[i] != null)
                    lucesTiempoReal[i].enabled = encendido;
            }
        }

        void Reflejos(bool encendido)
        {
            reflejos = encendido;
            for (int i = 0; i < sondas.Count; i++)
            {
                if (sondas[i] != null)
                    sondas[i].enabled = encendido;
            }
        }

        void Niebla(bool encendido)
        {
            niebla = encendido;
            RenderSettings.fog = encendido;
        }

        void Efectos(bool encendido)
        {
            efectos = encendido;
            if (atmosfera == null)
                return;

            Activar(atmosfera.objetosParticulas, encendido);
            Activar(atmosfera.objetosVolumetricos, encendido);
        }

        void Personajes(bool encendido)
        {
            personajes = encendido;
            if (luzPersonajes != null)
                luzPersonajes.gameObject.SetActive(encendido);
        }

        void Post(bool encendido)
        {
            post = encendido;
            for (int i = 0; i < volumenes.Count; i++)
            {
                if (volumenes[i] != null)
                    volumenes[i].enabled = encendido;
            }
        }

        static void Activar(GameObject[] objetos, bool encendido)
        {
            if (objetos == null)
                return;
            for (int i = 0; i < objetos.Length; i++)
            {
                if (objetos[i] != null)
                    objetos[i].SetActive(encendido);
            }
        }

        #endregion

#if UNITY_EDITOR
        // Las copias de los niveles no estan en Build Settings: en el editor se cargan por ruta.
        void SiguienteNivel()
        {
            string actual = SceneManager.GetActiveScene().path;
            int indice = System.Array.IndexOf(niveles, actual);
            string siguiente = niveles[(indice + 1) % niveles.Length];
            EditorSceneManager.LoadSceneInPlayMode(siguiente, new LoadSceneParameters(LoadSceneMode.Single));
        }
#endif
    }
}
