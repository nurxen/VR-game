# VRKit · Módulo Mundo de bloques

Un juego tipo Minecraft en VR: un mundo de cubos que se genera solo, romper y poner bloques con las manos, barra de bloques en la muñeca, fabricar herramientas, día y noche, criaturas y partidas guardadas. También como **maqueta sobre una mesa** (bloques de 3 cm que se tocan con el mando).

**Depende de:** Básico (`VRPlayer`, `HandInput`, `Health`, `PlayerHealth`, `ScreenFader`, `VRHaptics`, `VRAudio`, `Effects`, `FloatingText`, `SubtitleSystem`, `IAmmoSource`).
**Necesita:** URP (por el shader `VRKit/Voxel`). **No** necesita AI Navigation: las criaturas andan por los bloques sin NavMesh.
**Para:** juegos de construir, sandbox, supervivencia, creativo, dioramas.

> Guía completa con dibujos: `Docs/2-Como-funciona/Guia-Mundo-de-bloques.pdf`.

## Montarlo en 1 minuto
1. Escena nueva vacía.
2. **GameObject › VRKit › Mundo de bloques › Mundo completo · supervivencia** (o **· creativo**).
   La primera vez crea los assets de ejemplo en `Assets/_Juego/MundoDeBloques/`: 20 bloques, el atlas, el material, 11 recetas, 6 herramientas y 3 criaturas.
3. Play. Apareces en el centro del mundo.

## Controles (por defecto)
| Mano | Botón | Hace |
|---|---|---|
| Derecha | Gatillo (mantener) | Romper bloque · pegar a una criatura |
| Izquierda | Gatillo | Poner el bloque elegido · sobre una **mesa de trabajo**, abre las recetas de mesa |
| Izquierda | Y / X | Siguiente / anterior bloque de la barra (o tócalos con la otra mano) |
| Derecha | B | Fabricar (recetas de bolsillo). Se fabrica **tocando** la receta con la mano |
| Derecha | A | Saltar (si el XR Origin no trae su propio JumpProvider) |
| Izquierda | Joystick | Andar (XRI). Al andar contra un bloque de altura 1, se sube solo |
| Izquierda | Menú | Pausa (Básico) |

Para zurdos: en `Manos`, cambiad `Hand` de los dos `VoxelHandTool` y del `BlockHotbar`. Con `Role = Ambos`, una sola mano rompe (gatillo) y pone (A/X).

## Qué hay
| Script | Qué hace |
|---|---|
| `VoxelWorld` | El mundo: qué bloque hay en cada sitio, genera el terreno con una semilla, malla los chunks cerca del jugador, rayos contra bloques (`Raycast`), `GetBlock`/`SetBlock`, guardado de lo cambiado. El tamaño de bloque es la **escala** del objeto. |
| `VoxelChunk` | Una columna de 16 × 16 bloques convertida en malla (la crea el mundo). |
| `BlockType` · `BlockRegistry` | Un bloque (asset): casillas del atlas, dureza, herramienta, qué suelta, transparente, en cruz… · La lista de bloques, el atlas y el material. |
| `VoxelHandTool` | La mano que rompe y pone. Rayo, tocar o los dos. Contorno del bloque, vibración al picar, no deja poner un bloque dentro de ti o de una criatura. |
| `BlockInventory` | Lo que llevas (infinito en creativo). `IAmmoSource`: el `GameHUD` enseña cuántos quedan. |
| `BlockHotbar` | La barra en la muñeca: bloquecitos de verdad con su número. |
| `VoxelRecipe` · `VoxelCrafting` | Recetas (bloques → bloques, o → un objeto como un pico) · Arco de recetas que se fabrican tocándolas. |
| `VoxelToolItem` | Pico, pala, hacha, espada: se cogen con XRI y rompen más rápido / pegan más. Se gastan. |
| `VoxelDrop` · `VoxelFx` | Bloquecitos que caen y vuelan hacia ti · trozos al romper. |
| `VoxelGame` | Modo (creativo/supervivencia), aparecer, guardar y cargar, reaparecer al morir. |
| `VoxelPlayer` | Gravedad, salto, subir escalones, daño por caída, rescate del vacío. |
| `VoxelDayNight` | Sol, cielo, niebla y bloques más oscuros de noche. |
| `VoxelMob` · `VoxelMobSpawner` | Criaturas que andan por los bloques (pacíficas o hostiles) · aparecen de día o de noche. |
| `Nucleo/` | Código puro, sin Unity: ruido con semilla, generador de terreno, mallado, atlas de ejemplo, compresión. |

## Recetas de ejemplo
Bolsillo: **Tablones** ×4 ← Tronco · **Mesa de trabajo** ← 4 Tablones.
Mesa: **Pico/Hacha de madera** ← 3 Tablones · **Pala** ← 1 Tablón · **Pico de piedra** ← 3 Roca + 2 Tablones · **Espada de piedra** ← 2 Roca + 1 Tablón · **Pico de hierro** ← 3 Mineral de hierro + 2 Tablones · **Ladrillos de piedra** ×4 ← 4 Roca · **Cristal** ×2 ← Arena + Carbón · **Ladrillos** ×4 ← 2 Tierra + Carbón.

En supervivencia: la piedra y los minerales **necesitan pico** para soltar algo. Empezáis con 8 tablones.

## Recetas rápidas
- **Un bloque nuevo:** Create › VRKit › Mundo de bloques › Bloque. Pon sus casillas del atlas y añádelo **al final** de la lista del `Registro de bloques`.
- **Vuestras texturas:** pintad encima de `Atlas.png` (8 × 8 casillas de 16 px, Filter Mode **Point**, sin Mip Maps). Podéis hacerlo más grande (32 px por casilla): mismas casillas.
- **Mundo plano para construir:** `VoxelWorld › Terrain › Flat`.
- **Mundo más grande:** `Size In Chunks` (8 × 8 = 128 × 128 bloques) o `Infinite`. Probad el rendimiento en las gafas.
- **Desde código:** `VoxelWorld.Instance.SetBlock(pos, bloque)`, `GetBlock`, `SurfaceHeight(x, z)`, `Raycast(ray, metros, out hit)`, evento `BlockChanged`.
- **Otro mundo guardado:** `VoxelGame › World Name`. Botones del menú: `Save()`, `NewWorld()`, `ToggleMode()`, `Respawn()`, `VoxelDayNight.SkipToMorning()`.

## Cosas a tener en cuenta
- **Rendimiento en Quest 2:** `View Distance` 3–4, mundo de 8 × 8 chunks y altura 48 van holgados. Las cuevas añaden muchas caras. El shader no usa luces en tiempo real (la noche es una variable global).
- **Mareo:** caer, saltar y subir escalones solos mueven la cámara sin que lo decidas. Ofreced teletransporte (los chunks lo aceptan, solo encima de los bloques) y `Auto Step` desactivable. A escala 1 un bloque mide 1 m: el mundo se siente enorme, y está bien.
- **Guardado:** solo se guardan los chunks que habéis cambiado; lo demás sale de la semilla. Si cambiáis el nombre de un bloque, los guardados antiguos lo pierden (se convierte en aire). Si cambiáis la altura del mundo, se empieza uno nuevo.
- **Blender:** las criaturas y herramientas de ejemplo son cubos. Cambiadlas por las vuestras manteniendo la raíz (con `VoxelMob` o `VoxelToolItem`). En una criatura, los pies en el origen y 1 unidad = 1 bloque.
- **Lo que no trae (ideas para después):** agua y lava que fluyen, luz de antorchas, cofres, cultivos, redstone, multijugador.
