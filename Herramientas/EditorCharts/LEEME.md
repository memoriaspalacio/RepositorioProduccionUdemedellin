# Editor de Charts

Herramienta para crear a mano la partitura de notas (chart) de cada nivel. Es un solo
archivo HTML sin instalación: no forma parte del proyecto de Unity (está fuera de `Assets`),
solo el `.json` que produce llega al juego.

## Abrirlo

Doble clic en `index.html` y se abre en **Chrome o Edge** (son los únicos que pueden escribir
directamente en la carpeta del juego; en otros navegadores el chart se descarga y hay que
copiarlo a mano).

## Flujo de trabajo

1. **Nivel**: elige la escena arriba. Trae su BPM, el desfase del primer beat y las reglas de
   la escena (ventanas, viaje de la nota, penalización).
2. **Canción**: carga el mp3 del nivel (o arrástralo a la ventana). Está en
   `Assets/_Juego Actual/Arte/Audios/`. Se recuerda para la próxima vez.
3. **Calibrar** (una vez por equipo y audífonos): toca Espacio con 16 clics y la herramienta
   descuenta tu retraso de cada toque.
4. **Grabar** (tecla 2): Enter empieza la toma con un compás de cuenta de entrada; toca
   Espacio, J, K, F, D o haz clic. Cada toque se ajusta a la semicorchea más cercana y el
   medidor te muestra cuántos ms antes o después caíste. Ctrl+Z deshace la toma entera.
   Para un riff rápido: baja la velocidad a 0.5× o 0.75× y marca un bucle arrastrando en la
   regla; cada vuelta suma notas.
5. **Editar** (tecla 1):
   - *Línea de tiempo*: forma de onda arriba, notas abajo. Clic añade, arrastrar mueve, clic
     derecho borra, caja para seleccionar varias.
   - *Cuadrícula*: un compás por fila con sus 16 semicorcheas (1 e y a 2 e y a…). Clic o
     arrastrar pinta/borra. Las celdas naranjas son golpes fuertes de la canción. Clic derecho
     en el número del compás: escuchar en bucle, copiar, pegar, repetir en el siguiente.
   - Colores: rojo = en el beat, azul = corchea, amarillo = semicorchea.
6. **Simular** (tecla 3): se juega el chart con las mismas reglas que `BeatActionJudge`
   (la nota viaja 4 beats, zona verde = ventana de acierto, golpe al aire = penalización con
   bloqueo hasta la siguiente nota). Al final lista las notas perdidas para corregirlas.
   Jugarla entera desde el principio la marca como **simulada ✓**.
7. **Desplegar al juego**: la primera vez elige la carpeta
   `Assets/_Juego Actual/Resources/Charts`. Escribe `<escena>.json` y Unity lo recarga al
   volver a su ventana. «Abrir del juego» lee lo que el juego tiene ahora.

El trabajo se guarda solo en este navegador mientras editas. Exporta o despliega para
compartirlo.

## Formato del chart

```json
{
  "song": "Cementerio.mp3",
  "scene": "cementerio",
  "bpm": 124,
  "beatsPerMeasure": 4,
  "ticksPerBeat": 4,
  "offsetMs": 115,
  "notes": ["2:1:1", "2:1:3", "2:2:1"]
}
```

- Cada nota es `compás:beat:tick`, empezando en 1, con tick = semicorchea (1 a 4).
- La nota marca el momento en que **llega al corazón**, que es cuando suena en la canción.
  El juego ya la hace aparecer 4 beats antes; no hay que adelantarla a mano.
- `offsetMs` es dónde cae el compás 1:1:1 dentro del mp3. Valores medidos sobre los golpes de
  cada canción: Cementerio 115 ms, Iglesia 140 ms, Crisantia 10 ms, Mazmorra 0 ms.
- Si una escena no tiene su `.json`, el juego usa el patrón de intervalos de siempre.
