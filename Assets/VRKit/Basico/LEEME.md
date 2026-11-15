# VRKit · Básico

Lo que necesita cualquier juego VR. Va siempre: todos los módulos dependen de esto, y esto no depende de ningún módulo.

## Qué hay

### Runtime/Core
| Script | Qué hace | Dónde se pone |
|---|---|---|
| `Health` | Vida, daño, curación y muerte, con eventos (`On Damaged`, `On Health Changed`, `On Death`) | Enemigos, objetos rompibles, cualquier cosa que muera |
| `Damage` | `TryApply` (aplicar un golpe) y `Explode` (daño en área con empuje) | No se pone: se llama desde código |
| `PlayerHealth` | Vida del jugador: destello rojo, vibración, regeneración, dificultad, `DamageFilters` (escudos, armaduras) | XR Origin |
| `VRPlayer` | Cabeza, manos, velocidad de las manos, ¿sujeta algo?, teletransportar, recentrar, altura, bloquear la locomoción | XR Origin |
| `HandInput` | Gatillo, grip, A/X, B/Y y joystick de una mano, leídos directamente | No se pone: lo usan los poderes |
| `GameManager` | Puntos, tiempo límite, pausa, derrota y victoria | Objeto vacío, uno por escena |
| `ObjectiveTracker` | Objetivos («Recoge 5 setas»), victoria al completarlos | Un tablón o la muñeca con un texto |
| `EventSequence` | Pasos con esperas: intros, trampas, tutoriales | Objeto vacío |
| `TriggerZone` | Eventos al entrar o salir de una zona | Collider con Is Trigger |
| `ProgressSave` + `LevelProgress` | Progreso en JSON (niveles, récords, banderas, contadores) · récord y desbloqueo al ganar | Estático · junto al GameManager |
| `Projectile` | Proyectil genérico: rayos para no atravesar paredes, explosión opcional, impacto en la cabeza del jugador | Prefab de bala, hechizo o disparo enemigo |
| `HealthBar` | Barra de vida flotante para cualquier `Health` | Enemigos, jefes, objetos a defender |
| `VRItem` | Id, nombre y categoría de un objeto | Ingredientes, llaves, objetos |
| `VRAudio`, `ObjectPool`, `VRHaptics`, `Spinner`, `Effects`, `ReturnToSpawn` | Sonido eficiente, reutilizar objetos, vibración, girar y flotar, efectos de prototipo, devolver objetos perdidos | — |

### Runtime/Interaccion
| Script | Qué hace |
|---|---|
| `Throwable` | Lanzar con fuerza extra (1,5–2× se siente mejor en VR), sonidos, vibración, sabe si va lanzado |
| `DamageOnImpact` | Daño según la velocidad del choque (piedras, martillos) |
| `SpawnOnGrab` | Contenedor que te pone en la mano un objeto nuevo (carcaj, caja de munición, cesta) |
| `TagSocketInteractor` | Hueco que solo acepta ciertos Tags (cerradura con llave, pedestal de puzle) |
| `VRButton` | Botón 3D que se pulsa con el dedo o con el rayo |
| `BodyFollower` | Sigue al cuerpo a la altura de la cintura (cinturón, fundas, mochila) |
| `GrabHighlight` | Ilumina lo que se puede coger cuando se apunta. **Ponedlo en todo lo agarrable.** |

### Runtime/UI
| Script | Qué hace |
|---|---|
| `VRMenuManager` | Menú flotante (pausa, opciones, fin de partida). Botón Menú del mando izquierdo o Escape. |
| `SettingsManager` | 15 opciones guardadas: movimiento, sonido y accesibilidad |
| `ScreenFader` · `SceneLoader` | Fundidos y destellos · cambio de escena con fundido |
| `GameHUD` · `WristMenu` | Marcador (puntos, tiempo, vida, munición de cualquier `IAmmoSource`) · menú de muñeca |
| `SubtitleSystem` | Subtítulos en cola, con nombre y voz: `Say("Capitana: ¡Cuidado!")` |
| `TutorialPrompt` | Aviso pegado al mando, que se completa al pulsar su botón |
| `FloatingText` · `LazyFollow` | Textos que suben y se desvanecen · interfaz que sigue la mirada sin marear |
| `LevelSelectMenu` | Botones de nivel, bloqueados hasta ganar el anterior, con su récord |
| `VRDebugConsole` | Logs y FPS dentro de las gafas (solo en Development Build) |

## Menú GameObject › VRKit › Básico
- **Preparar escena VR**: suelo con teletransporte, XR Origin, partida, menú con opciones y subtítulos.
- **Piedra lanzable** · **Botón 3D**.
- **Sistema de partida**: GameManager, fundidos, y `PlayerHealth` y `VRPlayer` en el XR Origin.
- **Menú de pausa** · **Menú principal** · **Consola de depuración**.
- **Subtítulos** · **Tablón de objetivos** · **Tutorial pegado a los mandos** · **Secuencia de eventos**.

Y **Tools › VRKit › Módulos y tipos de juego**.

## Detalles que conviene saber

**Mover al jugador.** No se mueve la cámara, sino el XR Origin. `VRPlayer.TeleportTo(pies, giro)` calcula el desplazamiento para que tu cabeza acabe donde toca. `TeleportWithFade` hace lo mismo con un fundido.

**Bloquear la locomoción.** `VRPlayer.SetLocomotionEnabled(false/true)` lleva la cuenta: si dos cosas la bloquean, hace falta que las dos la liberen. El montador rellena solo la lista de componentes de locomoción.

**Opciones (SettingsManager).**
- **Movimiento:** giro suave o por saltos, ángulo del salto, velocidad de giro, joystick o teletransporte, velocidad al andar, viñeta, modo sentado y zurdo.
- **Sonido:** volumen general, música y efectos, con un AudioMixer con `MasterVolume`, `MusicVolume` y `SfxVolume` expuestos.
- **Accesibilidad:** subtítulos y su tamaño, vibración, dificultad (Historia, Fácil, Normal, Difícil) y textos flotantes.

El menú de pausa del montador trae las dos pantallas de opciones ya conectadas.

**Menús.** Las escenas tienen que estar en *File › Build Profiles › Scene List*. El menú de pausa vuelve a una escena llamada `MainMenu`, y el menú principal carga `Nivel1`. Cambiad esos nombres en el *OnClick* de cada botón.

**Texto en VR.** Como mínimo unos 30 px con el Canvas a escala 0,001, a 1–1,5 m.

**Ganchos para módulos.** Los módulos se meten en el Básico sin que el Básico los conozca:
- `PlayerHealth.DamageFilters` (escudos, armaduras).
- `Projectile.HeadHitBlockers` (algo que para un disparo justo antes de la cabeza).
- `IAmmoSource` (munición para el marcador).

Usad el mismo patrón en vuestros sistemas.

## Crear vuestros propios bloques
```csharp
using UnityEngine;
using VRKit;

public class Cristal : MonoBehaviour, IDamageable
{
    public void TakeDamage(DamageInfo info)
    {
        Debug.Log($"Golpe de {info.amount} en {info.point}");
        // cambiar color, abrir un portal, despertar a un titán...
    }
}
```
`GameManager.Instance.AddScore(10)` · `ObjectiveTracker.ReportStatic("setas")` · `SubtitleSystem.SayStatic("Duende", "¡Hola!")` · `SceneLoader.Load("Nivel2")` · `VRAudio.PlayAt(clip, pos)` · `FloatingText.Spawn("+10", pos, color)`.
