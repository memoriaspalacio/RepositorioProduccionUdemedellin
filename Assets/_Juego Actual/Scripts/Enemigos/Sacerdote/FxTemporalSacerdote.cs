using UnityEngine;

namespace UdeM.Characters
{
    /// <summary>
    /// Efecto suelto de un solo uso (cruz rota, destello de invocacion, escudo roto). Vive en un
    /// objeto propio para no depender de quien lo creo, apaga su luz con el tiempo y se borra
    /// solo cuando ya no le quedan particulas vivas.
    /// </summary>
    public class FxTemporalSacerdote : MonoBehaviour
    {
        Light luz;
        float intensidadLuz;
        float duracionLuz;
        float duracionMinima;
        float tiempo;
        ParticleSystem[] sistemas;

        /// <summary>Crea el objeto del efecto en una posicion del mundo.</summary>
        public static FxTemporalSacerdote Crear(string nombre, Vector3 posicion, float duracionMinima)
        {
            GameObject objeto = new GameObject(nombre);
            objeto.transform.position = posicion;
            FxTemporalSacerdote fx = objeto.AddComponent<FxTemporalSacerdote>();
            fx.duracionMinima = duracionMinima;
            return fx;
        }

        /// <summary>Destello de luz que se apaga en la duracion indicada.</summary>
        public void Destello(Color color, float intensidad, float alcance, float duracion)
        {
            luz = RecursosSacerdote.CrearLuz(transform, color, alcance);
            intensidadLuz = intensidad;
            duracionLuz = Mathf.Max(0.01f, duracion);
            luz.intensity = intensidad;
        }

        public ParticleSystem Sistema(string nombre, Material material, int maximo)
        {
            ParticleSystem sistema = RecursosSacerdote.CrearSistema(transform, nombre, material, maximo);
            sistemas = GetComponentsInChildren<ParticleSystem>();
            return sistema;
        }

        void Update()
        {
            tiempo += Time.deltaTime;

            if (luz != null)
            {
                float resto = 1f - Mathf.Clamp01(tiempo / duracionLuz);
                luz.intensity = intensidadLuz * resto * resto;
            }

            if (tiempo < duracionMinima || tiempo < duracionLuz)
                return;

            if (sistemas != null)
            {
                for (int i = 0; i < sistemas.Length; i++)
                {
                    if (sistemas[i] != null && sistemas[i].particleCount > 0)
                        return;
                }
            }

            Destroy(gameObject);
        }
    }
}
