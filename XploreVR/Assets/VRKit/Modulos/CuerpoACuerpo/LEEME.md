# VRKit · Módulo Cuerpo a cuerpo

Espadas, hachas, escudos y granadas.
**Depende de:** Básico. **Para:** acción, combate medieval o fantástico, defensa, arenas.

| Script | Qué hace | Detalles |
|---|---|---|
| `MeleeWeapon` | Daño según **la velocidad de la punta** | Daño = velocidad × `Damage Per Speed` (con tope `Max Damage`; por debajo de `Min Speed` no cuenta). Un golpe por enemigo cada `Per Target Cooldown`. Estela, «zas» y vibración proporcional. |
| `Shield` | Escudo que **para** de verdad | Para los proyectiles que lo tocan y reduce los golpes al jugador si está entre la cabeza y el atacante. Se engancha solo a `PlayerHealth.DamageFilters` y `Projectile.HeadHitBlockers`. `Durability` > 0 para que se rompa. |
| `Grenade` | Granada, bomba, poción explosiva | Gatillo con ella en la mano = arma. *Fuse* (mecha, se puede «cocinar») o *Impact* (al chocar). Usa `Damage.Explode` del Básico. |

## Menú GameObject › VRKit › Cuerpo a cuerpo
Espada · Escudo · Granada · Caja de granadas · Muñeco de entrenamiento (no se rompe nunca y tiene barra de vida).

## Cosas a tener en cuenta
- Poned el collider del filo en **Is Trigger**: corta sin rebotar ni temblar en la mano.
- La espada necesita un hijo **`Tip`** en la punta. Se mide la punta porque un giro de muñeca la mueve mucho más que el mango.
- El escudo protege por su **eje Z** (cara azul). Modelad la cara mirando a -Y en Blender.
- En VR no se puede frenar el brazo del jugador. No hagáis que las armas reboten de forma realista: hace que se sientan rotas.
