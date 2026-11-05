using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Vitrina de animaciones: cada personaje en su pedestal con todos sus clips, tambien los que no
    /// estan conectados en su Animator (ReproductorClips los toca directo). Una etiqueta en el suelo
    /// dice que clip suena.
    ///   - Desfile (Espacio): cada personaje pasa solo por sus clips.
    ///   - 1 a 5 eligen un personaje y la camara lo enfoca; 0 vuelve a todos.
    ///   - Q y E cambian al clip anterior o siguiente del elegido (o de todos).
    /// </summary>
    public class Vitrina03Animaciones : VitrinaPortafolio
    {
        [Serializable]
        public class Personaje
        {
            public string nombre = "Personaje";
            public GameObject modelo;
            public List<AnimationClip> clips = new List<AnimationClip>();
        }

        public List<Personaje> personajes = new List<Personaje>();

        [Header("Desfile")]
        public bool desfile = true;
        [Tooltip("Segundos minimos de cada clip en el desfile (los ciclos cortos se repiten).")]
        [Min(0.5f)] public float minimoPorClip = 2.6f;
        [Tooltip("Segundos que se queda el final de un clip que no se repite (ataque, muerte).")]
        [Min(0f)] public float pausaFinal = 0.6f;

        [Header("Etiquetas")]
        public TMP_FontAsset fuenteEtiquetas;
        public Color colorEtiqueta = new Color(1f, 0.94f, 0.66f, 1f);
        public Color colorElegido = new Color(0.62f, 0.85f, 1f, 1f);

        class Estado
        {
            public GameObject copia;
            public ReproductorClips reproductor;
            public TextMeshPro etiqueta;
            public int clip;
            public float inicio;
        }

        Estado[] estados = new Estado[0];
        int elegido = -1;

        protected override void Preparar()
        {
            estados = new Estado[personajes.Count];
            for (int i = 0; i < personajes.Count; i++)
            {
                int indice = i;
                estados[i] = Crear(personajes[i]);
                Registrar(TeclaNumero(i + 1), "Elegir: " + personajes[i].nombre, () => Elegir(indice));
            }

            Registrar(Key.Digit0, "Todos", () => Elegir(-1));
            Registrar(Key.Q, "Clip anterior", () => Cambiar(-1));
            Registrar(Key.E, "Clip siguiente", () => Cambiar(1));
            Registrar(Key.Space, "Desfile automatico", () => desfile = !desfile, () => desfile);
        }

        protected override void Reiniciar()
        {
            for (int i = 0; i < estados.Length; i++)
                Tocar(i, 0);
            Elegir(-1);
        }

        protected override void PuntosDeFoco(List<Foco> lista)
        {
            for (int i = 0; i < estados.Length; i++)
            {
                if (estados[i] != null)
                    lista.Add(FocoDe(personajes[i].nombre, estados[i].copia));
            }
        }

        Estado Crear(Personaje personaje)
        {
            if (personaje.modelo == null || personaje.clips.Count == 0)
                return null;

            Transform plantilla = personaje.modelo.transform;
            personaje.modelo.SetActive(false);
            GameObject copia = UtilVitrina.CopiaVisual(personaje.modelo, plantilla.parent, plantilla.position, plantilla.rotation);
            copia.transform.localScale = plantilla.localScale;

            Animator animador = copia.GetComponentInChildren<Animator>();
            if (animador == null)
            {
                Debug.LogWarning("Vitrina03Animaciones: " + personaje.nombre + " no tiene Animator.", this);
                return null;
            }

            Estado estado = new Estado
            {
                copia = copia,
                reproductor = animador.gameObject.AddComponent<ReproductorClips>(),
                etiqueta = CrearEtiqueta(plantilla.position)
            };
            return estado;
        }

        // Etiqueta tumbada en el suelo delante del pedestal, como la cartela de un museo.
        TextMeshPro CrearEtiqueta(Vector3 pie)
        {
            Camera camara = Camera.main;
            Vector3 haciaCamara = camara != null ? camara.transform.position - pie : Vector3.back;
            haciaCamara.y = 0f;
            haciaCamara = haciaCamara.sqrMagnitude > 0.001f ? haciaCamara.normalized : Vector3.back;

            GameObject objeto = new GameObject("Etiqueta", typeof(RectTransform));
            objeto.transform.SetParent(transform, false);
            objeto.transform.position = new Vector3(pie.x, 0.02f, pie.z) + haciaCamara * 1.05f;
            objeto.transform.rotation = Quaternion.LookRotation(Vector3.down, -haciaCamara);

            TextMeshPro texto = objeto.AddComponent<TextMeshPro>();
            if (fuenteEtiquetas != null)
                texto.font = fuenteEtiquetas;
            texto.fontSize = 2.4f;
            texto.alignment = TextAlignmentOptions.Center;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.rectTransform.sizeDelta = new Vector2(3f, 1f);
            TextoFlotante.EstiloCaricatura(texto, 0.25f);
            return texto;
        }

        protected override void Start()
        {
            base.Start();
            Reiniciar();
        }

        protected override void Update()
        {
            base.Update();
            if (!desfile)
                return;

            for (int i = 0; i < estados.Length; i++)
            {
                Estado estado = estados[i];
                if (estado == null)
                    continue;

                AnimationClip clip = personajes[i].clips[estado.clip];
                float duracion = clip.isLooping
                    ? Mathf.Max(minimoPorClip, clip.length)
                    : Mathf.Max(minimoPorClip, clip.length + pausaFinal);
                if (Time.time - estado.inicio >= duracion)
                    Tocar(i, estado.clip + 1);
            }
        }

        void Elegir(int indice)
        {
            elegido = indice >= 0 && indice < estados.Length && estados[indice] != null ? indice : -1;

            if (elegido >= 0)
                EnfocarEn(FocoDe(personajes[elegido].nombre, estados[elegido].copia));
            else
                PlanoGeneral();

            for (int i = 0; i < estados.Length; i++)
                Rotular(i);
        }

        void Cambiar(int paso)
        {
            for (int i = 0; i < estados.Length; i++)
            {
                if (estados[i] != null && (elegido < 0 || elegido == i))
                    Tocar(i, estados[i].clip + paso);
            }
        }

        void Tocar(int indice, int clip)
        {
            Estado estado = estados[indice];
            if (estado == null)
                return;

            int cantidad = personajes[indice].clips.Count;
            estado.clip = ((clip % cantidad) + cantidad) % cantidad;
            estado.inicio = Time.time;
            estado.reproductor.Reproducir(personajes[indice].clips[estado.clip]);
            Rotular(indice);
        }

        void Rotular(int indice)
        {
            Estado estado = estados[indice];
            if (estado == null || estado.etiqueta == null)
                return;

            List<AnimationClip> clips = personajes[indice].clips;
            AnimationClip clip = clips[estado.clip];
            string nombreClip = clip != null ? clip.name.Replace('_', ' ') : "-";
            estado.etiqueta.text = $"{personajes[indice].nombre}\n<size=70%>{nombreClip}  {estado.clip + 1}/{clips.Count}</size>";

            Color color = indice == elegido ? colorElegido : colorEtiqueta;
            TextoFlotante.Degradado(estado.etiqueta, Color.white, color);
        }
    }
}
