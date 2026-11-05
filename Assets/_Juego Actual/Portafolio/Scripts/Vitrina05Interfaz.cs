using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Vitrina de la interfaz y la retroalimentacion en juego, sobre una copia de un nivel con su
    /// ritmo, su HUD y su jugador funcionando de verdad (solo sin enemigos ni condicion de victoria).
    /// El bot y las acciones viven en RitmoVitrina:
    ///   - 1 Perfecto, 2 Bueno, 3 Malo, 4 Dejar pasar notas, 5 Mezcla, 0 Bot apagado (juegas tu).
    ///   - 6 golpe fuera de ritmo, 7 recibir dano, 8 enemigo derrotado (recompensas que vuelan al
    ///     HUD), 9 tiempo critico, C transicion del corazon, V fundido de inicio.
    /// </summary>
    [RequireComponent(typeof(RitmoVitrina))]
    public class Vitrina05Interfaz : VitrinaPortafolio
    {
        RitmoVitrina ritmo;

        protected override string AyudaCamara => "WASD: jugar tu (bot en 0)   F1: graficos   F2: filtros";

        protected override void Preparar()
        {
            ritmo = GetComponent<RitmoVitrina>();

            RegistrarBot(TeclaNumero(1), "Bot: perfecto", RitmoVitrina.Calidad.Perfecto);
            RegistrarBot(TeclaNumero(2), "Bot: bueno", RitmoVitrina.Calidad.Bueno);
            RegistrarBot(TeclaNumero(3), "Bot: malo", RitmoVitrina.Calidad.Malo);
            RegistrarBot(TeclaNumero(4), "Bot: dejar pasar notas", RitmoVitrina.Calidad.Fallar);
            RegistrarBot(TeclaNumero(5), "Bot: mezcla", RitmoVitrina.Calidad.Mezcla);
            RegistrarBot(Key.Digit0, "Bot apagado", RitmoVitrina.Calidad.Apagado);
            Registrar(TeclaNumero(6), "Golpe fuera de ritmo", ritmo.GolpeAlAire);
            Registrar(TeclaNumero(7), "Recibir dano", ritmo.RecibirDano);
            Registrar(TeclaNumero(8), "Enemigo derrotado", ritmo.DerrotarEnemigo);
            Registrar(TeclaNumero(9), "Tiempo critico", ritmo.TiempoCritico);
            Registrar(Key.C, "Transicion del corazon", ritmo.TransicionCorazon);
            Registrar(Key.V, "Fundido de inicio", ritmo.FundidoDeInicio);
        }

        public override System.Collections.Generic.List<Key> AccionesMiniatura =>
            new System.Collections.Generic.List<Key> { TeclaNumero(7), TeclaNumero(8) };
        public override float EsperaMiniatura => 1.2f;

        void RegistrarBot(Key tecla, string nombre, RitmoVitrina.Calidad calidad)
        {
            Registrar(tecla, nombre, () => ritmo.Bot(calidad), () => ritmo.bot == calidad);
        }

        protected override void Reiniciar()
        {
            ritmo.Bot(RitmoVitrina.Calidad.Mezcla);
        }
    }
}
