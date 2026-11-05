using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace UdeM.Portafolio
{
    /// <summary>
    /// Carga las escenas del portafolio por su ruta. No estan en Build Settings (para no tocar la
    /// build del juego), asi que en el editor se cargan con EditorSceneManager; en una build solo
    /// funcionan las que se hayan agregado a Build Settings.
    /// </summary>
    public static class CargadorEscenas
    {
        public const string RutaMenu = "Assets/_Juego Actual/Portafolio/Escenas/00_Menu_Portafolio.unity";

        public static void Cargar(string ruta)
        {
            if (string.IsNullOrEmpty(ruta))
                return;

            // El reloj del juego sobrevive a los cambios de escena y, al llegar a cero, cargaria la
            // derrota en medio de otra vitrina: se va con la escena que lo creo.
            if (ControladorContrarreloj.Instancia != null)
            {
                Object.Destroy(ControladorContrarreloj.Instancia.gameObject);
                ControladorContrarreloj.Instancia = null;
            }
            Time.timeScale = 1f;

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                EditorSceneManager.LoadSceneInPlayMode(ruta, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            if (SceneUtility.GetBuildIndexByScenePath(ruta) >= 0)
                SceneManager.LoadScene(ruta);
            else
                Debug.LogWarning("CargadorEscenas: " + ruta + " no esta en Build Settings.");
        }

        public static void VolverAlMenu()
        {
            Cargar(RutaMenu);
        }
    }
}
