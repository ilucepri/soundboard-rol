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
4. **En Discord** → Ajustes → Voz y vídeo → Dispositivo de entrada → el mix de Wave Link.

> Si algún día no usas Wave Link, sirve igual cualquier cable virtual: VB-Cable
> (`CABLE Input`) o VoiceMeeter. Sólo cambia qué eliges en **EMISIÓN**.

**Comprobación rápida:** con EMISIÓN puesta, dispara un pad. En Wave Link deberías ver moverse
el medidor del canal SFX. Si se mueve, en Discord se oye.

---

## Uso

| Acción | Cómo |
| --- | --- |
| Reproducir | Click en el pad |
| Parar ese sonido | Click otra vez en el mismo pad |
| Parar todo | Botón **PARAR TODO** |
| Añadir sonidos | Botón **+ Añadir sonidos**, o arrastrar ficheros a la ventana |
| Volumen de un sonido | El deslizador de dentro del pad (se puede mover mientras suena) |
| Renombrar / color / bucle / quitar | Click derecho en el pad |
| Nuevo perfil | El **+** de la barra lateral |
| Renombrar / icono / borrar perfil | Click derecho en el perfil |

El **bucle** (click derecho → *Reproducir en bucle*) es para música de ambiente: se repite hasta
que vuelves a pulsar el pad. Los pads en bucle llevan un `↻` en la esquina.

Borrar un pad o un perfil **no borra el fichero de audio**, sólo la referencia.

### Formatos

WAV y MP3 nativamente; M4A, AAC, WMA y FLAC a través de Media Foundation de Windows.
OGG y Opus normalmente **no** funcionan salvo que tengas el códec instalado.

---

## Dónde se guarda la configuración

`%APPDATA%\SoundboardRol\` — dos JSON legibles:

- `profiles.json` — perfiles y pads (guarda **rutas** a los ficheros, no los copia)
- `settings.json` — dispositivos y volúmenes elegidos

Se puede cambiar con la variable de entorno `SOUNDBOARD_DATA_DIR`, útil para llevártelo en un
pendrive o para tener un juego de perfiles de pruebas.

---

## Compilar y ejecutar

```bash
dotnet run --project src/Soundboard
```

Para generar un ejecutable suelto (~2 MB, necesita el runtime de .NET 10 Desktop instalado):

```bash
dotnet publish src/Soundboard -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true
```

Queda en `src/Soundboard/bin/Release/net10.0-windows/win-x64/publish/Soundboard.exe`.

Ojo: `-p:SelfContained=false` como propiedad, no `--self-contained false`; con el flag el SDK
acaba generando igualmente un self-contained de ~158 MB. Si lo que quieres es justo eso, un
ejecutable que funcione en un PC sin .NET instalado, usa `-p:SelfContained=true`.

---

## Cómo está montado

```
src/Soundboard/
  Audio/          Motor de audio, independiente de la UI
    AudioFormat        Formato del mezclador (48 kHz estéreo float)
    SoundSource        Carga de ficheros: cachea en RAM si dura <20 s, si no lee del disco
    Voice              Una reproducción: ganancia y rampa de salida para que no chasquee
    OutputBus          Una salida física: WASAPI + mezclador + volumen maestro
    AudioEngine        Las dos salidas y el reparto de disparos entre ellas
    AudioDeviceService Enumeración de dispositivos de Windows
  Models/         Perfiles, pads y preferencias (lo que se serializa)
  Services/       Persistencia en JSON y diálogos
  ViewModels/     MVVM con CommunityToolkit.Mvvm
  Views/          Conversores y diálogo de texto
  Themes/Dark.xaml
```

La regla que sostiene todo: `Audio/` no sabe nada de WPF. El motor se puede probar sin abrir
ninguna ventana.

---

## Pendiente

- Atajos de teclado globales (la arquitectura está preparada: un disparo es
  `PadViewModel.TriggerCommand`, no hace falta tocar el motor)
- Fundido de entrada/salida configurable para la música de ambiente
- Recortar el trozo del fichero que suena
- Buscador de pads e importación de carpetas enteras
- Reordenar pads arrastrando
- Ducking: bajar la música mientras hablas
