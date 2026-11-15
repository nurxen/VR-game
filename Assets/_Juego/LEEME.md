# _Juego · vuestro juego

El guion bajo hace que esta carpeta salga la primera en el Project de Unity.

```
_Juego/
├── Escenas/      una escena de pruebas por persona (Pruebas_Ana, Pruebas_Luis) + las escenas del juego
├── Prefabs/      TODO lo que montéis: enemigos, objetos, puertas, salas (así no hay conflictos en Git)
├── Scripts/      vuestros scripts (namespace propio, por ejemplo «namespace NuestroJuego»)
├── Arte/
│   ├── Modelos/      FBX exportados desde Blender (los .blend van en ArtSource/)
│   ├── Texturas/     PNG o TGA finales (1024 px como norma)
│   ├── Materiales/   materiales URP Lit
│   └── Animaciones/
├── Audio/
│   ├── Musica/       .ogg (Load Type: Streaming)
│   └── Efectos/      .wav cortos (Load Type: Decompress On Load)
└── Settings/     AudioMixer, perfiles de render, datos de juego (ScriptableObjects)
```

## Para modificar algo de VRKit
- **Heredad:** `public class Duende : SimpleEnemy { … }` en `_Juego/Scripts`.
- **Copiad:** duplicad el script a `_Juego/Scripts`, cambiad el nombre de la clase y usad el vuestro.

Así, si actualizáis VRKit, no perdéis nada.
