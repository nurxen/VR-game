# VRKit · Módulo Armas a distancia

Arco con flechas, pistola, varita, ballesta y dianas.
**Depende de:** Básico (usa `Projectile`). **Para:** shooters, galerías de tiro, caza, magia con varita.

| Script | Qué hace |
|---|---|
| `BowString` + `Arrow` | Arco completo: coges el arco, tensas la cuerda con la otra mano y sueltas. La flecha vuela, se clava, hace daño y se puede recoger. |
| `ProjectileLauncher` | Dispara con el gatillo. Modo **Raycast** (impacto instantáneo y trazo) o **Projectile** (bala o hechizo visible). Munición, ráfaga y dispersión. Es un `IAmmoSource` para el marcador. |
| `Target` | Diana: más puntos cuanto más cerca del centro. Puede reaparecer en otro sitio. `objectiveId` para los objetivos. |

## Arco: la jerarquía importa
```
Arco                 Rigidbody + XR Grab Interactable (Colliders: SOLO el mango)
├── Mango            Collider (lo que se agarra)
├── Grip Attach      dónde lo sujeta la mano
├── String Top / String Bottom   extremos de la cuerda
├── Nock Rest Point  la cuerda en reposo; su eje Z (azul) apunta hacia donde dispara
└── Cuerda           BoxCollider (Is Trigger) + XR Simple Interactable + LineRenderer + BowString
```
- En el `XR Grab Interactable` del arco, la lista **Colliders** lleva **solo el mango**. Si no, al ir a coger la cuerda cogeríais el arco.
- **Auto Arrow Prefab** relleno = aparece una flecha al coger la cuerda (arcade). Vacío = hay que sacarla del carcaj.
- La flecha: su eje Z apunta a la punta y necesita un hijo `Tip`. Valores para empezar: Max Pull Distance 0,5 m y Max Arrow Speed 40 m/s.

## Pistola y varita
- `Muzzle`: punto de salida, con el eje Z hacia delante.
- `Max Ammo` -1 = infinita. Para recargar, llamad a `Reload()` desde un botón, un socket o un evento.
- Proyectil: desactivad la gravedad del Rigidbody si debe ir recto. Con `Explosion Radius` > 0 hace daño en área.

## Menú GameObject › VRKit › Armas a distancia
Diana · Arco con flechas y carcaj · Pistola (impacto instantáneo) · Varita mágica (proyectil).

## Al modelar en Blender
El origen del arma va en el **agarre** (donde cierra la mano), con el cañón o la punta hacia **-Y en Blender** (que en Unity es +Z hacia delante, si exportáis con *Forward: -Z Forward*). Si no, ajustad el hijo `Grip Attach`.
