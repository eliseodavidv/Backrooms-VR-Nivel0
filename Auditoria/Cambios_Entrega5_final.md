# Cambios desde la Entrega 3 hasta la Entrega 5 (estado real)

Base de comparación: commit `3b6055e` (Entrega 3) contra `59793ae`/`3a1e978` (HEAD). Datos tomados de `git log`, `git diff --stat`, el código y las fechas de los archivos.

**Paquete real del APK** (leído con `aapt dump badging`, build-tools 34.0.0 de Unity 2022.3.62f3, sobre `Backrooms_VR.apk`, `Backrooms_VR_overlay.apk`, `Backrooms_VR_puertas.apk` y `BackroomsVR.apk`; los cuatro dan lo mismo):

```
package: name='com.DefaultCompany.VRMetaQuest02_Eliseo' versionCode='1' versionName='0.1'
application-label:'VRMetaQuest02_Eliseo'
```

Nota: `ProjectSettings.asset` solo tiene `Standalone: com.DefaultCompany.VRMetaQuest02-Eliseo` (con guion); en el APK de Android el nombre lleva guion bajo.

## Cómo leer las tres columnas

- **(a) Código:** el cambio existe en el repositorio (commit `59793ae` o anterior sin subir).
- **(b) Último APK:** el último APK compilado es `Builds/Backrooms_VR_puertas.apk` (17-nov 17:17). Se generó después de la última edición de scripts (17:11), de la escena (17:11) y de los shaders y recursos (16:44–16:48). Por eso todo lo que hay hoy en `Assets/` entró en ese APK. Solo lo comprobé por fechas de archivo y por el log de compilación del Editor ("Build succeeded"); no abrí el APK para inspeccionar su contenido.
- **(c) Probado en el visor:** **ninguno está marcado como hecho.** El APK `Backrooms_VR_puertas.apk` **no se instaló** (adb no detectó el visor) y yo no tengo acceso al visor. Lo que sí probé está en la columna "Probado en el Editor" (sin visor, con las limitaciones que se indican).

## Resumen

| Cambio | (a) Código | (b) Último APK | (c) Visor | Probado en el Editor |
|---|---|---|---|---|
| Corazones de vida arriba a la izquierda | Sí | Sí | **No** | Sí (quad de respaldo) |
| OVROverlay para el HUD | Sí | Sí | **No** | Parcial (en Editor el overlay se desactiva) |
| Late Latching | Sí | Sí | **No** | No aplica (solo actúa en el visor) |
| Puertas con mirilla en los 3 nichos | Sí | Sí | **No** | Sí |
| La entidad se aleja si el jugador está escondido | Sí | Sí | **No** | Sí (una corrida) |
| Los mini monstruos no muerden al escondido | Sí | Sí | **No** | Sí (una corrida) |
| Carteles y suciedad (decals, tuberías, paneles muertos) | Sí | Sí | **No** | Capturas del Editor (sin medir rendimiento) |
| Mesas sucias para las armas | Sí | Sí | **No** | Capturas del Editor |
| Menú de inicio, opciones y créditos | Sí | Sí | **No** | Capturas del Editor |
| Audio: biblioteca, mezcla y sonido 3D | Sí | Sí | **No** | Parcial |
| Entidad procedural y piel del Book Head | Sí | Sí | **No** | No verificado por mí |
| Pistola con balas infinitas | Sí | Sí | **No** | No verificado por mí |
| Reinicio de partida (INICIAR DE NUEVO) y recentrado | Sí | Sí | **No** | Parcial |

## Detalle por cambio

### 1. Corazones de vida arriba a la izquierda
- **Código:** `Assets/Scripts/IndicadorVidas.cs` (400 líneas, nuevo), `Assets/Scripts/Editor/BackroomsVidasBuilder.cs`, `Assets/Resources/Vidas/*`, shaders `UIHud.shader` y `UIHudQuad.shader`. Constantes actuales: distancia 1,2 m; centro de la fila a 23° a la izquierda (bordes 14° y 32°) y 18° hacia arriba; corazones de 6°. Eventos `OnVidaCambiada` (`SaludJugador`) y `OnFaseCambiada` (`GameManager`).
- **Coincide con lo pedido:** sí (borde izquierdo ~14°, derecho ~32°, centro ~18° arriba, 1,2 m, 6° por corazón; fijo a la cabeza, sin lerp).
- **Visible solo en la fase Jugando:** sí, por diseño.
- **No probado:** que el HUD se vea estable y bien posicionado en el Quest 2; que el puntero láser del menú y las mirillas no queden tapados; que no se corte en el borde del campo de visión real. Calculé que el borde queda a ~34° y el Quest 2 ve unos 44° hacia la nariz, pero es un cálculo, no una medición.

### 2. OVROverlay
- **Código:** `IndicadorVidas.cs` crea un `OVROverlay` tipo Quad (Overlay, dinámico) hijo del `CenterEyeAnchor`, con una cámara ortográfica que dibuja en una `RenderTexture` de 512x224 (capa 31). Más un quad normal de respaldo en el mismo objeto.
- **Limitación conocida:** en el Editor sin visor `OVROverlay` se desactiva solo, así que lo que vi en las capturas es el quad de respaldo, no la capa del compositor.
- **No probado:** el comportamiento real del overlay en el visor (orientación, transparencia, brillo, y si desaparece o parpadea).

### 3. Late Latching
- **Código:** `OculusSettings.asset` pasó de `LateLatching: 0` a `1`, y el `OVRManager` de la escena tiene el override `LateLatching: 1`. Es el único cambio de configuración de XR en el diff (`ProjectSettings` no cambió).
- **No probado:** si el movimiento flotante de los corazones desapareció. Antes de este cambio el usuario reportó que los corazones "se movían como flotando"; no tengo ningún reporte posterior de que se haya resuelto. APK intermedio: `Backrooms_VR_latelatching.apk` (16:30).

### 4. Puertas de los nichos con mirilla
- **Código:** `Assets/Scripts/PuertaNicho.cs` (nuevo), `Assets/Scripts/Editor/BackroomsPuertasBuilder.cs` (nuevo), mallas en `Assets/Modelo 3D/Puerta/`, 3 puertas en `Backrooms_Main.unity`. Sin colliders ni `NavMeshObstacle`. Entreabierta a 100° cuando no estás escondido; se cierra en 0,65 s con crujido procedural al esconderte y se abre al levantarte. Mirilla de 6 x 38 cm, centrada a 1,00 m. Reutiliza `Mat_MesaVieja_MaderaOscura` y `Mat_MesaVieja_Oxido`; 2 draw calls por puerta, sin luces nuevas.
- **Coincide con lo pedido:** sí (animación entre 0,5 y 0,8 s; rendija de 4 a 6 cm; sin colliders; misma condición de escondido: cabeza dentro del nicho en horizontal y por debajo de 1,1 m).
- **Diferencia a tener en cuenta:** elegí 100° de apertura porque a 130° o más la hoja atraviesa una pared de `Nicho_0_8`.
- **Probado en el Editor:** el progreso pasa de 0 a 1 y de vuelta; las capturas `escondite_puerta_e5.png` y `escondite_mirilla_e5.png` muestran la puerta abierta y la vista por la mirilla.
- **No probado:** la altura de la mirilla con la forma real de agacharse; el crujido (solo comprobé que el clip se crea, no lo escuché); cómo se ve la madera y la iluminación en el visor; los FPS.

### 5. El monstruo se va si el jugador está escondido
- **Código:** `EntityAI.cs`. Si estás escondido y te perseguía: mira alrededor entre 4 y 6 s (`tiempoMirarAlrededor`), luego pasa a Patrulla hacia un punto a 10 m o más del nicho (`distanciaAlejarse`) y no vuelve a ese nicho durante 20 s (`tiempoEvitarNicho`). Si sales o te levantas con la entidad a menos de 3 m y con visión, te detecta normalmente (`radioRedeteccionCercana`).
- **Probado en el Editor:** Persecución → Búsqueda ("jugador escondido") → Patrulla hacia un punto lejano (a 16 m del nicho); no hubo captura mientras estuve escondido; al levantarme a 2,2 m me detectó incluso desde atrás.
- **No probado:** no medí los 4–6 s al segundo (observé la transición dentro de una ventana de ~7 s); no probé la regla de los 20 s; la prueba usó velocidad de entidad reducida y `Time.timeScale` bajo, solo en esa sesión de Play. Tampoco lo probé con una partida completa en el visor.

### 6. Los mini monstruos no muerden al jugador escondido
- **Código:** `MiniMonstruo.cs`. Con el jugador escondido: `Ve()` devuelve falso, `Morder()` no hace nada, pierden el rastro, miran alrededor 4–6 s y se alejan a 10 m o más; los que están a menos de 10 m se alejan al esconderte.
- **Probado en el Editor:** dejó de morder mientras estaba escondido (la vida se mantuvo en 2) y se alejó a más de 12 m.
- **No probado:** el momento exacto del giro, la evitación de 20 s, y el comportamiento con varios minis a la vez en el visor. En la prueba de minis desactivé la entidad.

### 7. Carteles y suciedad
- **Código:** `Assets/Scripts/Editor/DetallesMiedo.cs` genera "Detalles_Miedo": decals de suelo y pared, huecos de techo, placa caída, escombros, carteles (SALIDA gastado, NO CORRAS, garabato SILENCIO), chorreones, óxido, una tubería y un cable, y paneles fluorescentes muertos. Shaders `Decal.shader` y `Silueta.shader`; atlas `detalles_atlas.png`; texturas gastadas de pared, alfombra, techo y rodapié (`TexturasRealismo.cs`). Todo sin colliders, marcado Static, con un solo material de decals.
- **Pedido relacionado:** estos cambios son de entregas anteriores (archivos del 4 al 11 de noviembre); no los modifiqué en la sesión de las puertas y los corazones.
- **No probado:** el rendimiento en el Quest 2 (72 FPS) y que ningún decal tape la mirilla de las puertas.

### 8. Otros cambios encontrados
- **Mesas sucias para las armas:** `BackroomsMesasBuilder.cs`, texturas en `Assets/Texturas/Mesas/`, 7 materiales `Mat_MesaVieja_*`, y `RotuloMesa.cs` (rótulo gastado). El tablero conserva su collider.
- **Menú de inicio, opciones y créditos:** `BackroomsMenuBuilder.cs` (814 líneas), `TituloFluorescente.cs`, `AmbienteMenuTerror.cs`, `VRButton.cs`, `GameManager.cs` (paneles, volumen, fin de partida con resumen).
- **Audio:** `BibliotecaSonidos.cs` (+ `.asset`), `MezclaBackrooms.mixer`, `Sonido3D.cs` (espacializador de Meta), `AmbienteJuego.cs`, `SonidosPlaceholder.cs` (+469 líneas), audios en `Assets/Audio/Reales/` (los créditos de autor y enlace siguen pendientes en `CREDITOS_AUDIO.txt`).
- **Entidad procedural y piel del Book Head:** `EntidadProcedural.cs`, `BookHeadSkinBuilder.cs`.
- **Parpadeo y persecución:** `FluorescentFlicker.cs` (+89 líneas), `PursuitFeedback.cs`, `EntityAudio.cs`, `ExitHum.cs`.
- **Pistola con balas infinitas:** en la Entrega 3 la pistola tenía 6 balas (`balasMax = 6`, contador `textoBalas`); ahora no hay límite ni recarga y solo hay una cadencia mínima de 0,2 s. **No tengo registrado que esto se haya pedido en las solicitudes de esta sesión**; confírmalo si era intencional (el README ya dice "balas infinitas").
- **Reinicio de partida:** `GameManager.EmpezarDirecto` (bandera estática que sobrevive a la recarga de escena) y recentrado de la cabeza al punto de inicio en el visor. El recentrado solo aplica con visor; no probado.
- **Lightmaps:** `Lightmap-0/1/2` regenerados (cambia el aspecto de la iluminación).
- **Archivos de ignore y repositorio:** `TempAssembly.dll` sacado del índice; `.gitignore` ahora excluye `TempAssembly.dll`, `*.pdb`, `Respaldo_antes_*/` y `Captures/`.

## Discrepancias con lo que se pidió

1. **Nombre de paquete en el README:** decía `com.DefaultCompany.VRMetaQuest02-Eliseo` (con guion, copiado de `ProjectSettings.asset`). El APK real usa guion bajo (`com.DefaultCompany.VRMetaQuest02_Eliseo`); corregido en el README, incluida la ruta del CSV en el Quest.
2. **No existe una entrada `Android:` bajo `applicationIdentifier`** en `ProjectSettings.asset`; solo `Standalone`. Unity deriva el identificador de Android de ahí.
3. **Pistola:** pasó de 6 balas a infinitas (ver arriba); no consta como pedido en esta sesión.
4. **Entidad:** tiene 4 estados de comportamiento más un valor `Captura` en el enum (fin de partida). El README lo aclara.
5. **Instalación en el visor:** `Backrooms_VR_puertas.apk` no se instaló por falta de conexión adb; por lo tanto **ningún cambio de esta lista está confirmado en el visor**.
6. **CSV local:** las pruebas del Editor dejaron 2 filas extra (`derrota,captura`) en `backrooms_stats.csv` de la PC (62 → 64 líneas). No se restauró porque pediste no tocarlo. El archivo no está en el repositorio.

## Cómo revertir los cambios de esta sesión

- Copiar `Respaldo_antes_puertas/Scripts/*` a `Assets/Scripts/` y `Respaldo_antes_puertas/Backrooms_Main.unity` a `Assets/`; borrar `PuertaNicho.cs`, `BackroomsPuertasBuilder.cs` y `Assets/Modelo 3D/Puerta/`. (Las carpetas `Respaldo_*` están en el disco pero ignoradas por git.)
- Para volver al HUD de abajo a la izquierda: reinstalar `Backrooms_VR_overlay.apk`.
