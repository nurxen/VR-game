# VRKit · Módulo Mundo abierto

Un mundo para explorar que no se acaba. Tiene montañas, colinas, valles y **ríos**, y **biomas** con su propia vegetación: pradera, bosque, orilla del río, desierto, montaña y cumbres nevadas. También trae **cuevas**, **día y noche con estrellas**, criaturas que andan solas (animales de día y bichos opcionales de noche), música por zona y lugares que se descubren. Todo sale de una **semilla**: la misma semilla da siempre el mismo mundo.

**Depende de:** Básico (`VRPlayer`, `Health`, `PlayerHealth`, `ScreenFader`, `SubtitleSystem`, `VRAudio`, `Effects`).
**Necesita:** URP, por sus shaders. **No necesita** el paquete AI Navigation: la NavMesh se hace por código.
**Estilo:** low poly con colores (caras planas, sin texturas). El material va aparte para cambiarlo cuando lo decidáis.

> Guía con dibujos: `Docs/2-Como-funciona/Guia-Mundo-abierto.pdf`.

## Montarlo en 1 minuto
1. Escena nueva vacía.
2. **GameObject › VRKit › Mundo abierto › Mundo abierto completo**.
   La primera vez crea los assets de ejemplo en `Assets/_Juego/MundoAbierto/`:
   - 8 biomas;
   - 12 plantas y rocas low poly;
   - 2 criaturas;
   - la entrada de cueva;
   - los materiales.
3. Play. Apareces en tierra firme y el mundo se genera alrededor.

## Cómo funciona (en corto)
| Pieza | Qué hace |
|---|---|
| `OpenWorld` | Lo principal. Divide el mundo en **chunks** de 32 × 32 m. Los genera en otros hilos (sin tirones) solo cerca del jugador y borra los lejanos. Lo cercano lleva todo el detalle, colisión, vegetación y cuevas; lo lejano va simplificado. Mantiene al jugador pegado al suelo. Guarda **lo que cambia**. |
| `Nucleo/OpenTerrain` | La fórmula del terreno: continentes, colinas, cordilleras, ríos que serpentean y clima (temperatura y humedad). De ahí sale el bioma de cada punto. |
| `Nucleo/TerrainMesher` | Convierte las alturas en malla: una rejilla de triángulos, cada uno con el color de su bioma (y roca si está muy empinado). Los bordes llevan faldones que tapan las grietas. |
| `Nucleo/PropScatter` · `PropCombiner` | Reparte la vegetación por casillas, siempre igual para la misma semilla, y la **junta en una sola malla por chunk**: así la Quest dibuja cientos de árboles de una vez. |
| `BiomeProfile` | Un asset por bioma: color del suelo, qué crece (con densidad, pendiente, altura, cerca del río…) y su música y ambiente de día y de noche. |
| `TerrainChunk` | Un trozo ya generado: malla, agua, colisión, teletransporte y vegetación. |
| `WorldProp` | Va en cada árbol o roca. `Remove()` lo quita para siempre (talar, recoger). |
| `PlacedObject` | Objetos que pone el jugador (`OpenWorld.Instance.Place(prefab, pos, giro)`). Se guardan en su zona. |
| `SkyCycle` | Sol, luna, color del cielo, niebla y **estrellas** que salen al anochecer. Eventos al anochecer y al amanecer. |
| `CaveSystem` · `CaveEntrance` | Bocas de cueva en las laderas. Al entrar, un fundido te lleva a un interior generado (túneles y salas con cristales) y te da una luz. |
| `WorldNavMesh` | La NavMesh se rehace sola alrededor del jugador para que las criaturas anden por un terreno que no existía al empezar. |
| `CreatureSpawner` · `WorldCreature` | Animales de día y bichos de noche, detrás del jugador y fuera de su vista. Al amanecer, los bichos se desvanecen. |
| `BiomeAudio` | Música y ambiente por bioma y por hora, con fundidos y silencios entre canciones. |
| `WorldDiscovery` | La primera vez que llegas a un bioma: «Descubres: Bosque». |

## El terreno y el guardado
- **No se guarda el terreno**: se vuelve a calcular igual con la semilla. Por eso el mundo puede ser infinito y las partidas ocupan poco.
- **Se guarda lo que cambia**, en un archivo por chunk (`persistentDataPath/vrkit_mundo_abierto/<World Name>/chunks/x_z.json`):
  - qué árboles o rocas se quitaron;
  - qué objetos puso el jugador y dónde.

  Al alejarte se escribe y se borra de memoria; al volver se lee y se vuelve a poner.
- `mundo.json` guarda la semilla, dónde estabas, la hora del día y lo descubierto.
- Se guarda solo cada minuto y medio, al quitarte las gafas y al salir. Desde un botón: `OpenWorld.Instance.Save()`, `NewWorld()`.

## Ajustes que más se tocan
| Quiero… | Toca esto |
|---|---|
| Otro mundo | `OpenWorld › Seed` (0 = al azar cada partida nueva) |
| Ver más lejos | `View Distance` (200 m por defecto; en Quest 2, de 160 a 220) |
| Más o menos detalle cerca | `Detail Distance` (80 m) y `Resolution` (16 = un cuadrado cada 2 m) |
| Montañas más altas o planas | `Terrain › Mountain Height`, `Hill Height` |
| Más ríos o más anchos | `Terrain › River Spacing`, `River Width` |
| Más desiertos o más bosque | `Terrain › Temperature Bias`, `Humidity Bias` |
| Más o menos árboles | En cada `BiomeProfile`, la `Density` de cada regla |
| Noches más cortas | `SkyCycle › Night Speed Factor`, `Day Minutes` |
| Sin bichos de noche | `CreatureSpawner › Max Night = 0` |
| Más o menos cuevas | `OpenWorld › Cave Spacing`, `Cave Chance` |

## Usar modelos de otros paquetes (Asset Store, Blender…)
1. Seleccionad en Project la carpeta de sus modelos (o sus prefabs) y usad **Tools › VRKit › Mundo abierto › Preparar modelos seleccionados (Read∕Write)**.
2. Abrid un bioma (`_Juego/MundoAbierto/Biomas/…`) y en **Props** cambiad el **Prefab** de una regla por el suyo, o añadid una regla nueva.
3. **LOD**: -1 (automático) coge el primer LOD con 1.500 triángulos o menos. Se puede fijar a mano (0, 1, 2).
4. Su textura y su color se dibujan con el shader ligero `VRKit/PropTextura` (sin viento ni efectos del paquete). Los materiales que se ven igual se juntan en uno; por cada material distinto sale una «Vegetación combinada · N» en cada chunk (una llamada de dibujo cada una). Menos modelos distintos, o modelos que compartan textura (atlas), = menos.
5. Si choca (árboles, rocas grandes), marcad **Collide**: se usa su collider, invisible.
- **¿Sale con el color cambiado** (rosas moradas, hierba azul)? Su shader guarda el color de otra forma. Mirad en la Consola la línea «[VRKit] Vegetación: …» de ese material (dice qué textura y color ha leído). Para corregirlo: seleccionad el prefab en Project › **Tools › VRKit › Mundo abierto › Copia editable de materiales**. Crea una copia en `_Juego/MundoAbierto/MaterialesVegetacion` (shader `VRKit/PropTextura`), la apunta en **Material Overrides** del OpenWorld, y en esa copia se cambia el **Color** o la **Textura** a mano.
- **¿La textura es una máscara** (colores raros que no se arreglan con un color)? En la regla del bioma, marcad **Original Material**: la vegetación combinada de esa regla usa su material y su shader originales (viento, estaciones, colores). Gasta más que el shader del kit: medid los FPS, sobre todo en la hierba.
- No toquéis los prefabs de `_Juego/MundoAbierto/Vegetacion` si otros biomas los usan; añadid reglas nuevas.
- Si la consola dice «no es legible», falta el paso 1. Si dice «tiene N triángulos», bajad el LOD de esa regla.

## Textura en el suelo (césped, arena…)
1. Material `_Juego/MundoAbierto/Materiales/Terreno` → hueco **Textura del suelo**: arrastrad ahí el PNG (con mipmaps, como viene por defecto).
2. **Tamaño**: cada cuántos metros se repite (4 m va bien para hierba). **Fuerza**: cuánto se nota.
3. Por defecto solo pone luces y sombras de la textura sobre el color del bioma (la misma hierba vale para todos). Con **Usar también sus colores** se ven sus colores tal cual.
4. Cada bioma dice cuánta textura lleva (**Ground Texture** en su BiomeProfile): pradera 1, desierto 0,4, nieve 0, por ejemplo.
- Cuesta una lectura de textura por píxel del suelo: medid los FPS.
- Para que el color de fondo se parezca al de la textura, cambiad **Ground Color** del bioma (con el cuentagotas sobre la textura).

## El mapa
- **Minimapa** en la muñeca izquierda: gira la muñeca y míralo como un reloj. Su norte apunta al norte de verdad.
- **Mapa grande:** botón **Y** (o **M** en el teclado). Joystick izquierdo = mover, joystick derecho arriba/abajo = zoom, **X** = poner o quitar una marca donde estás, **Y** = cerrar.
- Lo no explorado sale en niebla. Se descubren 48 m a tu alrededor (más si estás en alto). Se guarda en `mapa.json`, junto a la partida.
- **Cuevas:** gris si has pasado cerca, negro con nombre si has llegado a la boca, y un **«?» amarillo** por la zona si hay una cerca que no has visto.
- **Vuestros pueblos y sitios:** poned `MapMarker` en el prefab (icono y nombre). Al acercarte, «Descubres: …» y queda en el mapa. Para cosas colocadas con la semilla: `IMapFeatureSource` + `WorldMap.AddSource`.
- ¿Escena ya montada? **GameObject › VRKit › Mundo abierto › Añadir mapa y mejoras de rendimiento**.

## Rendimiento (Quest)
- Ya viene: colisión del terreno en otro hilo, árboles con colisión a poquitos (24 por frame), una sola agua, 3 niveles de detalle (más allá de 130 m, 4 veces menos triángulos), NavMesh solo cuando cambia el suelo cercano y **calidad automática** (baja la distancia de vista si los FPS no llegan; `Auto Quality`, `Min View Distance`).
- `VRPerformance` en el XR Origin: foveated rendering (y `Render Scale`).
- **Tools › VRKit › Optimizar para Quest:** URP sin HDR ni sombras, MSAA 4×, sin post-procesado, OpenXR Single Pass Instanced y Foveated Rendering.
- Ver FPS: modo desarrollador (F1 o los dos joysticks hundidos 1 s) y mira el minimapa.
- Medid en una build en las gafas: desde el editor con Link los FPS dependen del ordenador.

## Vuestros modelos de Blender
- **Vegetación:** un prefab con la malla y, si choca, un collider sencillo (cápsula o esfera).
  - En el FBX, marcad **Read/Write**: si no, no se puede juntar en la malla del chunk y se dibuja suelto, que es más lento.
  - Pintad los colores como **colores de vértice** (Blender: Vertex Paint / Color Attributes), o usad el color del material.
  - Origen en la base y 1 unidad = 1 metro.
- **Criaturas:** raíz con `NavMeshAgent` y `WorldCreature` (y `Health` si se les puede pegar).
- **Cueva:** la entrada mira hacia +Z (cuesta abajo).

## Cosas a tener en cuenta
- **Rendimiento:** ver «Rendimiento (Quest)». Si aún va justo: baja `View Distance` o la densidad de árboles con colisión.
- **Mareo:** el terreno tiene cuestas. Ofreced teletransporte (funciona sobre el terreno, no en paredes muy empinadas) y giro por saltos.
- **Las cuevas son interiores aparte:** el terreno es una malla de alturas y no puede tener agujeros. Es el método más barato y fiable para Quest.
- **El agua no se nada:** el fondo de los ríos es poco profundo y se puede andar. Nadar o barcas serían un añadido.
- **Las luces puntuales** (la luz del jugador en la cueva) iluminan el terreno con el renderizado Forward de URP. Con Forward+, la cueva se ve con la luz ambiente.
- **No se ha probado aún en Unity:** el núcleo (terreno, mallas, vegetación y cuevas) está probado fuera de Unity. Si sale un error, copiad el mensaje de la consola.
