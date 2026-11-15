# VRKit · Módulo Mecanismos

Cosas que se manipulan con la mano siguiendo un eje: palancas, ruedas, cajones, puertas y cajas fuertes.
**Depende de:** Básico. **Para:** puzles, escape rooms, cabinas de naves, máquinas, casas.

## El truco de todos
El objeto **no se coge de verdad**. Un `XR Simple Interactable` avisa de que lo agarras y el script lee dónde está tu mano para mover la pieza por **su único eje**. Así no se sale de su sitio ni atraviesa paredes.

| Script | Eje | Eventos | Úsalo para |
|---|---|---|---|
| `VRLever` | Gira sobre X de `Handle` | `onValueChanged(0-1)`, `onTurnedOn`, `onTurnedOff` | Interruptores (Notches = 2), aceleradores (Notches = 0), frenos con muelle (Return When Released) |
| `VRDial` | Gira sobre Y de `Knob`, con la muñeca | `onValueChanged`, `onStepChanged(int)`, `onReleased` | Caja fuerte (Steps = 10), radios, válvulas; sin Limited, manivelas (cuenta `Turns`) |
| `VRSlider` | Se desliza por Z del padre (0 → Length; negativo = hacia ti) | `onValueChanged`, `onReachedEnd`, `onReachedStart` | Cajones, persianas, cerrojos (Spring Back) |
| `VRDoor` | Bisagra (Y) o corredera | `onOpened`, `onClosed`, `onTriedLocked` | Puertas, rejas, trampillas. `Open`, `Close`, `Toggle`, `SetOpen(bool)`, `SetOpenAmount(0-1)`, `Lock`, `Unlock`, `UnlockAndOpen` |
| `CombinationLock` | — | `onSolved`, `onUnsolved` | Varios `VRDial` en su número. También estatuas o espejos que hay que colocar. |

## Menú GameObject › VRKit › Mecanismos
Palanca · Rueda o dial · Cajón · Puerta · Caja fuerte con combinación (3 diales, solución 3-7-1, abre su puerta).

## Recetas rápidas
- **Palanca que abre una puerta:** Palanca › *On Turned On* → Puerta › `Open`; *On Turned Off* → `Close`.
- **Llave en una cerradura:** `TagSocketInteractor` (Básico) con Accepted Tags = Llave → *Select Entered* → Puerta › `UnlockAndOpen`.
- **Puente que baja girando una manivela:** VRDial sin límites › *On Value Changed* → vuestro script, o VRDoor › `SetOpenAmount`.
- **Puzle en orden:** `ObjectiveTracker` con objetivos *Sequential* y `Report("id")` desde cada mecanismo.

## Al modelar en Blender
Poned el **origen** (pivote) de cada pieza móvil donde gira: en la base de la palanca, el centro del pomo, la bisagra de la puerta. Y modelad con la palanca «hacia arriba» (eje Y de Unity), la rueda «tumbada» (su eje es Y) y el cajón cerrado.
