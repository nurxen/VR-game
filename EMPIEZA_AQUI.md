# EMPIEZA AQUÍ

Plantilla para empezar vuestro juego VR para **Meta Quest 2** con **Unity + GitHub + Blender**. Trae:

- **`Assets/VRKit`**: el kit de bloques VR (un núcleo básico y seis módulos opcionales).
- **`.gitignore` y `.gitattributes`**: GitHub sube lo que toca y los archivos grandes van por Git LFS.
- **`Herramientas/`**: un script que configura Git LFS y la fusión de escenas en cada ordenador.
- **`ArtSource/`**: los `.blend` y las fuentes de arte, fuera de Unity.
- **`Assets/_Juego/`**: las carpetas de vuestro juego, ya ordenadas.
- **`Docs/`**: dos guías en PDF.
  - **Guía para empezar el proyecto:** todo esto paso a paso y con detalle.
  - **Guía de VRKit:** cómo está organizado el kit, qué módulos necesita cada tipo de juego y cómo funciona cada pieza.

> Seguid los pasos en orden. Lo que va marcado como **(una persona)** lo hace solo una; lo marcado **(cada uno)**, los dos.

---

## Paso 0 · Instalar las herramientas (cada uno)

| Herramienta | Para qué | Notas |
|---|---|---|
| **Unity Hub** + **Unity 6 LTS** (6000.0.66f2 o superior) | El motor | Al instalar, añadid los módulos **Android Build Support**, **OpenJDK** y **Android SDK & NDK Tools**. Meta pide 6000.0.66f2 como mínimo. |
| **Git** (trae Git LFS en Windows) | Control de versiones | En Mac: `brew install git git-lfs` |
| **GitHub Desktop** | Subir y bajar cambios sin consola | Opcional pero recomendado para empezar |
| **Blender** 4.x LTS | Modelado | |
| **Visual Studio Code** o **Rider** | Editar scripts | En VS Code, la extensión «Unity» |
| **Meta Horizon** (app del móvil) | Activar el **modo desarrollador** en las gafas | Hace falta una organización de desarrollador en developers.meta.com (es gratis) |
| **Meta Quest Link** (Windows) | Probar desde Unity sin compilar | En Mac no hay Link: compilad el APK o usad el XR Device Simulator |

---

## Paso 1 · Crear el proyecto de Unity (una persona)

1. Unity Hub › **New project** › plantilla **Universal 3D** (URP). Nombre del proyecto, por ejemplo `NuestroJuegoVR`.
2. Abridlo y cerradlo una vez: así Unity crea sus carpetas.
3. **Copiad todo el contenido de esta plantilla dentro de la carpeta del proyecto** (al lado de `Assets/`, `Packages/` y `ProjectSettings/`), incluidos `.gitignore` y `.gitattributes`, que son archivos ocultos. `Assets/VRKit` y `Assets/_Juego` se juntan con la `Assets/` que ya existe.
4. Abrid el proyecto otra vez.

## Paso 2 · Preparar Unity para Quest (una persona)

1. **Window › Package Manager › Unity Registry**. Instalad:
   - **XR Interaction Toolkit** (3.x). En su pestaña **Samples**, importad **Starter Assets** y también **XR Device Simulator**.
   - **XR Plugin Management**, **OpenXR Plugin** y **Unity OpenXR: Meta**.
   - **AI Navigation**, para enemigos que andan.
2. **Edit › Project Settings › XR Plug-in Management**, pestaña Android: marcad **OpenXR** y el grupo **Meta Quest**. Abrid **Project Validation** y pulsad **Fix All**.
3. **Edit › Project Settings › Editor**: comprobad que *Version Control* está en **Visible Meta Files** y *Asset Serialization* en **Force Text**. Es lo que viene por defecto en Unity 6.
4. **File › Build Profiles** › **Meta Quest** (o Android) › **Switch Platform**.
5. **Window › TextMeshPro › Import TMP Essential Resources**.
6. **Tools › VRKit › Módulos y tipos de juego**: todo debe salir en **OK**.

## Paso 3 · Subirlo a GitHub (una persona)

1. GitHub Desktop › **File › Add local repository** › elegid la carpeta del proyecto › «create a repository» › **Create**.
2. Primer commit: «Proyecto inicial con VRKit».
3. **Publish repository** › marcad **Keep this code private**.
4. En github.com › el repositorio › **Settings › Collaborators** › invitad a vuestro compañero o compañera.

## Paso 4 · Cada uno en su ordenador (cada uno)

1. La otra persona: GitHub Desktop › **Clone repository**, y luego Unity Hub › **Add › Add project from disk**.
2. **Los dos:** ejecutad una vez `Herramientas/configurar-git-windows.bat`, o `bash Herramientas/configurar-git-mac.sh` en Mac. Activa Git LFS y la fusión inteligente de escenas (Smart Merge).
3. La primera vez que abrís el proyecto tarda: Unity regenera `Library/`, que no se sube.

## Paso 5 · Primera escena VR (5 minutos)

1. `Assets/_Juego/Escenas` › **Create › Scene** › `Pruebas_TuNombre`.
2. **GameObject › VRKit › Básico › Preparar escena VR**.
3. Añadid cosas desde **GameObject › VRKit › …**
4. Play con las gafas conectadas por Quest Link, o con el XR Device Simulator.
5. Para verlo todo funcionando: escena nueva › **GameObject › VRKit › Ejemplos › Montar campo de entrenamiento completo**.

## Paso 6 · Compilar en las gafas

1. Gafas en **modo desarrollador** y conectadas por USB; aceptad «Permitir depuración USB» dentro de las gafas.
2. **File › Build Profiles** › añadid vuestras escenas a la **Scene List** › **Build And Run**.

---

## Las normas del equipo (para no pisarse)

1. **Antes de empezar: Pull.** Al acabar algo que funciona: **Commit + Push**.
2. **Commits pequeños** con mensajes claros: «Añade la puerta del sótano», no «cambios».
3. **Nunca los dos en la misma escena a la vez.** Cada uno tiene su escena de pruebas. La escena principal, avisad antes de tocarla.
4. **Prefabs para todo.** Lo que montéis en una escena, guardadlo como prefab en `_Juego/Prefabs`. Así cada uno trabaja en prefabs distintos y no hay conflictos.
5. **No editéis VRKit directamente.** Si necesitáis cambiar un script, heredad de él o copiadlo a `_Juego/Scripts` con otro nombre.
6. **Los `.blend` van en `ArtSource/`** y lo exportado (FBX) en `Assets/_Juego/Arte/Modelos`.
7. **Ramas** cuando os sintáis cómodos: una rama por tarea grande y Pull Request para juntarla con `main`.
8. **Si hay un conflicto en una escena:** no intentéis mezclarla a mano. Quedaos con una versión y repetid el cambio pequeño de la otra.

## Cosas a tener en cuenta desde el principio

- **Mareo:** teletransporte y giro por saltos por defecto, fundidos en todo cambio brusco y nunca mover la cámara sin que lo decida el jugador.
- **Rendimiento:** Quest 2 a 72 FPS. Iluminación horneada, pocos materiales, modelos ligeros. Probad en las gafas cada semana.
- **Escala:** 1 unidad = 1 metro, en Blender y en Unity. Modelad a tamaño real.
- **Git LFS:** GitHub Free trae 10 GiB de almacenamiento LFS y 10 GiB de descarga al mes. Los `.blend`, las texturas y el audio cuentan. No subáis vídeos de referencia enormes; mirad el consumo en *Settings › Billing*.
- **No subáis `Library/` ni los APK.** El `.gitignore` ya lo evita.

Más detalle de cada punto en `Docs/Guia-Empezar-el-proyecto.pdf`.
