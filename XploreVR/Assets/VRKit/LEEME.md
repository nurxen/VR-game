# VRKit — bloques para juegos VR en Unity (Meta Quest 2)

Piezas listas para usar, sea cual sea vuestro juego: **un núcleo básico** que va siempre y **ocho módulos opcionales** que añadís o quitáis según el tipo de juego.

> Escrito para **Unity 6 (6000.0.66f2 o superior) + XR Interaction Toolkit 3.x + OpenXR (Meta Quest)**. Se ha comprobado con un compilador, combinando el núcleo con cada módulo, pero **todavía no se ha probado dentro de Unity ni en las gafas**. Si al importarlo sale un error, copiad el mensaje de la consola y se corrige.

**¿Aún no tenéis proyecto?** Empezad por `Docs/Guia-Empezar-el-proyecto.pdf` (o `EMPIEZA_AQUI.md` en la raíz del repositorio). Explica Unity, GitHub y Blender paso a paso.

---

## 1. Cómo está organizado

```
Assets/VRKit/
├── LEEME.md                ← este archivo (el mapa)
├── Basico/                 ← SIEMPRE. Todo lo demás depende de esto.
│   ├── Runtime/Core        vida y daño, partida, jugador, sonido, guardado, objetivos, secuencias
│   ├── Runtime/Interaccion coger y lanzar, botones, huecos, contenedores, resaltado
│   ├── Runtime/UI          menús, opciones, fundidos, subtítulos, tutorial, textos flotantes
│   └── Editor              menú GameObject › VRKit › Básico y la ventana Tools › VRKit › Módulos
├── Modulos/                ← OPCIONALES. Cada uno depende solo del Básico.
│   ├── Mecanismos          palancas, ruedas, cajones, puertas, caja fuerte
│   ├── ObjetosEInventario  coleccionables, pociones, mochila, caldero, linterna, rompibles
│   ├── ArmasADistancia     arco y flechas, pistola, varita, dianas
│   ├── CuerpoACuerpo       espada, escudo, granadas
│   ├── Poderes             hechizos, telequinesis, gancho, tiempo bala, portales
│   ├── Enemigos            enemigo a pie, dron volador, oleadas
│   ├── MundoDeBloques      mundo tipo Minecraft: generar, romper, poner, fabricar, día y noche, criaturas
│   └── MundoAbierto        terreno infinito con biomas, ríos, cuevas, cielo con estrellas, criaturas, guardado por zonas
├── Ejemplos/               ← campo de entrenamiento completo (usa los seis primeros módulos)
└── Generated/              (se crea sola) materiales y prefabs de los objetos de ejemplo
```

Cada carpeta tiene su propio `LEEME.md` con los detalles: qué hay, cómo se monta y qué tocar.

**Regla de oro:** los módulos **nunca dependen unos de otros**, solo del Básico. Se puede quitar cualquiera borrando su carpeta, o desde **Tools › VRKit › Módulos y tipos de juego**. Ejemplos usa los seis primeros: si quitáis uno de ellos, quitad también Ejemplos (la ventana lo hace sola).

---

## 2. El Básico (siempre)

| Grupo | Scripts | Para qué |
|---|---|---|
| Vida y daño | `Health`, `Damage` (`TryApply`, `Explode`), `IDamageable`, `DamageInfo`, `HealthBar` | Cualquier cosa que reciba daño; explosiones; barras de vida flotantes |
| Jugador | `VRPlayer`, `PlayerHealth`, `HandInput` | Cabeza, manos, velocidad de las manos, mover o bloquear al jugador, su vida, botones de cada mando |
| Partida | `GameManager`, `ObjectiveTracker`, `EventSequence`, `TriggerZone`, `ProgressSave`, `LevelProgress` | Puntos, tiempo, pausa, victoria; objetivos; escenas scriptadas; zonas; progreso guardado |
| Coger y tocar | `Throwable`, `DamageOnImpact`, `SpawnOnGrab`, `TagSocketInteractor`, `VRButton`, `BodyFollower`, `GrabHighlight`, `ReturnToSpawn`, `VRItem` | Lanzar, golpear al chocar, contenedores, huecos con filtro, botones, cinturón, resaltado, identidad de objetos |
| Proyectiles | `Projectile` | Balas, bolas de fuego, disparos enemigos (lo usan Armas, Poderes y Enemigos) |
| Interfaz | `VRMenuManager`, `SettingsManager`, `ScreenFader`, `SceneLoader`, `GameHUD`, `WristMenu`, `SubtitleSystem`, `TutorialPrompt`, `FloatingText`, `LazyFollow`, `LevelSelectMenu`, `VRDebugConsole` | Menús, 15 opciones, fundidos, cambio de escena, marcador, menú de muñeca, subtítulos, tutorial, textos, selector de niveles, consola en las gafas |
| Utilidades | `VRAudio`, `ObjectPool`, `VRHaptics`, `Spinner`, `Effects` | Sonido eficiente, reutilizar objetos, vibración, girar y flotar, efectos de prototipo |

**Menú GameObject › VRKit › Básico:** Preparar escena VR · Piedra lanzable · Botón 3D · Sistema de partida · Menú de pausa · Menú principal · Consola · Subtítulos · Tablón de objetivos · Tutorial · Secuencia de eventos.

Detalles: `Basico/LEEME.md`.

---

## 3. Los módulos (opcionales)

| Módulo | Qué trae | Menú (GameObject › VRKit › …) |
|---|---|---|
| **Mecanismos** | `VRLever`, `VRDial`, `VRSlider`, `VRDoor`, `CombinationLock` | Palanca · Rueda o dial · Cajón · Puerta · Caja fuerte |
| **Objetos e inventario** | `Collectible`, `Consumable`, `BackpackStorage`, `CraftingStation`, `CraftingRecipe`, `Flashlight`, `Breakable` | Moneda · Poción · Caldero con recetas · Mochila · Linterna |
| **Armas a distancia** | `BowString`, `Arrow`, `ProjectileLauncher`, `Target` | Diana · Arco con carcaj · Pistola · Varita |
| **Cuerpo a cuerpo** | `MeleeWeapon`, `Shield`, `Grenade` | Espada · Escudo · Granada · Caja de granadas · Muñeco |
| **Poderes** | `HandCaster`, `Telekinesis`, `GrappleHook`, `TimeController`, `PortalTeleporter` | Hechizos · Telequinesis · Gancho · Tiempo bala · Portales |
| **Enemigos** | `SimpleEnemy`, `FlyingEnemy`, `WaveSpawner` | Enemigo básico · Dron · Arena de oleadas |
| **Mundo abierto** | `OpenWorld`, `BiomeProfile`, `TerrainChunk`, `SkyCycle`, `CaveSystem`, `WorldNavMesh`, `CreatureSpawner`, `BiomeAudio`, `WorldDiscovery`… | Mundo abierto completo |
| **Mundo de bloques** | `VoxelWorld`, `BlockType`, `BlockRegistry`, `VoxelHandTool`, `BlockInventory`, `BlockHotbar`, `VoxelCrafting`, `VoxelGame`, `VoxelPlayer`, `VoxelDayNight`, `VoxelMob`… | Mundo completo (supervivencia o creativo) · Maqueta sobre una mesa |

---

## 4. ¿Qué módulos necesito? (por tipo de juego)

| Tipo de juego | Módulos | Lo que más vais a usar |
|---|---|---|
| Puzle / escape room | Mecanismos, Objetos | Palancas, diales, puertas, `TagSocketInteractor` con llaves, objetivos en orden |
| Shooter / arena | Armas a distancia, Enemigos (+ Cuerpo a cuerpo) | Arco o pistola, oleadas, drones, `GameHUD` |
| Acción cuerpo a cuerpo | Cuerpo a cuerpo, Enemigos, Objetos | Espada y escudo, pociones, oleadas |
| Magia / aventura | Poderes, Objetos, Mecanismos, Enemigos | Hechizos, caldero, telequinesis, portales, subtítulos |
| Cozy / gestión | Objetos, Mecanismos | Coleccionables, mochila, caldero o banco de trabajo, `ProgressSave` |
| Terror / exploración | Objetos, Mecanismos, Enemigos | Linterna, notas, puertas, `TriggerZone` para sustos |
| Experiencia narrativa | Solo Básico | Subtítulos, `EventSequence`, `TriggerZone`, fundidos |
| Exploración (mundo abierto) | Mundo abierto (+ Objetos e inventario, Mecanismos) | Terreno con semilla, biomas, cuevas, día y noche, música por zona |
| Mundo de bloques (tipo Minecraft) | Mundo de bloques | Generador de terreno, mano que rompe y pone, barra de la muñeca, recetas, día y noche |

Si dudáis, **dejadlos todos**: tener módulos de más no molesta. Quitarlos solo sirve para tener el proyecto más limpio.

La ventana **Tools › VRKit › Módulos y tipos de juego** hace tres cosas:
- Comprueba que tenéis los paquetes necesarios.
- Enseña qué módulos hay.
- Deja quedarse solo con los de un tipo de juego.

---

## 5. Empezar en 5 minutos (con el proyecto ya creado)

1. **Tools › VRKit › Módulos y tipos de juego** → todo en «OK» (si falta algo, la ventana dice dónde instalarlo).
2. Escena nueva → **GameObject › VRKit › Básico › Preparar escena VR**. Crea:
   - El suelo con teletransporte.
   - El jugador (XR Origin de Starter Assets) con vida.
   - GameManager, fundidos, menú de pausa con opciones y subtítulos.
3. Añade lo que quieras desde **GameObject › VRKit › …** Cada objeto aparece delante del jugador, ya montado.
4. Play con las gafas por **Quest Link**, o sin gafas con el **XR Device Simulator**.

¿Queréis verlo todo funcionando? **GameObject › VRKit › Ejemplos › Montar campo de entrenamiento completo**. Monta una escena con siete zonas (tiro, combate, puzles, alquimia, magia, arena y monedas) y cinco objetivos.

---

## 6. Cinco ideas que explican todo el kit

1. **XRI detecta, VRKit decide.** XR Interaction Toolkit detecta los agarres y los gatillos y lanza eventos (`selectEntered`, `activated`…). Los scripts del kit escuchan esos eventos y ponen la lógica de juego.
2. **El daño es un enchufe común.** Todo lo que hace daño llama a `Damage.TryApply(golpeado, DamageInfo)`, que busca un `IDamageable` en lo golpeado. Para que algo vuestro reciba daño, implementad `IDamageable`.
3. **Puntos de encuentro.** `GameManager.Instance`, `VRPlayer.Instance`, `PlayerHealth.Instance`, `ScreenFader.Instance`, `ObjectiveTracker.Instance` y `SubtitleSystem.Instance`: cualquier script los encuentra sin buscarlos.
4. **Todo se conecta en el Inspector.** Casi todos los scripts tienen eventos (`On Death`, `On Turned On`, `On Solved`…) para conectar puertas, sonidos o subtítulos sin programar.
5. **Ganchos en lugar de dependencias.** El Básico no conoce los módulos. Cuando un módulo necesita meterse en algo del Básico, se engancha. Por ejemplo, el escudo se añade a `PlayerHealth.DamageFilters` y la munición se lee con `IAmmoSource`. Así se puede quitar cualquier módulo.

---

## 7. Cómo trabajar con el kit sin perderse

- **No editéis los scripts de VRKit directamente.** Si necesitáis cambiar uno, hay dos formas:
  - Heredad de él (por ejemplo `class MiEnemigo : SimpleEnemy`).
  - Copiadlo a `Assets/_Juego/Scripts` con otro nombre de clase.

  Así podréis actualizar el kit sin perder vuestros cambios.
- **Vuestro juego va en `Assets/_Juego`**: escenas, prefabs, modelos, scripts propios.
- **Prefabs para todo.** Montad un objeto con el menú, cambiad el modelo por el vuestro y guardadlo como prefab en `_Juego/Prefabs`. En GitHub, dos personas pueden tocar prefabs distintos sin conflictos; en la misma escena, no.
- **Tocad valores, no código.** Daño, velocidades, tiempos, textos y eventos están en el Inspector. La guía tiene una tabla «¿Dónde toco para…?».

---

## 8. Cosas que hay que tener en cuenta desde el día uno

| Tema | Lo importante |
|---|---|
| **Mareo** | Teletransporte y giro por saltos por defecto. Fundidos en todo cambio brusco. No mováis la cámara sin que el jugador lo decida. Ofreced viñeta y modo sentado (ya están en Opciones). |
| **Rendimiento** | Quest 2 a 72 FPS: 13,9 ms por fotograma. Iluminación horneada, una luz en tiempo real sin sombras, pocos materiales, `ObjectPool` y `VRAudio`. Probad en las gafas cada semana con la consola de FPS. |
| **Escala** | 1 unidad = 1 metro. Modelad a tamaño real en Blender (una puerta mide 2,1 m). En VR los errores de escala se notan muchísimo. |
| **Texto** | Grande y a 1–3 m. Lo pequeño no se lee en Quest 2. |
| **Interacción** | Todo lo que se pueda coger debe parecerlo (`GrabHighlight`). Avisad antes de los ataques (los enemigos y los drones ya lo hacen). |
| **Equipo** | Una escena por persona, prefabs para todo, commits pequeños y frecuentes, *pull* antes de empezar. |

---

## 9. Problemas frecuentes

| Problema | Solución |
|---|---|
| Error en `SendHapticImpulse` o `XRBaseInputInteractor` | Otra versión de XRI: cambiad solo `Basico/Runtime/Core/VRHaptics.cs`. |
| Errores con `...Toolkit.Interactables` / `.Interactors` | Tenéis XRI 2.x: actualizad a 3.x. |
| Error con `linearVelocity` | Unity anterior a la 6: cambiadlo por `velocity`. |
| «Missing Script» tras quitar un módulo | Algún objeto usaba ese módulo: borrad el componente o recuperad la carpeta (papelera o GitHub). |
| La escena de ejemplo sale sin jugador | Falta Starter Assets: Package Manager › XR Interaction Toolkit › Samples › Starter Assets › Import. |
| Los menús no responden | Canvas sin `Tracked Device Graphic Raycaster`, o EventSystem sin `XR UI Input Module`. |
| El enemigo no se mueve | Falta el NavMesh: NavMeshSurface › Bake (paquete AI Navigation). |
| Los poderes no hacen nada | `VRPlayer` en el XR Origin, y la mano debe estar vacía. Si no encuentra las manos, arrastradlas a `VRPlayer`. |
| Las opciones de giro o velocidad no cambian nada | `SettingsManager › Locomotion Providers` vacío: volved a crear el menú con el XR Origin ya en la escena. |
| Textos con cuadrados | Window › TextMeshPro › Import TMP Essential Resources. |
| La escena no carga | No está en *File › Build Profiles › Scene List*, o el nombre no coincide. |
