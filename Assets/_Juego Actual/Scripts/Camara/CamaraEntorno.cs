using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Extension de la CinemachineCamera que abre el plano para que se aprecie el escenario sin ensenar sus
// costuras: aleja un poco la camara, adelanta el encuadre hacia donde camina el jugador y suaviza el
// seguimiento, y procura que el borde de la pantalla no salga del suelo modelado (o de la zona que se le de).
//
// Esconder costuras es lo ultimo: antes van el jugador, las casillas de su alrededor y los enemigos cercanos,
// que siempre quedan dentro del area segura de la pantalla aunque para ello se asome algo de fuera del mapa.
//
// Solo mueve la camara, nunca la gira: el movimiento del jugador es relativo a la camara y asi no cambia.
// Para comparar con la camara de antes basta con apagar el componente en juego.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Cinemachine/Procedural/Extensions/Camara Entorno")]
public class CamaraEntorno : CinemachineExtension
{
    [Header("Encuadre")]
    [Tooltip("Metros que se aleja la camara sobre su distancia normal. Si el escenario no da para tanto se aleja lo que quepa.")]
    [Range(0f, 6f)] public float alejamiento = 1.5f;
    [Tooltip("Metros que el encuadre se adelanta hacia donde se movio el jugador por ultima vez.")]
    [Range(0f, 4f)] public float adelanto = 1.2f;
    [Tooltip("Segundos sin moverse tras los que el encuadre vuelve a centrarse en el jugador.")]
    [Min(0f)] public float segundosParaCentrar = 2f;

    [Header("Suavizado (segundos)")]
    [Tooltip("Lo que tarda la camara en alcanzar al jugador. 0 la deja pegada como antes.")]
    [Range(0f, 1f)] public float suavizadoSeguimiento = 0.18f;
    [Tooltip("Lo que tarda el encuadre en girar hacia la nueva direccion.")]
    [Range(0f, 2f)] public float suavizadoAdelanto = 0.6f;

    [Header("Lo que siempre se ve")]
    [Tooltip("Casillas alrededor del jugador que siempre quedan en pantalla (hasta el borde del mapa).")]
    [Range(0, 3)] public int casillasAlrededor = 1;
    [Tooltip("Metros: los enemigos mas cerca que esto entran en el encuadre, empezando por el mas cercano. 0 los ignora.")]
    [Min(0f)] public float radioEnemigos = 9f;
    [Tooltip("Altura de la cabeza de los enemigos, para no cortarlos por arriba.")]
    [Min(0f)] public float alturaEnemigos = 1.6f;
    [Tooltip("Area segura de la pantalla, en fraccion de su ancho, a cada lado.")]
    [Range(0f, 0.4f)] public float margenLateral = 0.06f;
    [Tooltip("Area segura abajo, en fraccion del alto. Deja sitio a lo que haya de interfaz.")]
    [Range(0f, 0.4f)] public float margenInferior = 0.12f;
    [Tooltip("Area segura arriba, en fraccion del alto.")]
    [Range(0f, 0.4f)] public float margenSuperior = 0.1f;

    [Header("Limites del escenario")]
    [Tooltip("Zona que se puede ver. Vacio: el suelo modelado del MeshGrid de la escena.")]
    public Collider zonaVisible;
    [Tooltip("Metros que la vista puede pasar del borde de la zona (negativo: se queda mas adentro).")]
    public float margen = 0f;
    [Tooltip("Hasta que altura de la pantalla (0 abajo, 1 arriba) se procura ver suelo. Lo de mas arriba se toma como fondo: paredes, niebla, cielo.")]
    [Range(0.2f, 1f)] public float alturaComprobada = 0.7f;

    const int PasosBusqueda = 8;
    const float DistanciaMaxima = 80f;

    Bounds zona;
    Bounds mapa;
    float alturaSuelo;
    float tamanoCasilla = 2f;
    bool tieneZona;
    bool tieneMapa;
    bool zonaBuscada;

    Vector3 posicionSuave;
    Vector3 velocidadSuave;
    bool tieneSuave;

    Vector3 adelantoActual;
    Vector3 velocidadAdelanto;
    Vector3 direccion;
    Vector3 ultimaPosicion;
    bool tieneUltima;
    float tiempoQuieto;

    readonly Vector3[] huella = new Vector3[4];
    readonly List<Transform> enemigosCerca = new List<Transform>();

    // Desplazamientos permitidos en los ejes de la camara (x a la derecha, y hacia arriba)
    float rangoXMin, rangoXMax, rangoYMin, rangoYMax;

    void OnValidate()
    {
        zonaBuscada = false;
    }

    public override float GetMaxDampTime()
    {
        return Mathf.Max(suavizadoSeguimiento, suavizadoAdelanto);
    }

    public override void OnTargetObjectWarped(CinemachineVirtualCameraBase vcam, Transform target, Vector3 positionDelta)
    {
        posicionSuave += positionDelta;
        ultimaPosicion += new Vector3(positionDelta.x, 0f, positionDelta.z);
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body)
            return;

        bool jugando = Application.isPlaying && deltaTime >= 0f;
        Transform objetivo = vcam.Follow;
        Vector3 posicionBase = state.RawPosition;
        Quaternion rotacion = state.RawOrientation;
        float tangente = Mathf.Tan(state.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspecto = state.Lens.Aspect > 0.01f ? state.Lens.Aspect : 16f / 9f;

        if (jugando && objetivo != null)
            ActualizarAdelanto(objetivo.position, deltaTime);
        else
            ReiniciarAdelanto(objetivo);

        BuscarZona();

        Vector3 atras = rotacion * Vector3.back;
        Vector3 deseada = posicionBase + adelantoActual + atras * AlejamientoQueCabe(posicionBase, rotacion, tangente, aspecto);
        if (tieneZona)
            deseada += Correccion(deseada, rotacion, tangente, aspecto, out _);
        if (objetivo != null)
            deseada += Encuadre(deseada, objetivo.position, rotacion, tangente, aspecto, true);

        if (jugando && tieneSuave && suavizadoSeguimiento > 0f)
        {
            posicionSuave = Vector3.SmoothDamp(posicionSuave, deseada, ref velocidadSuave, suavizadoSeguimiento, Mathf.Infinity, deltaTime);
        }
        else
        {
            posicionSuave = deseada;
            velocidadSuave = Vector3.zero;
        }
        tieneSuave = true;

        // Mientras el suavizado alcanza al jugador, el jugador y su alrededor no pueden quedarse fuera.
        // Los enemigos ya van en la posicion deseada: meterlos aqui de golpe se saltaria el suavizado.
        Vector3 final = posicionSuave;
        if (objetivo != null)
            final += Encuadre(final, objetivo.position, rotacion, tangente, aspecto, false);

        // Deja en la huella lo que de verdad se ve, para el gizmo
        if (tieneZona)
            HuellaEnSuelo(final, rotacion, tangente, aspecto);

        state.RawPosition = final;
    }

    void ActualizarAdelanto(Vector3 posicionObjetivo, float deltaTime)
    {
        Vector3 plana = new Vector3(posicionObjetivo.x, 0f, posicionObjetivo.z);
        if (!tieneUltima)
        {
            ultimaPosicion = plana;
            tieneUltima = true;
        }

        Vector3 movimiento = plana - ultimaPosicion;
        if (movimiento.sqrMagnitude > 0.0025f)
        {
            direccion = movimiento.normalized;
            ultimaPosicion = plana;
            tiempoQuieto = 0f;
        }
        else
        {
            tiempoQuieto += deltaTime;
        }

        Vector3 meta = tiempoQuieto < segundosParaCentrar ? direccion * adelanto : Vector3.zero;
        if (suavizadoAdelanto > 0f)
        {
            adelantoActual = Vector3.SmoothDamp(adelantoActual, meta, ref velocidadAdelanto, suavizadoAdelanto, Mathf.Infinity, deltaTime);
        }
        else
        {
            adelantoActual = meta;
            velocidadAdelanto = Vector3.zero;
        }
    }

    void ReiniciarAdelanto(Transform objetivo)
    {
        adelantoActual = Vector3.zero;
        velocidadAdelanto = Vector3.zero;
        direccion = Vector3.zero;
        tiempoQuieto = 0f;
        tieneUltima = objetivo != null;
        if (objetivo != null)
            ultimaPosicion = new Vector3(objetivo.position.x, 0f, objetivo.position.z);
    }

    // El tamano de lo que se ve del suelo solo depende de la altura de la camara, no de donde este el jugador:
    // si con todo el alejamiento la vista es mas ancha que el escenario, se busca el mayor que aun quepa.
    float AlejamientoQueCabe(Vector3 posicionBase, Quaternion rotacion, float tangente, float aspecto)
    {
        if (!tieneZona || alejamiento <= 0f)
            return alejamiento;

        Vector3 atras = rotacion * Vector3.back;
        Correccion(posicionBase + atras * alejamiento, rotacion, tangente, aspecto, out bool cabe);
        if (cabe)
            return alejamiento;

        float minimo = 0f;
        float maximo = alejamiento;
        for (int i = 0; i < PasosBusqueda; i++)
        {
            float medio = (minimo + maximo) * 0.5f;
            Correccion(posicionBase + atras * medio, rotacion, tangente, aspecto, out cabe);
            if (cabe)
                minimo = medio;
            else
                maximo = medio;
        }

        return minimo;
    }

    // Desplazamiento en el plano del suelo que mete la parte comprobada de la pantalla dentro de la zona.
    // Si la vista no cabe en un eje, la centra en ese eje para repartir lo que sobra por igual a cada lado.
    Vector3 Correccion(Vector3 posicion, Quaternion rotacion, float tangente, float aspecto, out bool cabe)
    {
        cabe = true;
        if (!HuellaEnSuelo(posicion, rotacion, tangente, aspecto))
            return Vector3.zero;

        Vector3 minimo = huella[0];
        Vector3 maximo = huella[0];
        for (int i = 1; i < huella.Length; i++)
        {
            minimo = Vector3.Min(minimo, huella[i]);
            maximo = Vector3.Max(maximo, huella[i]);
        }

        float dx = CorreccionEje(minimo.x, maximo.x, zona.min.x - margen, zona.max.x + margen, ref cabe);
        float dz = CorreccionEje(minimo.z, maximo.z, zona.min.z - margen, zona.max.z + margen, ref cabe);
        return new Vector3(dx, 0f, dz);
    }

    static float CorreccionEje(float vistaMin, float vistaMax, float zonaMin, float zonaMax, ref bool cabe)
    {
        if (vistaMax - vistaMin > zonaMax - zonaMin)
        {
            cabe = false;
            return (zonaMin + zonaMax) * 0.5f - (vistaMin + vistaMax) * 0.5f;
        }

        if (vistaMin < zonaMin)
            return zonaMin - vistaMin;
        if (vistaMax > zonaMax)
            return zonaMax - vistaMax;
        return 0f;
    }

    // Donde tocan el suelo las cuatro esquinas de la parte comprobada de la pantalla
    bool HuellaEnSuelo(Vector3 posicion, Quaternion rotacion, float tangente, float aspecto)
    {
        float altura = posicion.y - alturaSuelo;
        if (altura <= 0.1f)
            return false;

        for (int i = 0; i < huella.Length; i++)
        {
            float u = (i & 1) == 0 ? 0f : 1f;
            float v = (i & 2) == 0 ? 0f : alturaComprobada;
            Vector3 rayo = rotacion * new Vector3((u * 2f - 1f) * tangente * aspecto, (v * 2f - 1f) * tangente, 1f);

            // Un rayo que no baja (o baja casi nada) se corta lejos para no pedir un suelo infinito
            float distancia = rayo.y < -0.0001f ? Mathf.Min(altura / -rayo.y, DistanciaMaxima) : DistanciaMaxima;
            huella[i] = posicion + rayo * distancia;
        }

        return true;
    }

    // Menor desplazamiento (de lado o de alto, en los ejes de la camara) que deja en el area segura de la pantalla,
    // por orden: el jugador, las casillas de su alrededor y los enemigos cercanos del mas proximo al mas lejano.
    // Lo que ya no cabe junto con lo anterior se deja fuera en ese eje (para eso estan las flechas de los enemigos).
    Vector3 Encuadre(Vector3 posicion, Vector3 jugador, Quaternion rotacion, float tangente, float aspecto, bool conEnemigos)
    {
        Quaternion inversa = Quaternion.Inverse(rotacion);
        rangoXMin = rangoYMin = float.NegativeInfinity;
        rangoXMax = rangoYMax = float.PositiveInfinity;

        Incluir(jugador, posicion, inversa, tangente, aspecto);

        if (tieneMapa && casillasAlrededor > 0)
        {
            float radio = tamanoCasilla * (casillasAlrededor + 0.5f);
            float x0 = Mathf.Max(jugador.x - radio, mapa.min.x);
            float x1 = Mathf.Min(jugador.x + radio, mapa.max.x);
            float z0 = Mathf.Max(jugador.z - radio, mapa.min.z);
            float z1 = Mathf.Min(jugador.z + radio, mapa.max.z);
            Incluir(new Vector3(x0, alturaSuelo, z0), posicion, inversa, tangente, aspecto);
            Incluir(new Vector3(x1, alturaSuelo, z0), posicion, inversa, tangente, aspecto);
            Incluir(new Vector3(x0, alturaSuelo, z1), posicion, inversa, tangente, aspecto);
            Incluir(new Vector3(x1, alturaSuelo, z1), posicion, inversa, tangente, aspecto);
        }

        if (conEnemigos && radioEnemigos > 0f)
        {
            BuscarEnemigos(jugador);
            for (int i = 0; i < enemigosCerca.Count; i++)
            {
                Vector3 pies = enemigosCerca[i].position;
                Incluir(pies, posicion, inversa, tangente, aspecto);
                Incluir(pies + Vector3.up * alturaEnemigos, posicion, inversa, tangente, aspecto);
            }
        }

        float dx = Mathf.Clamp(0f, rangoXMin, rangoXMax);
        float dy = Mathf.Clamp(0f, rangoYMin, rangoYMax);
        return rotacion * new Vector3(dx, dy, 0f);
    }

    // Estrecha el rango de desplazamiento para que el punto quede en el area segura. Moverse por los ejes x e y
    // de la camara no cambia la profundidad del punto, asi que el rango sale exacto.
    void Incluir(Vector3 punto, Vector3 posicion, Quaternion inversa, float tangente, float aspecto)
    {
        Vector3 local = inversa * (punto - posicion);
        if (local.z <= 0.1f)
            return;

        float ancho = local.z * tangente * aspecto;
        float alto = local.z * tangente;
        float lateral = (1f - 2f * margenLateral) * ancho;

        Juntar(ref rangoXMin, ref rangoXMax, local.x - lateral, local.x + lateral);
        Juntar(ref rangoYMin, ref rangoYMax, local.y - (1f - 2f * margenSuperior) * alto, local.y + (1f - 2f * margenInferior) * alto);
    }

    static void Juntar(ref float minimo, ref float maximo, float nuevoMin, float nuevoMax)
    {
        if (nuevoMin > maximo || nuevoMax < minimo)
            return;

        minimo = Mathf.Max(minimo, nuevoMin);
        maximo = Mathf.Min(maximo, nuevoMax);
    }

    void BuscarEnemigos(Vector3 jugador)
    {
        enemigosCerca.Clear();
        float radio2 = radioEnemigos * radioEnemigos;
        List<OffScreenTarget> activos = OffScreenTarget.Activos;
        for (int i = 0; i < activos.Count; i++)
        {
            if (activos[i] == null)
                continue;

            Transform enemigo = activos[i].transform;
            float distancia2 = (enemigo.position - jugador).sqrMagnitude;
            if (distancia2 > radio2)
                continue;

            // Son pocos: se mete cada uno en su sitio, del mas cercano al mas lejano
            int j = enemigosCerca.Count;
            while (j > 0 && (enemigosCerca[j - 1].position - jugador).sqrMagnitude > distancia2)
                j--;
            enemigosCerca.Insert(j, enemigo);
        }
    }

    void BuscarZona()
    {
        if (zonaBuscada && (tieneZona || Application.isPlaying))
            return;
        zonaBuscada = true;
        tieneZona = false;
        tieneMapa = false;

        // Igual que MeshGrid y SueloGrid: su Renderer o el primero de sus hijos
        MeshGrid grid = FindFirstObjectByType<MeshGrid>();
        Renderer suelo = grid != null ? grid.GetComponent<Renderer>() : null;
        if (suelo == null && grid != null)
            suelo = grid.GetComponentInChildren<Renderer>();

        if (suelo != null)
        {
            mapa = suelo.bounds;
            alturaSuelo = mapa.max.y;
            tamanoCasilla = grid.CellSize;
            tieneMapa = true;
        }

        if (zonaVisible != null)
        {
            zona = zonaVisible.bounds;
            if (!tieneMapa)
                alturaSuelo = zona.min.y;
            tieneZona = true;
        }
        else if (tieneMapa)
        {
            zona = mapa;
            tieneZona = true;
        }

        // El plano del SueloGrid es lo que se ve como suelo, si lo hay
        SueloGrid plano = FindFirstObjectByType<SueloGrid>();
        if (tieneZona && plano != null)
            alturaSuelo = plano.transform.position.y;
    }

    void OnDrawGizmosSelected()
    {
        if (!tieneZona)
            return;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Vector3 centro = new Vector3(zona.center.x, alturaSuelo, zona.center.z);
        Gizmos.DrawWireCube(centro, new Vector3(zona.size.x + margen * 2f, 0.01f, zona.size.z + margen * 2f));

        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
        Gizmos.DrawLine(huella[0], huella[1]);
        Gizmos.DrawLine(huella[1], huella[3]);
        Gizmos.DrawLine(huella[3], huella[2]);
        Gizmos.DrawLine(huella[2], huella[0]);
    }
}
