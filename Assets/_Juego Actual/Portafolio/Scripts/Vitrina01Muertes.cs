using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Vitrina de los efectos de muerte: un personaje por pedestal y una tecla para cada uno.
    /// Al pulsarla recibe el golpe (MinecraftHitFeedback), muere con su estilo (EfectoMuerte) y
    /// reaparece al rato. Espacio los mata a todos en cascada.
    /// Los modelos de la escena son plantillas: se apagan al empezar y se juega con copias
    /// visuales, asi se pueden mover y girar en el editor para encuadrar.
    /// </summary>
    public class Vitrina01Muertes : VitrinaPortafolio
    {
        [Serializable]
        public class Pedestal
        {
            public string nombre = "Enemigo";
            public GameObject modelo;
            public EfectoMuerte.Estilo estilo = EfectoMuerte.Estilo.Desintegrarse;
        }

        public List<Pedestal> pedestales = new List<Pedestal>();

        [Header("Ritmo")]
        [Tooltip("Segundos entre el golpe y el inicio de la muerte.")]
        [Min(0f)] public float retrasoGolpe = 0.18f;
        [Tooltip("Segundos hasta que el personaje vuelve a aparecer. 0 = solo con R.")]
        [Min(0f)] public float reaparecerTras = 3.5f;
        [Min(0f)] public float intervaloCascada = 0.55f;

        GameObject[] vivos;
        readonly List<GameObject> muriendo = new List<GameObject>();

        protected override void Preparar()
        {
            vivos = new GameObject[pedestales.Count];

            for (int i = 0; i < pedestales.Count; i++)
            {
                int indice = i;
                if (pedestales[i].modelo != null)
                    pedestales[i].modelo.SetActive(false);
                Registrar(TeclaNumero(i + 1), "Morir: " + pedestales[i].nombre, () => Matar(indice));
            }
            Registrar(Key.Space, "Todos en cascada", () => StartCoroutine(Cascada()));

            for (int i = 0; i < pedestales.Count; i++)
                Aparecer(i, false);
        }

        protected override void Reiniciar()
        {
            StopAllCoroutines();

            // Los que estaban recibiendo el golpe se quedarian congelados en rojo.
            for (int i = 0; i < muriendo.Count; i++)
            {
                if (muriendo[i] != null)
                    Destroy(muriendo[i]);
            }
            muriendo.Clear();

            for (int i = 0; i < pedestales.Count; i++)
                Aparecer(i, true);
        }

        // La cascada recien empezada: los primeros ya se deshacen y los demas siguen en pie.
        public override List<Key> AccionesMiniatura => new List<Key> { Key.Space };
        public override float EsperaMiniatura => 1.3f;

        protected override void PuntosDeFoco(List<Foco> lista)
        {
            for (int i = 0; i < pedestales.Count; i++)
            {
                GameObject objeto = vivos[i] != null ? vivos[i] : pedestales[i].modelo;
                if (objeto != null)
                    lista.Add(FocoDe(pedestales[i].nombre, objeto));
            }
        }

        void Aparecer(int indice, bool animar)
        {
            if (vivos[indice] != null)
                Destroy(vivos[indice]);

            GameObject modelo = pedestales[indice].modelo;
            if (modelo == null)
                return;

            Transform plantilla = modelo.transform;
            GameObject copia = UtilVitrina.CopiaVisual(modelo, plantilla.parent, plantilla.position, plantilla.rotation);
            copia.transform.localScale = plantilla.localScale;

            EfectoMuerte efecto = copia.AddComponent<EfectoMuerte>();
            efecto.estilo = pedestales[indice].estilo;
            copia.AddComponent<MinecraftHitFeedback>();

            vivos[indice] = copia;
            if (animar)
                StartCoroutine(UtilVitrina.Crecer(copia.transform, 0.5f));
        }

        void Matar(int indice)
        {
            if (indice < vivos.Length && vivos[indice] != null)
                StartCoroutine(Morir(indice));
        }

        IEnumerator Morir(int indice)
        {
            GameObject victima = vivos[indice];
            vivos[indice] = null;
            muriendo.Add(victima);

            Vector3 origen = PuntoAtacante(victima.transform.position);
            MinecraftHitFeedback golpe = victima.GetComponent<MinecraftHitFeedback>();
            if (golpe != null)
                golpe.PlayHitFeedbackFromAttacker(origen);

            if (retrasoGolpe > 0f)
                yield return new WaitForSeconds(retrasoGolpe);

            if (victima == null)
                yield break;

            // Como en el juego: el efecto copia el cuerpo y el enemigo se destruye en el mismo frame.
            EfectoMuerte.Reproducir(victima, origen);
            muriendo.Remove(victima);
            Destroy(victima);

            if (reaparecerTras <= 0f)
                yield break;

            yield return new WaitForSeconds(reaparecerTras);
            if (vivos[indice] == null)
                Aparecer(indice, true);
        }

        IEnumerator Cascada()
        {
            for (int i = 0; i < pedestales.Count; i++)
            {
                Matar(i);
                yield return new WaitForSeconds(intervaloCascada);
            }
        }
    }
}
