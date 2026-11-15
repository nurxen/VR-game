# VRKit · Módulo Enemigos

Rivales a pie, voladores y por oleadas.
**Depende de:** Básico (usa `Health`, `HealthBar`, `Projectile`, `PlayerHealth`). **Necesita el paquete AI Navigation** para los enemigos a pie.
**Para:** arenas, hordas, terror, jefes, defensa.

| Script | Qué hace | Detalles |
|---|---|---|
| `SimpleEnemy` | Te ve, te persigue y te ataca cuerpo a cuerpo. Patrulla opcional. | NavMeshAgent + Health + Collider. Animator opcional (`Speed`, `Attack`, `Hit`, `Die`). Puntos y `objectiveId` al morir. |
| `FlyingEnemy` | Dron, murciélago, hada, nave pequeña | Sin NavMesh. Te rodea a `Preferred Distance`, **brilla antes de disparar** (`Warning Time`) y apunta a la cabeza. Cae con física al morir. |
| `WaveSpawner` | Oleadas: qué y cuántos | Spawn Points o círculo alrededor del jugador. *Endless* repite con más enemigos. *On All Waves Cleared* para la victoria o para abrir una puerta. |

## Máquina de estados del enemigo a pie
```
Idle / Patrol ──te ve (Detect Range)──► Chase ──a Attack Range──► Attack
      ▲                                   │  ▲                       │
      └──────── te alejas (Lose Range) ───┘  └── te alejas un poco ──┘
Cualquiera ──vida a 0──► Dead   (si le haces daño en Idle o Patrol, pasa a Chase)
```

## Menú GameObject › VRKit › Enemigos
Enemigo básico · Dron volador · Arena de oleadas (botón de inicio, 3 oleadas, cartel de estado).

## Cosas a tener en cuenta
- El suelo necesita **NavMeshSurface › Bake**. Volved a hornear si cambiáis el escenario.
- En VR, que te ataquen por la espalda frustra. Dad avisos (sonido, brillo) y poned enemigos delante o a los lados.
- Los proyectiles enemigos miden la distancia a vuestra **cabeza** (el jugador VR no tiene collider ahí). Un escudo en medio los para.
