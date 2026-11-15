# VRKit · Módulo Poderes

Poderes con la **mano vacía**: hechizos, telequinesis, gancho o telaraña, tiempo bala y portales.
**Depende de:** Básico (usa `VRPlayer`, `HandInput`, `Projectile`, `GrabHighlight`). **Para:** magia, superpoderes, dioses, sueños, viajes.

| Script | Cómo se usa | Por dentro |
|---|---|---|
| `HandCaster` | Mano vacía: mantén el gatillo para cargar y suelta para lanzar. A/X cambia de hechizo. | Una esfera crece en la palma; al soltar crea el `Projectile` del hechizo con daño y velocidad según la carga. |
| `Telekinesis` | **Pull:** apunta, grip y tirón de muñeca hacia ti; el objeto vuela a tu mano. **Levitate:** gatillo; el objeto sigue tu mano, el joystick lo acerca o aleja, y al soltar sale lanzado. | Pull: calcula la parábola para que caiga en tu mano. Levitate: quita la gravedad y lo lleva con un muelle. |
| `GrappleHook` | Apunta (punto verde), gatillo para engancharte, mantenlo. | *Pull* te arrastra (cómodo); *Swing* te balancea (marea más). Bloquea la locomoción mientras tira. |
| `TimeController` | Y (mando izquierdo) o T en el teclado. Gasta energía. | Baja `Time.timeScale` y `fixedDeltaTime`. Tus manos y tu cabeza siguen a velocidad normal. |
| `PortalTeleporter` | Mete la cabeza en el portal. | Te lleva al destino con un fundido. Los objetos lanzados también pasan, con la velocidad girada. |

## Menú GameObject › VRKit › Poderes
Hechizos en la mano derecha · Telequinesis en la mano izquierda · Gancho en la mano derecha · Tiempo bala · Pareja de portales.

## Cosas a tener en cuenta
- Los poderes se apagan solos si la mano sujeta algo (`VRPlayer.IsHolding`).
- **Hechizos y gancho usan el gatillo:** ponlos en manos distintas.
- La telequinesis usa el grip, que en XRI también es el agarre lejano del *Near-Far Interactor*. Si solo queréis telequinesis en esa mano, desactivad su *Far Casting*.
- **Confort:** el gancho mueve al jugador sin que ande. Conectad *On Attached / On Released* a la viñeta, y ofreced *Swing* solo como opción avanzada.
