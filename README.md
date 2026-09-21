# Soundboard de rol

Soundboard para Windows pensado para partidas de rol por Discord. Perfiles por personaje
(bardo, mago, ambiente...), pads con click, y **salida doble**: el sonido va a la vez al canal
que oyen los demás y a tus cascos, con volúmenes independientes.

C# / .NET 10 / WPF / NAudio.

---

## Cómo llega el sonido a Discord

No hace falta ningún driver ni cable virtual extra: **Wave Link ya publica un dispositivo de
salida de Windows por cada canal de su mezclador**.

```
Soundboard  ──[EMISIÓN]──►  "Wave Link SFX"  ─┐
                                              ├─► Mix de Wave Link ──► Discord (entrada)
Micro Wave:3 ────────────────────────────────┘

Soundboard  ──[MONITOR]──►  tus cascos   (sólo lo oyes tú)
```

### Puesta a punto (una vez)

1. **En la app**, arriba: pon **EMISIÓN** en `Wave Link SFX (Elgato Wave:3)`.
   La app intenta detectarlo sola en el primer arranque.
2. **En la app**: pon **MONITOR** en tus cascos, para oírte los efectos tú también.
   Bájale el volumen a gusto; no afecta a lo que oyen los demás.
3. **En Wave Link**: asegúrate de que el canal **SFX** está incluido en el mix que usas para
   la voz, y de que tu micro también está en ese mix.
4. **En Discord** → Ajustes → Voz y vídeo → Dispositivo de entrada → el mix de Wave Link
   (`Wave Link MicrophoneFX`).
5. **Supresión de ruido: en Wave Link, no en Discord.** Discord recibe la mezcla ya hecha y
   no distingue tu voz de los efectos, así que su supresión de ruido (sobre todo Krisp) se come
   la música y los efectos. Pon **Supresión de ruido → Ninguna** en Discord y añade el filtro
   *NVIDIA Noise Removal* (o *Noise Removal* de Elgato si no hay GPU RTX) en los efectos del
   **canal del micro** en Wave Link. Así se limpia sólo tu voz antes de mezclarla con los efectos.

> Si algún día no usas Wave Link, sirve igual cualquier cable virtual: VB-Cable
> (`CABLE Input`) o VoiceMeeter. Sólo cambia qué eliges en **EMISIÓN**.

Sin salida de emisión la app avisa con una banda roja bajo la barra de dispositivos: sin eso
no llega nada a Discord.

**Comprobación rápida:** con EMISIÓN puesta, dispara un pad. En Wave Link deberías ver moverse
el medidor del canal SFX. Si se mueve, en Discord se oye.

---

## Uso

| Acción | Cómo |
| --- | --- |
| Reproducir | Click en el pad, o su tecla de atajo |
| Parar ese sonido | Click otra vez en el mismo pad |
| Parar todo | Botón **Parar todo**, o `Esc` |
| Añadir sonidos | Botón **Añadir sonidos**, o arrastrar ficheros a la ventana |
| Volumen de un sonido | El deslizador de dentro del pad (se puede mover mientras suena) |
| Editar un sonido | Click derecho → **Editar sonido…** |
| Buscar | El campo de la cabecera; filtra el perfil activo por nombre |
| Nuevo perfil | El **+** de la barra lateral |
| Editar / duplicar / borrar perfil | Click derecho en el perfil |
| Ver todos los ficheros conocidos | **Biblioteca**, al pie de la barra lateral |

### El editor de sonido

Click derecho en un pad → *Editar sonido…*. Desde ahí:

- **Nombre** y **atajo de teclado** (una tecla; funciona con la ventana en primer plano).
- **Recorte**: los dos deslizadores bajo la forma de onda eligen qué trozo del fichero suena.
  Las barras fuera del recorte se apagan. Útil para quedarte con el golpe de un efecto y tirar
  el silencio de delante.
- **Volumen**, **color** de la paleta y **bucle**.

El **bucle** es para música de ambiente: se repite hasta que vuelves a pulsar el pad. Los pads
en bucle llevan el símbolo de repetición en la esquina.

Borrar un pad o un perfil **no borra el fichero de audio**, sólo la referencia.

### Atajos de teclado

Funcionan con la ventana en primer plano y se ignoran mientras escribes en un campo de texto
o hay un diálogo abierto.

- La tecla asignada a un pad lo dispara (se ve en la esquina del pad).
- `Esc` para todo.

> Para que funcionaran con la ventana en segundo plano haría falta registro global
> (`RegisterHotKey` de Win32). No está hecho.

### Formatos

WAV y MP3 nativamente; M4A, AAC, WMA y FLAC a través de Media Foundation de Windows.
OGG y Opus normalmente **no** funcionan salvo que tengas el códec instalado.

---

## Dónde se guarda la configuración

`%APPDATA%\SoundboardRol\` — dos JSON legibles:

- `profiles.json` — perfiles y pads (guarda **rutas** a los ficheros, no los copia)
- `settings.json` — dispositivos y volúmenes elegidos

Si algo falla en la interfaz, la app no se cierra: escribe el error en `crash.log`, en esa misma
carpeta, y sigue abierta.

Se puede cambiar la carpeta con la variable de entorno `SOUNDBOARD_DATA_DIR`, útil para
llevártelo en un pendrive o para tener un juego de perfiles de pruebas.

---

## Compilar y ejecutar

```bash
dotnet run --project src/Soundboard
```

Para generar un ejecutable suelto (~2 MB, necesita el runtime de .NET 10 Desktop instalado):

```bash
dotnet publish src/Soundboard -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -o dist
```

Ojo: `-p:SelfContained=false` como propiedad, no `--self-contained false`; con el flag el SDK
acaba generando igualmente un self-contained de ~158 MB. Si lo que quieres es justo eso, un
ejecutable que funcione en un PC sin .NET instalado, usa `-p:SelfContained=true`.

El acceso directo del escritorio apunta a `dist\Soundboard.exe`, así que **después de tocar el
código hay que volver a publicar ahí** para que el icono abra la versión nueva.

---

## Cómo está montado

```
src/Soundboard/
  Audio/          Motor de audio, independiente de la UI
    AudioFormat        Formato del mezclador (48 kHz estéreo float)
    SoundSource        Carga de ficheros, recorte y picos para la forma de onda.
                       Cachea en RAM si dura <20 s; si no, lee del disco
    Voice              Una reproducción: ganancia, posición y rampa de salida
    OutputBus          Una salida física: WASAPI + mezclador + volumen maestro
    AudioEngine        Las dos salidas y el reparto de disparos entre ellas
    PlaybackHandle     Un disparo: agrupa sus dos voces, expone progreso y parada
    AudioDeviceService Enumeración de dispositivos de Windows
  Models/         Perfiles, pads y preferencias (lo que se serializa)
  Services/       Persistencia en JSON y diálogos
  ViewModels/     MVVM con CommunityToolkit.Mvvm
  Views/          Diálogos, conversores, la rejilla de pads y el arreglo de maximizado
  Themes/Nocturne.xaml   Tokens y estilos del sistema de diseño
```

La regla que sostiene todo: `Audio/` no sabe nada de WPF. El motor se puede probar sin abrir
ninguna ventana.

### El sistema de diseño

La interfaz sigue el handoff de **Nocturne**. Los tokens (color, tipografía, radios, sombras)
viven todos en `Themes/Nocturne.xaml`; no hay colores sueltos por el XAML.

Tres sustituciones respecto al prototipo, todas contempladas en el propio handoff:

- **Inter → Segoe UI Variable.** No hacía falta empaquetar una fuente.
- **Phosphor → Segoe Fluent Icons.** Viene con Windows 11.
- **Sin `letter-spacing`.** WPF no sabe hacer tracking en `TextBlock`, así que las etiquetas
  de 9,5 px en mayúsculas van sin el `.12em` del diseño.

Y una limitación de la plataforma: WPF no pinta emoji a color, así que los iconos de perfil
salen monocromos.

---

## Pendiente

- Atajos globales, que funcionen con la app en segundo plano (`RegisterHotKey`)
- Fundido de entrada/salida configurable para la música de ambiente
- Reordenar pads arrastrando
- Importar carpetas enteras de una vez
- Ducking: bajar la música mientras hablas
