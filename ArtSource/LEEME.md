# ArtSource · fuentes de arte

Aquí van los **archivos de trabajo** que Unity no necesita leer: `.blend`, `.psd`, `.kra`, Substance, referencias.
Están **fuera de `Assets/`** a propósito. Si Unity viera un `.blend`, intentaría importarlo y obligaría a tener Blender instalado y en la misma versión en todos los ordenadores.

```
ArtSource/
├── Modelos/      .blend (uno por objeto o por conjunto: Arco.blend, Taller.blend…)
├── Texturas/     .psd, .kra, .spp… (las texturas finales se exportan a Assets/_Juego/Arte/Texturas)
├── Audio/        proyectos de audio (las .wav o .ogg finales van a Assets/_Juego/Audio)
└── Referencias/  imágenes de referencia (ligeras: cuentan para la cuota de Git LFS)
```

## De Blender a Unity (resumen)
1. **Escena de Blender:** Unit System *Metric*, Unit Scale *1.0*. Modelad a tamaño real (1 unidad = 1 m).
2. **Antes de exportar:** *Object › Apply › All Transforms* (Ctrl+A), y el **origen** donde tenga sentido:
   - Lo que se coge: en el agarre.
   - Una puerta: en la bisagra.
   - Una palanca: en la base.
   - Un personaje: entre los pies.
3. **Export › FBX:**
   - *Selected Objects*.
   - *Forward: -Z Forward*, *Up: Y Up*.
   - *Apply Scalings: FBX All*.
   - *Apply Transform* marcado.
   - *Apply Modifiers* marcado.
   - Sin *Leaf Bones*.
4. **Destino:** `Assets/_Juego/Arte/Modelos/`.
5. **En Unity**, en el modelo importado: *Scale Factor* 1, y marcad *Bake Axis Conversion* si sale girado.

## Nombres
`SM_` modelo estático · `SK_` modelo con huesos · `M_` material · `T_` textura (`T_Arco_BaseColor`, `T_Arco_Normal`) · `A_` animación · `_LOD0`, `_LOD1` para niveles de detalle (Unity crea el LOD Group solo).

## Presupuesto orientativo para Quest 2
| Qué | Triángulos | Texturas |
|---|---|---|
| Objeto pequeño que se coge | 300 – 3.000 | 512–1024 px |
| Mueble o atrezo grande | 1.000 – 8.000 | 1024 px |
| Personaje o enemigo | 5.000 – 15.000 | 1024–2048 px |
| Toda la escena visible | 300.000 – 750.000 | — |

Un material por objeto siempre que se pueda (texturas en atlas). Hornead la oclusión ambiental (AO) en Blender.
