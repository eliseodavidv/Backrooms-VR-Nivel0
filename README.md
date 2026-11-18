# Backrooms VR · Nivel 0

Experiencia de terror en realidad virtual para Meta Quest 2, inspirada en los Backrooms.
El jugador despierta en un laberinto de pasillos idénticos y debe encontrar la salida en 8 minutos sin que una entidad lo atrape.

Curso CS2H01 Interacción Humano-Computador, UTEC.
Equipo: Alejandro Vargas Rios, Eliseo David Velásquez Díaz, Paolo Centeno.
Profesor: Teófilo Chambilla.

## Descripción
Juego de terror en realidad virtual para Meta Quest 2, inspirado en los Backrooms.
- Laberinto de 15x15 celdas con una sola salida y 8 minutos para encontrarla.
- 3 nichos para esconderse (cada uno con una puerta con mirilla).
- Una entidad con 4 estados de comportamiento: patrulla, sospecha, persecución y búsqueda (al atraparte pasa a captura y termina la partida).
- 5 mini monstruos (3 de vida cada uno) y 3 vidas del jugador, mostradas como corazones en la parte superior izquierda de la vista.
- Pistola con balas infinitas y barra de metal.

## Controles
- **Teletransporte:** arco de teletransporte con el control, giro por pasos de 45° y viñeta de comodidad.
- **Esconderse:** entrar a un nicho y agacharse (cabeza por debajo de 1,1 m). La puerta se cierra y queda una mirilla; la entidad y los mini monstruos pierden el rastro.
- **Pistola:** se agarra con controles, manos o a distancia; se dispara con el gatillo del índice.
- **Barra de metal:** se agarra igual y golpea a los mini monstruos.
- **Menú y pausa:** puntero láser.

## Requisitos
- Unity 2022.3.62f3 (render Built-in) con Android Build Support
- Meta XR SDK
- Meta Quest 2 con modo desarrollador activado

## Instalación y despliegue
1. Clonar el repositorio y abrir la carpeta con Unity 2022.3.62f3.
2. Abrir la escena `Assets/Backrooms_Main.unity`.
3. Cambiar la plataforma a Android (File > Build Settings > Android > Switch Platform).
4. File > Build Settings > Build para generar el APK.
5. Instalar el APK en el visor:
   - con Meta Quest Developer Hub (arrastrar el APK), o
   - con `adb install -r Builds/Backrooms_VR.apk`.

El APK no está en el repositorio porque pesa unos 84 MB (`Builds/` y `*.apk` están en `.gitignore`).

## Estructura
- `Assets/Scripts`: lógica del juego (GameManager, EntityAI, MiniMonstruo, NichoZona, PuertaNicho, IndicadorVidas, StatsLogger...) y `Editor/` con los constructores de escena.
- `Assets/Shaders`: shaders del HUD, decals y armas.
- `Assets/Resources`: biblioteca de sonidos, mezcla de audio, ajustes de Meta XR y los materiales y texturas de los corazones (`Vidas/`).
- `Docs`: guías del curso en PDF.
- `Auditoria`: informe de auditoría del informe frente a la implementación.

## Estadísticas
El juego guarda una fila por partida en `backrooms_stats.csv` con las columnas:
`fecha_utc, partida, resultado, causa, tiempo_s, detectado, teletransportes, escondites, mini_monstruos_muertos, voto`

El archivo se crea en `Application.persistentDataPath`. En el Quest es:
`/sdcard/Android/data/com.DefaultCompany.VRMetaQuest02-Eliseo/files/backrooms_stats.csv`
(se puede copiar con `adb pull`). En Windows, en el Editor: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\VRMetaQuest02_Eliseoackrooms_stats.csv`.

## Equipo
Alejandro Vargas Rios, Eliseo David Velásquez Díaz, Paolo Centeno.
Curso CS2H01 Interacción Humano-Computador, UTEC. Profesor: Teófilo Chambilla.

Nota: algunas carpetas de modelos de otra escena (`Planta-Andahuasi` y `Texturas-adicionales`) no se subieron por su tamaño y no se usan en el juego.

## Créditos de modelos (Unity Asset Store)
- Pistol 92 (StarGames studio)
- Crowbar Free (Undertaker3D)
- Creep Horror Creature (AC Game Assets)
- Book Head Monster (h3llowner)

## Créditos de sonido (Pixabay)
Todos los sonidos reales (en `Assets/Audio/Reales/`) vienen de **Pixabay (Pixabay Content License)**. Archivos originales:
- `freesound_community-low-monster-roar-97413` (rugido de la entidad)
- `freesound_community-single-pistol-gunshot-33-37187` (disparo de la pistola)
- `incesante-metalic-hit-479257` (golpe de la barra)
- `freesound_community-tiny-vicious-creature-47253` (mini monstruos)
- `lenspulse-intense-cinematic-heartbeat-sound-effect-584627` (latido)
- `freesound_community-039888_faulty-fluorescent-light-starter-amp-humwav-61228` (zumbido de luces)
- `fronbondi_skegs-drone-dark-mechanical-grinding-ambient-background-522545` (drone de fondo)
- `universfield-male-death-scream-123078` (grito de muerte)

Autor y enlace de cada uno: pendientes de completar en `Assets/Audio/Reales/CREDITOS_AUDIO.txt`.
