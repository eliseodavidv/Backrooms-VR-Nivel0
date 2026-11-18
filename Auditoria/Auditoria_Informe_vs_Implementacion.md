# Auditoría: informe vs. implementación (Backrooms VR – Nivel 0)

Fecha de la auditoría: 2026-11-11 (hora local). Solo lectura: no se modificó ningún archivo del proyecto salvo este.

## 0. Alcance y límites (leer primero)

- **No tengo el informe.** El texto de los requisitos es el que pegaste en el mensaje (resumen de RF-01…RF-20 y RNF-01…RNF-08). Donde digo "frase del informe" me refiero a ese resumen. RNF-01 no traía texto salvo "72 FPS".
- **No probé nada en el visor.** Todo se basa en código (`Assets/Scripts/*.cs`), en la escena `Assets/Backrooms_Main.unity` leída desde el Editor (sin Play) y en el CSV del Editor. Lo que solo se ve en el Quest 2 figura como NO VERIFICABLE.
- Las rutas de código son relativas a `Assets/Scripts/`. Formato `archivo:línea`.
- Valores de la escena: se leyeron los valores serializados reales (que pueden diferir del valor por defecto del script; se indica cuando difieren).

## 1. Tabla resumen por requisito

| ID | Estado | Evidencia | Corrección sugerida al informe |
|---|---|---|---|
| RF-01 | IMPLEMENTADO | `UI_E4/Panel_Menu`: botones COMENZAR, TUTORIAL, OPCIONES, CRÉDITOS, SALIR enlazados a `GameManager.Comenzar/AbrirTutorial/AbrirOpciones/AbrirCreditos/Salir` (`GameManager.cs:216-246,402`). Láser: `LaserPointer.cs:66-67,103` (gatillo `PrimaryIndexTrigger`). Volumen: `GameManager.cs:456-480`, panel `Panel_Opciones` (botones − / +). | Aclarar que el puntero láser funciona **solo con controles**, no con manos (`LaserPointer.cs:103`). |
| RF-02 | IMPLEMENTADO | `Panel_Tutorial` con 4 pasos: 1 "Stick + gatillo / Apunta y suelta para teletransportarte", 2 "Agáchate / Entra a un nicho para esconderte", 3 "Mira tu muñeca / El reloj muestra el tiempo restante", 4 "Escucha / El zumbido más fuerte guía a la salida". Opcional: `GameManager.cs:229-251` (COMENZAR lo muestra solo la primera vez, `PlayerPrefs backrooms_tutorial_visto`; botón TUTORIAL siempre; "Omitir" vuelve al menú). | Decir "se muestra la primera vez y se puede repetir desde TUTORIAL". El paso 1 dice "gatillo": no pude comprobar con qué botón del SDK se activa el arco (ver RF-03). |
| RF-03 | IMPLEMENTADO (SDK de Meta) | Escena: `TeleportInteractor` + `TeleportArcGravity` + `TeleportProceduralArcVisual` + `TeleportReticleDrawer` en ambos controles y en ambas manos; 141 hotspots (`Hotspots`: 138 + 3 `TeleportHotspot_Nicho_*`). Filtro propio `TeleportReachGate.cs:1-12`. | Añadir que un destino solo es válido si hay línea de vista o camino corto por el NavMesh (`TeleportReachGate`). Botón exacto que activa el arco: NO VERIFICABLE en código propio (lo define el SDK). |
| RF-04 | IMPLEMENTADO (SDK de Meta) | `LocomotionAxisTurnerInteractor` en ambos controles (`_deadZone=0.8`) y `LocomotionTurnerInteractor` en ambas manos. | El ángulo de cada paso lo fija el SDK; no lo pude leer. No citar un ángulo concreto sin medirlo en el visor. |
| RF-05 | IMPLEMENTADO | `Editor/BackroomsMazeBuilder.cs:22-38` (mapa 15×15), `:41` inicio (0,0), `:42` salida (14,14). Escena: 135 `Celda_*` + 3 `Nicho_*` = 138 celdas, un solo `Laberinto/Salida/Puerta_Luz` (`GameManager.salida`). Módulos repetidos: prefabs `Pasillo_Modulo` y `Pasillo_Nicho`. | Ninguna. |
| RF-06 | IMPLEMENTADO (con un 5.º estado) | `EntityAI.cs:10` `Patrulla, Sospecha, Persecucion, Busqueda, Captura`. Captura es terminal (fin de partida). | Decir "4 estados de comportamiento más el estado de captura". |
| RF-07 | IMPLEMENTADO (frase imprecisa) | Visión: `EntityAI.cs:18` (8 m), línea de vista `:308-319`, **cono de 100°** `:298-306`. Entre 8 y 16 m (`:20`) y dentro del cono pasa a **Sospecha**, no a Persecución (`:109-110`). Ruido: `:321-332` (2 teletransportes en 8 s con destino a ≤15 m ⇒ Sospecha; ignorado si ya persigue). | "Persecución si el jugador está a menos de 8 m, dentro del cono de visión (100°) y con línea de vista. A 8–16 m lo ve y sospecha. Dos teletransportes en 8 s a menos de 15 m provocan sospecha (no persecución)." |
| RF-08 | IMPLEMENTADO (frase inexacta) | `NichoZona.cs:22-34`, `EntityAI.cs:99-101,132,138`. Ver Parte A. | Ver Parte A. |
| RF-09 | IMPLEMENTADO (audio generado, no real) | `ExitHum.cs:34-46` (3D, `distanciaMaxima` 40, volumen 0,3→1 al acercarse). `EntityAudio.cs:47-75` (pasos según velocidad; respiración según estado), ambos 3D. En `Assets/Audio/Reales` **no** hay archivos de pasos, respiración ni zumbido de salida: usan los sonidos generados por código (`SonidosPlaceholder`). | Si el informe cuenta "8 sonidos reales", estos tres no están entre ellos. |
| RF-10 | IMPLEMENTADO | Escena: `RelojMuneca` bajo `LeftControllerAnchor`. `GameManager.duracion = 480` (`GameManager.cs:74`; valor de escena 480). `WristClock.cs:16-21` formato mm:ss. | Está anclado al **anclaje del control izquierdo**; con manos sin controles no pude comprobar que se vea (NO VERIFICABLE). |
| RF-11 | IMPLEMENTADO | `PursuitFeedback.cs`: borde rojo `:119-124`, latido `:94-106,127-137` (acelera y sube con el peligro), vibración `:135,142` y `EntityProximityCue.cs`. | La vibración es solo en controles (`ControllerHaptics`). |
| RF-12 | IMPLEMENTADO | Victoria a ≤1,2 m de `Puerta_Luz`: `GameManager.cs:198-201`. Derrota por tiempo `:193-196`, por captura `:533-536`, por monstruo `:544-547`. | Añadir la tercera causa de derrota: "devorado por los mini monstruos". |
| RF-13 | IMPLEMENTADO (nombre distinto) | Panel de fin: resumen, detalle, voto ME GUSTA / NO ME GUSTA, **INICIAR DE NUEVO** (derrota) / **JUGAR DE NUEVO** (victoria) y MENÚ (`GameManager.cs:318-346`). Objeto: `Panel_Fin/Boton_IniciarDeNuevo` → `GameManager.IniciarDeNuevo` (`:350`). `Reintentar()` existe solo como alias sin uso en la UI (`:389`). | Cambiar "Reintentar" por "Iniciar de nuevo" (en victoria el botón dice "Jugar de nuevo"). |
| RF-14 | IMPLEMENTADO (con huecos) | `StatsLogger.cs:12,35`. Ver Parte A. | Ver Parte A. |
| RF-15 | IMPLEMENTADO | 3 mesas: `Mesa_1` barra (−1,13; −3), `Mesa_2` pistola (−1,13; −6), `Mesa_3` pistola (7; −30). Cada arma tiene 4 interactables: `GrabInteractable` (controles), `HandGrabInteractable` (manos), `DistanceGrabInteractable` y `DistanceHandGrabInteractable` (a distancia). Base: `ArmaAgarrable.cs:6-12,116-144`. | Precisar "una arma por mesa". |
| RF-16 | IMPLEMENTADO | Barra: `BarraMetal.cs:16` (2,2 m/s), tope 15 m/s `:17`, daño 1 `:15`, enfriamiento 0,45 s por blanco `:19`, vibración `:85`. Pistola: sin munición ni recarga (`Pistola.cs:3-4`), daño 2 `:15`, cadencia **0,25 s en la escena** (valor por defecto del script 0,2, `:18`), sonido `:87`, vibración `:88`. Un disparo por pulsación (`GatilloPulsado` usa `GetDown`, `ArmaAgarrable.cs:154-167`). | "Barra: daña si la punta supera 2,2 m/s (y no más de 15 m/s)". "Pistola: sin límite de balas, 0,25 s mínimo entre disparos". |
| RF-17 | IMPLEMENTADO | 5 `MiniMonstruo`, `vidaMax = 3` (`MiniMonstruo.cs:13`). Barra los mata en 3 golpes, pistola en 2 disparos. La entidad es intocable: `ArmaAgarrable.cs:252-255`, `Pistola.cs:70`, `BarraMetal.cs:66,87-91`. | Ninguna. |
| RF-18 | IMPLEMENTADO (el informe es exacto) | `SaludJugador.cs:8-9,78-94`. Ver Parte A. | Opcional: aclarar que la captura de la entidad principal mata de golpe (no quita una vida). |
| RF-19 | IMPLEMENTADO | `GameManager.cs:350-387`: recarga la escena (`SceneManager.LoadScene`) y arranca directo (`EmpezarDirecto`, `:151-157`). Reinicio de todo: `ReiniciarComponentes` (`:288-296`). | Aclarar que el volumen y "tutorial visto" se conservan (PlayerPrefs, `GameManager.cs:138,250`). |
| RF-20 | PARCIAL (frase) | Estilo común TMP en Menú, Opciones, Créditos, Pausa y Fin. `TituloFluorescente` solo en: `Panel_Menu/TituloGrupo/Titulo` (con glitch), `Panel_Opciones/Titulo`, `Panel_Creditos/Titulo`, `Panel_Pausa/Titulo` (+ etiquetas de volumen). **`Panel_Fin` no parpadea.** `Panel_Tutorial` usa `UnityEngine.UI.Text` (no TMP). | "El título BACKROOMS parpadea en el menú principal; Opciones, Créditos y Pausa tienen su propio título (OPCIONES, CRÉDITOS, PAUSA) con el mismo parpadeo. La pantalla de fin y el tutorial no parpadean." Que el tutorial tenga el mismo estilo visual: NO VERIFICABLE por código (usa otro tipo de texto). |
| RNF-01 | NO VERIFICABLE EN CÓDIGO | No hay medición de FPS en el código. Contexto: 3 luces en tiempo real, 59 horneadas. | Presentar los 72 FPS como objetivo, no como medida, salvo que exista una captura del perfilador en el Quest. |
| RNF-02 | PARCIAL / NO VERIFICABLE | Prefab de Meta `TunnelingEffect` presente y activo en la escena (`Backrooms_Main.unity:40526` y `:40617`, `LocomotionTunneling`). No hay locomoción continua: solo teletransporte y giro por pasos. `Vineta_Menu` es otra cosa (atmósfera del menú, inactiva en partida). | Decir "viñeta de confort del SDK de Meta durante los movimientos de locomoción", y comprobar en el visor que se ve con teletransporte y giro. |
| RNF-03 | NO CUMPLIDO COMO ESTÁ ESCRITO / NO VERIFICABLE | Duración **máxima** 480 s (`GameManager.duracion`). Nada fuerza un mínimo de 5 min. Camino mínimo inicio→salida: 32 celdas = 96 m. En el CSV las 7 victorias duran entre 1,6 s y 78,5 s (partidas de prueba). | "La partida dura como máximo 8 minutos." El "5 a 8" solo vale como estimación si hay pruebas con usuarios. |
| RNF-04 | IMPLEMENTADO (legibilidad NO VERIFICABLE) | Menú, Opciones, Créditos, Pausa y Fin a 2 m (`distanciaMenu = 2`); Tutorial a 1,5 m (`distanciaPaneles`, `GameManager.cs:611`). Escala de panel 0,002 (tutorial 0,0015). | "Menús a 1,5–2 m". Legibilidad real en el visor: no verificable. |
| RNF-05 | NO VERIFICABLE EN CÓDIGO | Hay tutorial de 4 pasos y rótulos sobre las mesas ("PISTOLA", "BARRA" con instrucciones). | Necesita prueba con usuarios para afirmar "en 30 s". |
| RNF-06 | PARCIAL | Ver tabla RNF-06 abajo. | Ver tabla. |
| RNF-07 | PARCIAL | Solo teletransporte + giro por pasos (sin locomoción continua), `HeadWallGuard.cs` evita atravesar paredes. Pero esconderse exige agacharse físicamente (altura de cabeza ≤1,1 m) y las armas están en mesas (hay agarre a distancia como alternativa). | "Se juega de pie y casi estacionario; esconderse requiere agacharse." |
| RNF-08 | IMPLEMENTADO | Volumen 0–100 % en pasos de 10 (`GameManager.cs:456-470`), guardado en PlayerPrefs. Pausa: botón Start del control (`GameManager.cs:181`) funciona en cualquier fase; congela tiempo y audio (`:430-435`). | Aclarar que la pausa es el botón de menú del control izquierdo (con manos solas no hay pausa propia). |

### RNF-06: alertas sonoras y su equivalente visual o háptico

| Alerta sonora | Equivalente | ¿Cubierto? |
|---|---|---|
| Latido en persecución | Borde rojo que pulsa + vibración (`PursuitFeedback.cs:119-142`) | Sí |
| Rugido de la entidad al detectar | Aparece el borde rojo al instante (mismo cambio de estado) | Sí |
| Pasos y respiración de la entidad (patrulla/sospecha/búsqueda) | Pulsos hápticos que crecen al acercarse (`EntityProximityCue.cs`) | Sí (solo háptico; solo controles) |
| Zumbido de la salida | La puerta de luz pulsa más fuerte y rápido (`ExitHum.cs:40-45`) | Sí |
| Mordisco de mini monstruo | Destello rojo + vibración (`SaludJugador.cs:85-90`) | Sí |
| Disparo / golpe de barra | Trazador y destello (pistola) + vibración (`Pistola.cs:77-88`, `BarraMetal.cs:85`) | Sí |
| Grito al ser devorado / sonido de derrota | Pantalla de derrota | Sí |
| **Susurros espaciales** (`AmbienteJuego.cs:75-85`) | Ninguno | **No** |
| **Mini monstruos acercándose** (gritan solo al morder o morir, `MiniMonstruo.cs:201,219,231`) | Ninguno antes del mordisco | **No** |
| Zumbido/chasquido de luces fluorescentes | El propio parpadeo de la luz | Sí (es visual en sí) |
| Whoosh al teletransportarse | El arco y la reticle del teletransporte | Sí |
| Drone de fondo | No es alerta | n/a |

## 2. Parte A: respuestas concretas

### RF-08 – Esconderse

**¿Existe la detección?** Sí. Scripts: `NichoZona.cs` (volumen del nicho) y `EntityAI.cs` (reacción).

**Cómo detecta que está dentro:** `NichoZona.ContieneHorizontal` (`NichoZona.cs:22-26`) comprueba que la posición de la cabeza (CenterEyeAnchor) esté dentro del `BoxCollider` del nicho **solo en X y Z**. El volumen mide 1,30 × 2,70 × 1,50 m.

**Cómo detecta que está agachado:** `NichoZona.JugadorEscondido` (`NichoZona.cs:28-34`) devuelve `false` si `cabeza.position.y > alturaAgachado`. `alturaAgachado = 1,1 m` (`EntityAI.cs:33`; valor de escena 1,1). Es una **altura absoluta del mundo**, no relativa a la estatura del jugador.

**Cuántos nichos y dónde (escena):** 3, objetos `Laberinto/Modulos/Nicho_*/Nicho/ZonaEscondite`:
- `Nicho_4_4` en (14,2; 1,35; −12,0)
- `Nicho_0_8` en (2,2; 1,35; −24,0)
- `Nicho_6_12` en (20,2; 1,35; −36,0)

Declarados en `Editor/BackroomsMazeBuilder.cs:43`.

**Qué hace la entidad cuando está escondido:**
- Mientras está escondido, `lineaDeVision` es siempre falso (`EntityAI.cs:99-101`): la entidad **no lo ve**, haya visto o no cómo entraba. La condición del informe "si no lo ve entrar" **no existe** en el código.
- Si lo perseguía: pasa a **Búsqueda** con el motivo "jugador escondido" (`:136-139`), va a la última posición que conocía (`:186`) y rastrea alrededor durante 10 s (`:147-153`, `:276-287`), luego vuelve a Patrulla.
- **No puede capturarlo escondido**: la captura exige `!jugadorEscondido` (`:132`). El nicho es "Not Walkable" en el NavMesh (`NichoZona.cs:5`; `Editor/BackroomsMazeBuilder.cs:276`), así que la entidad no entra.
- En cuanto el jugador se levanta (cabeza >1,1 m) o sale, y está a menos de 8 m, en cono y con línea de vista, vuelve a perseguir (`:148-149`).
- **Excepción importante:** los **mini monstruos ignoran el escondite**. `MiniMonstruo.Ve()` (`MiniMonstruo.cs:149-162`) no consulta `NichoZona`; pueden verlo y morderlo dentro del nicho.

**¿Cuenta el contador `escondites`?** Sí: `GameManager.cs:189-191` incrementa `escondites` cada vez que se pasa de no escondido a escondido durante la partida (cuenta entradas, estén o no persiguiéndolo). Se guarda en la columna `escondites` del CSV. En las 61 filas actuales solo la fila 1 tiene un valor distinto de 0 (no hay evidencia suficiente para decir que falla; son pruebas).

**Veredicto: IMPLEMENTADO, pero la frase del informe no es exacta.**
Frase propuesta: *"El jugador se esconde entrando a un nicho y agachándose físicamente (cabeza por debajo de 1,1 m). Mientras está escondido la entidad no lo ve ni puede capturarlo: si lo perseguía, pierde el rastro y pasa a buscarlo durante 10 s hacia donde lo vio por última vez. Los mini monstruos sí pueden atacarlo."*

### RF-14 – Estadísticas en archivo

**¿Se escribe un archivo?** Sí.
- Ruta definida en `StatsLogger.cs:12`: `Path.Combine(Application.persistentDataPath, "backrooms_stats.csv")`.
- **Editor de Windows** (`companyName = DefaultCompany`, `productName = VRMetaQuest02_Eliseo`, `ProjectSettings.asset:15-16`): `C:\Users\LABORATORIO-L507\AppData\LocalLow\DefaultCompany\VRMetaQuest02_Eliseo\backrooms_stats.csv` (existe, 61 filas de datos).
- **Quest 2** (identificador Android `com.DefaultCompany.VRMetaQuest02_Eliseo`, leído desde `PlayerSettings`): `/storage/emulated/0/Android/data/com.DefaultCompany.VRMetaQuest02_Eliseo/files/backrooms_stats.csv`. Esta ruta es la que Unity usa normalmente para `persistentDataPath` en Android; **no la verifiqué en un visor**.

**Encabezado y columnas** (`StatsLogger.cs:9`):
```
fecha_utc,partida,resultado,causa,tiempo_s,detectado,teletransportes,escondites,mini_monstruos_muertos,voto
```
Hay migración automática de un CSV antiguo sin `mini_monstruos_muertos` (`StatsLogger.cs:48-60`).

**Cuándo se escribe una fila** (`StatsLogger.Guardar`, `StatsLogger.cs:14-35`):
- Al terminar la partida: `GameManager.Terminar` (`GameManager.cs:312`). Causas: `salida` (victoria), `captura`, `monstruo`, `tiempo`.
- **Tiempo agotado: sí** (`GameManager.cs:193-196` → `derrota,tiempo`; hay una fila así, la 46, de 480,0 s).
- Al pulsar INICIAR DE NUEVO con la partida aún en curso o en pausa (`GameManager.cs:365-369`, causa `reinicio`). **Pero esa rama no es alcanzable desde la interfaz actual:** `IniciarDeNuevo` solo está enlazado a `Panel_Fin/Boton_IniciarDeNuevo`, cuando la fase ya es Fin (la fila ya se guardó al terminar). El panel de pausa no tiene ese botón.
- **NO se guarda al salir por el menú de pausa.** `Panel_Pausa/Boton_MenuPrincipal` llama a `GameManager.MostrarMenu` (`GameManager.cs:206-212`), que no escribe nada: una partida abandonada así se pierde del CSV. Tampoco se guarda con SALIR (`GameManager.cs:402-410`).

**Voto:** sí, en la **misma fila** de la partida. `Terminar` escribe la fila con `voto = ninguno` (`StatsLogger.cs:34`). Al pulsar ME GUSTA / NO ME GUSTA, `GameManager.Votar` (`GameManager.cs:391-397`) llama a `StatsLogger.ActualizarUltimoVoto` (`StatsLogger.cs:64-82`), que reescribe la última columna de la **última fila** del archivo. Valores: `me_gusta`, `no_me_gusta`, `ninguno`.

**Filas ahora en el archivo del Editor:** 61 de datos (62 líneas con encabezado). Composición:

| resultado / causa | filas |
|---|---|
| derrota / captura | 26 |
| derrota / monstruo | 26 |
| victoria / salida | 7 |
| derrota / tiempo | 1 |
| derrota / reinicio | 1 |

Votos: 57 `ninguno`, 3 `me_gusta`, 1 `no_me_gusta`. **Son datos de pruebas de desarrollo en el Editor**, no de usuarios: las fechas van del 2026-10-15 al 2026-11-12 (UTC), varias partidas duran segundos y hay 3 filas con `tiempo_s = 0,0` y causa `monstruo` (filas 29, 33, 37) que no sé explicar (podrían ser pruebas que llamaron a `DerrotaPorMonstruo` directamente). Las filas 48 y 49 (derrota por captura de 32,0 s y victoria de 1,6 s) son de la sesión de capturas de pantalla de hoy, por coincidencia exacta de valores y hora. Si usas estadísticas en el informe, **hay que usar el archivo copiado del Quest, no este**.

**Cómo copiar el archivo del Quest a la PC:**
- **adb:** con el visor conectado y la depuración USB activa:
  `adb pull /storage/emulated/0/Android/data/com.DefaultCompany.VRMetaQuest02_Eliseo/files/backrooms_stats.csv .`
- **Meta Quest Developer Hub (File Manager):** Device Manager → pestaña File Manager → `Android/data/com.DefaultCompany.VRMetaQuest02_Eliseo/files/` → seleccionar `backrooms_stats.csv` → Download.
- El archivo solo existe después de terminar al menos una partida en el visor. No probé ninguno de los dos métodos en el dispositivo.

**Veredicto: IMPLEMENTADO**, con dos huecos: no se registra el abandono desde la pausa y el valor `reinicio` es inalcanzable desde la interfaz.

### RF-18 – Vidas

**Todas las formas de perder una vida o morir:**

1. **Mordisco de mini monstruo** (única forma de perder *una vida*):
   - Daño: 1 vida (`SaludJugador.cs:78-84`, `Vida--`).
   - Frecuencia: `invulnerabilidad = 1,5 s` (`SaludJugador.cs:9`, escena 1,5). El mini además se detiene 0,8 s tras morder (`MiniMonstruo.cs:29,200`). Distancia de mordisco: 0,75 m en horizontal (`:28`). Entre mordiscos efectivos pasan ≥1,5 s.
   - Con `Vida <= 0` ⇒ `GameManager.DerrotaPorMonstruo` (`SaludJugador.cs:94`; `GameManager.cs:544-547`) ⇒ derrota con causa `monstruo`.
   - `vidaMax = 3` (`SaludJugador.cs:8`, escena 3).
2. **Captura de la entidad principal:** **mata de golpe**, no quita una vida. `EntityAI.Capturar` (`EntityAI.cs:206-215`) a ≤1 m (`:132`, `distanciaCaptura = 1`) o ≤2 m si llegó al final del camino (nicho). ⇒ `GameManager.AlCapturar` ⇒ derrota `captura` (`GameManager.cs:533-536`).
3. **Tiempo agotado:** 480 s ⇒ derrota `tiempo` (`GameManager.cs:193-196`).
4. **Otras:** ninguna (no hay caídas, trampas ni daño ambiental). Abandonar no cuenta como muerte.

**¿Hay indicador de vidas en pantalla, muñeca o mundo?** **No.** El único feedback del daño es el destello rojo (`SaludJugador.cs:89-90`), la vibración y el sonido. No existe ningún objeto de UI, texto o icono de vidas; el reloj de muñeca (`WristClock.cs`) solo muestra el tiempo.

**Dónde se guarda y quién la cambia:** `SaludJugador.Vida`, propiedad pública de solo lectura (`SaludJugador.cs:13`, `{ get; private set; }`). La cambian solo `SaludJugador.Start/Update/ReiniciarPartida/RecibirMordisco` (`:24,40,65,81`). `RecibirMordisco` la llama `MiniMonstruo.Morder` (`MiniMonstruo.cs:198`). Se restablece a 3 al empezar la partida (`SaludJugador.cs:40`, `:65` y `GameManager.ReiniciarComponentes`, `GameManager.cs:288-296`).

**¿Evento o método central?** **No hay evento** (ningún `Action` ni `UnityEvent` de vida). El único punto central es el método **`SaludJugador.RecibirMordisco()`** (`SaludJugador.cs:78`): ahí se podría disparar un evento nuevo. Hoy una interfaz tendría que leer `Vida` cada frame (se comprobó con búsqueda en todo `Assets/Scripts`: ningún otro script lee `Vida` de `SaludJugador`).

**¿El informe lo describe con exactitud?** Sí: 3 vidas, 1,5 s de invulnerabilidad, destello rojo, vibración y derrota por monstruo coinciden. Dos precisiones opcionales: (1) la captura de la entidad principal es muerte inmediata, no una vida; (2) no hay indicador de vidas en pantalla.

## 3. Parte C: datos concretos

| Dato | Informe | Real | Fuente | ¿Coincide? |
|---|---|---|---|---|
| Celdas transitables | 138 | 138 (135 `Celda_*` + 3 `Nicho_*`) | Mapa en `BackroomsMazeBuilder.cs:22-38`; conteo de objetos en la escena | Sí |
| Ciclos | 12 | 12 (149 aristas − 138 celdas + 1, grafo conexo) | Calculado sobre el mapa | Sí |
| Nichos | 3 | 3 | Escena, `NichoZona` | Sí |
| Vidas | 3 | 3 | `SaludJugador.cs:8`, escena | Sí |
| Invulnerabilidad | 1,5 s | 1,5 s | `SaludJugador.cs:9`, escena | Sí |
| Vida de cada mini | 3 | 3 | `MiniMonstruo.cs:13`, escena | Sí |
| Mini monstruos | 5 | 5 | Escena `MiniMonstruos` | Sí |
| Visión de la entidad | 8 m | 8 m (más sospecha a 16 m y cono de 100°) | `EntityAI.cs:18-21` | Sí, con matices (ver RF-07) |
| Velocidad mínima de la barra | 2,2 m/s | 2,2 m/s (tope 15 m/s) | `BarraMetal.cs:16-17`, escena | Sí |
| Duración total | 8 min | 480 s | `GameManager.duracion`, escena | Sí |
| Mesas | 3 | 3 | Escena `Armas`: `Mesa_1`, `Mesa_2`, `Mesa_3` | Sí |
| Armas por mesa | – | 1 cada una: Mesa_1 barra, Mesa_2 pistola, Mesa_3 pistola | Escena | – |
| Luces parpadeantes | 3 | 3 (`Celda_6_2`, `Celda_10_8`, `Celda_10_12`) | `FluorescentFlicker` en escena | Sí |
| Sonidos reales | 8 | 8 `.mp3` en `Assets/Audio/Reales/` | `disparo_pistola, drone_fondo, golpe_barra, grito_muerte, latido, mini_monstruo, rugido_entidad, zumbido_luz` | Sí |
| Reverb del mixer | 2,2 s y 1,5 s | SFX 2,2 s; Ambiente 1,5 s | `Resources/MezclaBackrooms.mixer:145,147` (valores de `Decay Time`) | Sí |
| Susurro | cada 20–40 s | 20–40 s, a 1,6–3,2 m, volumen 0,25–0,40, solo durante la partida | `AmbienteJuego.cs:15,60-64,82-84` | Sí |
| Triángulos entidad | 5.448 | **7.098** en lo que se ve (`Body`, `BookHead_Skin`). 5.448 es `BookHead_Low` en el objeto `Modelo_CuerpoEntero`, que está **inactivo** | Escena, `Entidad` | **No** |
| Triángulos mini monstruo | 3.169 | 3.169 | Escena `MiniMonstruos/Body` | Sí |
| Triángulos pistola | 1.900 | 1.900 (malla `Pistol92_Low`). El prefab entero suma 2.668 por el destello inactivo (esfera de 768) | Escena | Sí |
| Texturas a 1024 con ASTC | 1024, ASTC | **Parcial.** Formato por defecto de Android = ASTC (`ProjectSettings.asset:491-493`, formato 3). De las 70 texturas que usa la escena: 37 son ≤1024 (26 con override Android ASTC explícito) y 33 superan 1024: texturas del menú (`placa_*`, `fondo_panel`, `titulo_sangre`, `medidor_marcas`) a 2048, 3 lightmaps (color y dirección) a 2048 y texturas de los controles del SDK a 2048. `vineta_menu`, `grano_menu` y `cono_luz` están **sin comprimir** (2048). | `AssetDatabase`/`TextureImporter` | **No del todo** |
| Luces en tiempo real | – | **3** (puntuales, las de las luces parpadeantes). 59 puntuales horneadas activas, 4 horneadas apagadas, 4 direccionales apagadas. Quality: 1 luz por píxel | Escena | – |

Nota técnica: la lectura de `EditorUserBuildSettings.androidBuildSubtarget` dio "ASTC" al revisar la configuración de build y "Generic" (= usar Player Settings) al repetirla más tarde; `git status` no muestra cambios en `ProjectSettings`. El formato por defecto del proyecto sigue siendo ASTC.

## 4. Parte D: lo que existe y el informe no dice, y viceversa

### Existe en el código/escena y no aparece en los requisitos
- **Estado Captura** como quinto estado de la entidad y **zona de sospecha de 8–16 m** (`EntityAI.cs:20,109-110`).
- **Cono de visión de 100°** de la entidad (`EntityAI.cs:298-306`).
- **Derrota por mini monstruos** y el sistema de vidas con destello rojo (RF-17/RF-18 los cubren solo en parte).
- **Mini monstruos que reaparecen** y se reordenan en cada partida (`MiniMonstruo.cs:257-276`).
- **Persecución con rugido 3D** al detectar (una vez, `PursuitFeedback.cs:76-81`) y **música de persecución** opcional (solo suena si existe el archivo; hoy no existe, `:108-117`).
- **Susurros espaciales** (`AmbienteJuego.cs:75-85`), **drone de fondo** (menú y partida), **whoosh al teletransportarse** (`AmbienteJuego.cs:87-91`).
- **Pulsos hápticos por cercanía de la entidad** (`EntityProximityCue.cs`).
- **`HeadWallGuard`**: pantalla negra y "Vuelve atrás" si se atraviesa una pared físicamente (`HeadWallGuard.cs:1-6`).
- **`TeleportReachGate`**: no deja teletransportarse a destinos al otro lado de una pared.
- **Armas** que vuelven a su mesa al empezar o si caen por debajo del suelo (`ArmaAgarrable.cs:102,239-245`), resalte amarillo al apuntar (`:171-191`), rótulos sobre las mesas.
- **Voto** ME GUSTA / NO ME GUSTA y botón MENÚ en el fin de partida.
- **Menú de terror:** niebla oscura, grano, cono de luz, glitch ocasional del título (`TituloFluorescente.cs`, `AmbienteMenuTerror.cs`).
- **Créditos con sección SONIDOS** y volumen persistente.
- **Carteles y detalles de miedo** (`Detalles_Miedo`: suelo, paredes, techo, carteles).
- **Tutorial solo la primera vez** (PlayerPrefs).
- **Manos y control:** agarre con manos y a distancia; disparo con pinza de índice (`ArmaAgarrable.cs:154-167`).
- **Código sin uso en esta escena:** `DoorAutoOpen.cs` (ninguna instancia en `Backrooms_Main`), `Reintentar()` (alias, `GameManager.cs:389`) y el estado `reinicio` del CSV (inalcanzable).

### El informe lo dice (en tu resumen) y sí existe
Tutorial (4 pasos), victoria (ESCAPASTE), susurro, zumbido de salida y **puerta de luz que pulsa** (`ExitHum.cs:40-45`, el objeto `puerta` está asignado en la escena) existen. Todos los elementos listados en RF-01…RF-20 existen en algún grado.

### El informe lo dice y no lo encuentro, o difiere
- **Botón "Reintentar"** → ahora es "Iniciar de nuevo" / "Jugar de nuevo" (RF-13).
- **Título "BACKROOMS" parpadeante en todos los menús** → solo en el menú principal (RF-20).
- **Triángulos de la entidad 5.448** → 7.098 en lo visible.
- **"Texturas a 1024 con ASTC"** → parcial.
- **Persecución si el jugador está "a menos de 8 m y con línea de visión"** → falta el cono de 100° y la zona de sospecha (RF-07).
- **"Si la entidad no lo ve entrar, pierde su rastro"** → siempre pierde el rastro mientras está escondido (RF-08).
- **RNF-02** y **RNF-03** no se pueden confirmar tal cual.
- **Pasos, respiración y zumbido de salida como sonidos reales:** son generados por código, no archivos.

## 5. Bugs de código frente a diferencias de documento

**Bugs / huecos de código (conviene arreglar o declarar):**
1. **Abandonar desde la pausa (MENÚ PRINCIPAL) no guarda la partida en el CSV**; la causa `reinicio` es inalcanzable (`GameManager.cs:206-212`, `:365-369`).
2. **Los mini monstruos atacan al jugador escondido** (`MiniMonstruo.cs:149-162` no usa `NichoZona`), lo que contradice la idea de "esconderse".
3. **No hay indicador de vidas ni evento de cambio de vida** (`SaludJugador.cs`), aunque la mecánica es central.
4. **Filas con `tiempo_s = 0,0` y causa `monstruo`** en el CSV (filas 29, 33, 37): origen desconocido; revisar si hay una ruta real que mate al jugador en el instante de empezar.
5. **Menús y pausa solo con controles** (`LaserPointer.cs:103`, `GameManager.cs:181`): con manos no se puede pulsar un botón del menú.
6. **Aviso de build:** Active Input Handling = Both (no soportado en Android).

**Diferencias de documento (no son bugs):** RF-07 (cono y sospecha), RF-08 (frase), RF-09 (audio generado), RF-13 (nombre del botón), RF-20 (título), RNF-03 (duración máxima), triángulos de la entidad, texturas.

**Por comprobar en el visor (no verificables en código):** RNF-01 (72 FPS), RNF-02 (viñeta de confort), RNF-04 (legibilidad), RNF-05 (30 s), botón exacto del arco de teletransporte, ángulo del giro por pasos, ruta del CSV en el Quest, aspecto del reloj con manos.
