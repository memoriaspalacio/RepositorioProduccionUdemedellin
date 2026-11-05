using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Ayudas compartidas por las vitrinas.
    /// CopiaVisual hace una copia de un personaje solo con lo que se ve (mallas y Animator): la
    /// crea dentro de un objeto apagado, le quita los scripts, colliders, rigidbodies y agentes
    /// antes de que se ejecute su Awake y luego la enciende. Asi se puede usar cualquier prefab del
    /// juego (con su EnemyHealth, su Behaviour o su NavMeshAgent) sin que intente jugar.
    /// </summary>
    public static class UtilVitrina
    {
        public static GameObject CopiaVisual(GameObject original, Transform padre, Vector3 posicion, Quaternion rotacion)
        {
            GameObject espera = new GameObject("Copia en espera");
            espera.SetActive(false);

            GameObject copia = UnityEngine.Object.Instantiate(original, posicion, rotacion, espera.transform);
            copia.name = original.name;
            Limpiar(copia);

            copia.transform.SetParent(padre, true);
            copia.SetActive(true);
            UnityEngine.Object.Destroy(espera);
            return copia;
        }

        /// <summary>Quita todo lo que no sea visual. Solo es seguro en un objeto que nunca se encendio.</summary>
        public static void Limpiar(GameObject objeto)
        {
            // Varias pasadas: un script con RequireComponent tiene que irse antes que lo que pide.
            for (int pasada = 0; pasada < 4; pasada++)
            {
                List<MonoBehaviour> scripts = new List<MonoBehaviour>(objeto.GetComponentsInChildren<MonoBehaviour>(true));
                if (scripts.Count == 0)
                    break;

                for (int i = 0; i < scripts.Count; i++)
                {
                    if (scripts[i] != null && !LoPideOtro(scripts[i], scripts))
                        UnityEngine.Object.DestroyImmediate(scripts[i]);
                }
            }

            foreach (NavMeshAgent agente in objeto.GetComponentsInChildren<NavMeshAgent>(true))
                UnityEngine.Object.DestroyImmediate(agente);
            foreach (Rigidbody cuerpo in objeto.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(cuerpo);
            foreach (Collider colision in objeto.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(colision);
        }

        /// <summary>Si otro script del mismo objeto lo exige con RequireComponent (hay que quitarlo despues).</summary>
        public static bool LoPideOtro(Component componente, List<MonoBehaviour> todos)
        {
            Type tipo = componente.GetType();
            for (int i = 0; i < todos.Count; i++)
            {
                MonoBehaviour otro = todos[i];
                if (otro == null || otro == componente || otro.gameObject != componente.gameObject)
                    continue;

                foreach (RequireComponent pide in otro.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (Pide(pide.m_Type0, tipo) || Pide(pide.m_Type1, tipo) || Pide(pide.m_Type2, tipo))
                        return true;
                }
            }
            return false;
        }

        static bool Pide(Type pedido, Type tipo)
        {
            return pedido != null && pedido.IsAssignableFrom(tipo);
        }

        /// <summary>Dispara un trigger del Animator solo si existe (sin avisos en consola).</summary>
        public static void Trigger(Animator animator, string nombre)
        {
            if (Tiene(animator, nombre, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(nombre);
        }

        public static void Bool(Animator animator, string nombre, bool valor)
        {
            if (Tiene(animator, nombre, AnimatorControllerParameterType.Bool))
                animator.SetBool(nombre, valor);
        }

        static bool Tiene(Animator animator, string nombre, AnimatorControllerParameterType tipo)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter parametro in animator.parameters)
            {
                if (parametro.type == tipo && parametro.name == nombre)
                    return true;
            }
            return false;
        }

        /// <summary>Aparece creciendo con un rebote elastico.</summary>
        public static IEnumerator Crecer(Transform objeto, float duracion)
        {
            Vector3 escala = objeto.localScale;
            float tiempo = 0f;
            while (objeto != null && tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float k = Mathf.Clamp01(tiempo / duracion);
                float elastico = Mathf.Pow(2f, -10f * k) * Mathf.Sin((k * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
                objeto.localScale = escala * elastico;
                yield return null;
            }
            if (objeto != null)
                objeto.localScale = escala;
        }
    }
}
