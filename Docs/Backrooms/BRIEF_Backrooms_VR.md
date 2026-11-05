# BRIEF DE CONSTRUCCIÓN — Backrooms VR · Nivel 0 (Meta Quest 2, Unity 2022.3, Meta XR SDK)

Este archivo es la fuente de verdad. Construye la escena `Backrooms_Main` siguiendo el informe del Hito 1.
Referencias visuales en esta misma carpeta (ábrelas y míralas antes de construir):

- `video_frames/frame_01.jpg … frame_15.jpg`: la demo conceptual (15 s, 1 fotograma por segundo). Es el LOOK objetivo.
- `informe_figuras/pag10-10.png`: mapa del laberinto (fig. 4a/4b) y boceto de la vista en primera persona (fig. 5).
- `informe_figuras/pag11-11.png`: wireframes de las 6 pantallas (menú, tutorial, en juego sin HUD, persecución, derrota, victoria).
- `informe_figuras/pag13-13.png`: flujo de navegación (fig. 7) y máquina de estados de la entidad (fig. 8).
- `informe_figuras/pag7-07.png`: arco de la historia y tabla HEART.

## Reglas de trabajo
- Haz UN bloque (entregable) a la vez. No avances al siguiente hasta que el actual compile sin errores rojos en consola.
- No borres el OVRCameraRigInteraction ni el TeleportHotspot que ya existen en la escena; reutilízalos.
- No toques `Escena_Andahuasi`.
- Al terminar cada bloque, lista qué creaste (GameObjects, prefabs, scripts) y revisa la consola.
- Rendimiento Quest 2: 72 FPS. Módulos Static, luz bakeada, pocas luces en tiempo real.

## Look visual (lo que se ve en el video)
- Paredes: papel tapiz amarillo desaturado con rayas verticales; rodapié marrón oscuro. Piso: alfombra amarillo-verdosa húmeda. Techo: placas blancas sucias con paneles fluorescentes.
- Paleta: amarillo desaturado, luz blanca fluorescente, sombras marrones. El ROJO solo para peligro (persecución); el VERDE solo para la salida.
- Los pasillos miden 2,7 m de alto. El pasillo se ve largo y repetitivo.
- La salida: puerta de luz blanco-verdosa con un letrero verde encima (frame_15).
- Entidad: silueta humana alta y oscura, vista a lo lejos al fondo del pasillo (frames 06, 07, 09). Un modelo simple o silueta basta.
- Viñeta de confort: borde negro que cierra la vista durante el teletransporte (frames 03, 06, 07, 08). Se activa solo mientras se mueve.
- Persecución: tinte rojo en el borde de la vista, latido y vibración en controladores (frames 09 a 11).
- Arco de teletransporte con zona de destino en cian (frame_04).
- Reloj de muñeca: cuenta regresiva de 8:00 en el controlador izquierdo, fondo oscuro, números amarillos en fuente monoespaciada (esquina inferior izquierda en el video). Es UI diegética, sin HUD.
- Menú: panel flotante a 1,5 m, fondo oscuro, título amarillo "BACKROOMS VR · NIVEL 0", subtítulo "Encuentra la salida antes de que te encuentre", botones Comenzar (amarillo, principal), Tutorial y Salir (gris oscuro). Selección con puntero láser.

## Requisitos por entregable (propuesta de distribución)

### E1 (15 %, semana 10) — base y movimiento
RF-03 teletransporte con arco y zona de destino · RF-04 giro por pasos con el stick · RNF-01 72 FPS · RNF-02 viñeta de confort · RNF-07 jugar de pie, estacionario · XR Rig (OVRCameraRigInteraction).

### E2 (30 %, semana 11) — laberinto y entidad patrullando
RF-05 laberinto con módulos de pasillo repetidos, un inicio y una única salida · RF-06 entidad con estado Patrulla.
Tareas:
1. Prefab `Pasillo_Modulo` (piso, 2 paredes, techo, 2,7 m de alto, Static) y variante `Pasillo_Nicho` con hueco lateral.
2. Armar el laberinto copiando el mapa de `informe_figuras/pag10-10.png` (fig. 4b): inicio arriba a la izquierda, salida abajo a la derecha, zona de patrulla arriba a la derecha, 3 nichos (escondites).
3. Reemplazar el piso azul y los placeholders actuales por el piso/paredes con el look de arriba.
4. Bakear NavMesh; los nichos con NavMeshModifier en Not Walkable.
5. Objeto `Entidad` con NavMeshAgent y `EntityAI.cs`, estado Patrulla con 4 waypoints en la zona de patrulla.
6. Luces fluorescentes emisivas con iluminación bakeada; solo 2 o 3 parpadean.

### E3 (50 %, semana 12) — detección, persecución y sonido
RF-06 estados completos (Patrulla, Sospecha, Persecución, Búsqueda; ver fig. 8) · RF-07 detección por visión (menos de 8 m y línea de visión) y por ruido · RF-08 esconderse en nicho agachándose · RF-09 audio espacial (zumbido de la salida que crece, pasos y respiración de la entidad) · RF-11 borde rojo + latido + vibración en persecución · RNF-06 toda alerta sonora con equivalente visual o háptico.

### E4 (70 %, semana 13) — partida completa
RF-01 menú inicial con puntero láser · RF-02 tutorial de 4 pasos · RF-10 reloj de muñeca con 8 minutos · RF-12 victoria/derrota · RF-13 pantalla de resumen con voto y botones Reintentar/Menú · RF-14 registro de estadísticas en archivo · RNF-04 menús a 1–2 m dentro del campo de visión · RNF-05 onboarding en 30 s · RNF-08 volumen y pausa.

### E5 (100 %, semana 14) — pruebas y pulido
RNF-01 verificar 72 FPS · RNF-03 partida de 5 a 8 min · pruebas con usuarios y ajustes · build final para Quest 2.

## Estados de la entidad (fig. 8)
Patrulla → Sospecha (oye ruido o ve al jugador lejos) → Persecución (lo ve a menos de 8 m) → Captura (a menos de 1 m = derrota).
Persecución → Búsqueda (pierde la línea de visión, jugador escondido) → Patrulla (10 s sin encontrarlo). Sospecha → Patrulla (nada en 5 s).
Velocidad de persecución ajustada para que un buen salto de teletransporte dé ventaja sin volverla trivial.
