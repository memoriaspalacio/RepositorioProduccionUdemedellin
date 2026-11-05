using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Vitrina del arte del equipo: una galeria con el arte 2D y la interfaz, las texturas pintadas,
    /// los props sacados de los modelos de los niveles y los personajes modelados.
    ///   - 1 a 4 llevan la camara a cada sala; Q y E pasan de pieza en pieza dentro de la sala.
    ///   - C cambia los modelos a vista de arcilla (un material neutro) para ver solo el modelado.
    ///   - Doble clic sobre cualquier pieza (cuadro, prop o personaje) la enfoca.
    /// </summary>
    public class Vitrina07Arte : VitrinaPortafolio
    {
        [Serializable]
        public class Sala
        {
            public string nombre = "Sala";
            [TextArea] public string descripcion = "";
            public Transform centro;
            [Min(1f)] public float distancia = 10f;
            public float altura = 2f;
            [Tooltip("Sus modelos cambian a arcilla con la tecla C.")]
            public bool modelos;
            public List<Transform> piezas = new List<Transform>();
        }

        public List<Sala> salas = new List<Sala>();
        [Tooltip("Material neutro para la vista de arcilla.")]
        public Material materialArcilla;

        int sala = -1;
        int pieza = -1;
        bool arcilla;
        float ultimoClic = -1f;
        readonly Dictionary<Renderer, Material[]> originales = new Dictionary<Renderer, Material[]>();

        protected override void Preparar()
        {
            for (int i = 0; i < salas.Count; i++)
            {
                int indice = i;
                Registrar(TeclaNumero(i + 1), "Sala: " + salas[i].nombre, () => IrASala(indice), () => sala == indice);
            }
            Registrar(Key.Q, "Pieza anterior", () => CambiarPieza(-1));
            Registrar(Key.E, "Pieza siguiente", () => CambiarPieza(1));
            Registrar(Key.C, "Vista de arcilla (modelado)", AlternarArcilla, () => arcilla);

            if (salas.Count > 0)
                IrASala(0);
        }

        protected override void Reiniciar()
        {
            if (arcilla)
                AlternarArcilla();
            IrASala(0);
        }

        protected override void PuntosDeFoco(List<Foco> lista)
        {
            foreach (Sala s in salas)
            {
                foreach (Transform p in s.piezas)
                {
                    if (p != null)
                        lista.Add(FocoDe(p.name, p.gameObject, 1.2f));
                }
            }
        }

        public override List<Key> AccionesMiniatura => new List<Key>();

        void IrASala(int indice)
        {
            if (indice < 0 || indice >= salas.Count || salas[indice].centro == null)
                return;

            sala = indice;
            pieza = -1;
            Sala s = salas[indice];
            CambiarRotulo("Arte del equipo: " + s.nombre, s.descripcion);
            if (Camara != null)
                Camara.Enfocar(s.centro.position, s.distancia, s.altura);
        }

        void CambiarPieza(int paso)
        {
            if (sala < 0)
                IrASala(0);
            if (sala < 0 || salas[sala].piezas.Count == 0)
                return;

            List<Transform> piezas = salas[sala].piezas;
            pieza = ((pieza + paso) % piezas.Count + piezas.Count) % piezas.Count;
            Transform elegida = piezas[pieza];
            if (elegida != null)
                EnfocarEn(FocoDe(elegida.name, elegida.gameObject, 1.2f));
        }

        protected override string AyudaCamara => "Doble clic: enfocar pieza   Rueda: acercar   Clic der.: girar";

        protected override void Update()
        {
            base.Update();

            Mouse raton = Mouse.current;
            if (raton == null || !raton.leftButton.wasPressedThisFrame)
                return;

            float ahora = Time.unscaledTime;
            bool doble = ahora - ultimoClic < 0.35f;
            ultimoClic = doble ? -1f : ahora;
            if (doble)
                EnfocarBajoCursor(raton.position.ReadValue());
        }

        // Sin colliders: la pieza elegida es la mas cercana cuyo rectangulo en pantalla contiene el cursor.
        void EnfocarBajoCursor(Vector2 cursor)
        {
            Camera camara = Camera.main;
            if (camara == null)
                return;

            float mejor = float.MaxValue;
            for (int s = 0; s < salas.Count; s++)
            {
                for (int p = 0; p < salas[s].piezas.Count; p++)
                {
                    Transform candidata = salas[s].piezas[p];
                    if (candidata == null || !RectEnPantalla(camara, candidata, out Rect rect, out float profundidad))
                        continue;
                    if (!rect.Contains(cursor) || profundidad >= mejor)
                        continue;

                    mejor = profundidad;
                    sala = s;
                    pieza = p;
                }
            }

            if (mejor < float.MaxValue)
            {
                Transform elegida = salas[sala].piezas[pieza];
                CambiarRotulo("Arte del equipo: " + salas[sala].nombre, elegida.name);
                EnfocarEn(FocoDe(elegida.name, elegida.gameObject, 1.2f));
            }
        }

        static bool RectEnPantalla(Camera camara, Transform pieza, out Rect rect, out float profundidad)
        {
            rect = default;
            profundidad = 0f;
            bool hay = false;
            Bounds limites = default;
            foreach (Renderer render in pieza.GetComponentsInChildren<Renderer>())
            {
                if (!render.enabled || render is ParticleSystemRenderer)
                    continue;
                if (!hay)
                    limites = render.bounds;
                else
                    limites.Encapsulate(render.bounds);
                hay = true;
            }
            if (!hay)
                return false;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = limites.center + Vector3.Scale(limites.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 enPantalla = camara.WorldToScreenPoint(esquina);
                if (enPantalla.z <= 0f)
                    return false;
                min = Vector2.Min(min, enPantalla);
                max = Vector2.Max(max, enPantalla);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            profundidad = Vector3.Distance(camara.transform.position, limites.center);
            return true;
        }

        void AlternarArcilla()
        {
            if (materialArcilla == null)
                return;

            arcilla = !arcilla;
            foreach (Sala s in salas)
            {
                if (!s.modelos)
                    continue;

                foreach (Transform p in s.piezas)
                {
                    if (p == null)
                        continue;
                    foreach (Renderer render in p.GetComponentsInChildren<Renderer>())
                    {
                        if (render is ParticleSystemRenderer)
                            continue;

                        if (arcilla)
                        {
                            originales[render] = render.sharedMaterials;
                            Material[] gris = new Material[render.sharedMaterials.Length];
                            for (int i = 0; i < gris.Length; i++)
                                gris[i] = materialArcilla;
                            render.sharedMaterials = gris;
                        }
                        else if (originales.TryGetValue(render, out Material[] previos))
                        {
                            render.sharedMaterials = previos;
                        }
                    }
                }
            }
            if (!arcilla)
                originales.Clear();
        }
    }
}
