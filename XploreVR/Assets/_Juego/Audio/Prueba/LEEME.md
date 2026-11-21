# Audios de prueba (VRKit)

81 sonidos sintetizados por ordenador, **sin licencia que respetar**: podéis usarlos, cambiarlos y publicarlos.
Son para probar y poder trabajar con sonido desde ya; para la versión final, cambiadlos por otros mejores (ver abajo).

## Cómo ponerlos

1. Descomprimid el zip **encima de la carpeta del proyecto** (la que tiene `Assets`). Si pregunta, *reemplazar*. No borra nada del VRKit.
2. En Unity: **Tools › VRKit › Audios de prueba › Poner en los huecos vacíos**.
   - Rellena todos los huecos de sonido que estén vacíos: en las escenas abiertas, en los prefabs de `Assets/_Juego` y `Assets/VRKit` y en los biomas del mundo abierto.
   - No toca los sonidos que ya hayáis puesto vosotros. Si queréis cambiarlo todo: *Poner y reemplazar los que haya*.
3. Guardad la escena (Ctrl+S).

Para cambiar uno: arrastrad otro audio al hueco en el Inspector, o reemplazad el `.ogg` por otro con **el mismo nombre** y volved a usar el menú.

## Qué hay

| Carpeta | Qué es |
|---|---|
| `Musica` (6, 72 s, en bucle) | `musica_pradera_dia`, `musica_bosque_dia`, `musica_desierto_dia`, `musica_montana_dia`, `musica_noche_1`, `musica_noche_2` |
| `Ambiente` (9, 32 s, en bucle) | pradera de día, bosque de día, río, desierto, montaña, nieve, playa, noche (grillos) y cueva |
| `Efectos` (66) | todo lo demás |

Efectos por grupo:

- **Básico**: `coger`, `impacto`, `aparece`, `boton_3d`, `tutorial_hecho`, `objetivo_progreso`, `objetivo_completo`, `impacto_proyectil`, `ui_click` (de sobra, para vuestros menús)
- **Mecanismos**: `puerta_abrir`, `puerta_cerrar`, `puerta_cerrada`, `puerta_desbloquear`, `palanca_click`, `dial_paso`, `deslizador_tope`, `puzle_resuelto`, `portal`
- **Objetos**: `recoger`, `consumir`, `romper_objeto`, `linterna_click`, `mochila_guardar`, `mochila_sacar`, `mochila_llena`, `caldero_anadir`, `caldero_exito`, `caldero_fallo`
- **Armas y poderes**: `disparo`, `sin_municion`, `diana`, `arco_cargar`, `arco_soltar`, `flecha_impacto`, `espada_golpe`, `espada_golpe_2`, `espada_movimiento`, `escudo_bloqueo`, `granada_armar`, `explosion`, `hechizo_cargar`, `hechizo_lanzar`, `hechizo_cambiar`, `telequinesis`, `gancho_disparo`, `gancho_engancha`
- **Enemigos**: `enemigo_alerta`, `enemigo_ataque`, `enemigo_herido`, `enemigo_muerte`, `dron_disparo`, `dron_muerte`, `oleada`
- **Mundo de bloques**: `bloque_romper`, `bloque_poner`, `golpe_mano`, `denegado`, `recoger_bloque`, `fabricar`, `mob_grunido`, `mob_herido`, `mob_muerte`
- **Mundo abierto**: `descubrir`, `cueva_entrar`, `ciervo`, `sombra_nocturna`

## Sonidos de verdad, gratis

Buscad licencia **CC0** (no hay que citar a nadie) o CC-BY (hay que poner el autor en los créditos):

- **Kenney.nl** › Assets › Audio: packs CC0 de interfaz, impactos, RPG, ciencia ficción.
- **Freesound.org**: filtrad por licencia «Creative Commons 0». Ideal para ambientes (bosque, río, viento).
- **OpenGameArt.org**: música y efectos para juegos; mirad la licencia de cada uno.
- **Pixabay.com/sound-effects** y **/music**: gratis para usar en juegos.
- **Sonniss GDC Game Audio Bundle**: miles de efectos profesionales gratis (se publica cada año).

Formato recomendado: `.ogg` o `.wav`. Los efectos, cortos y en mono; la música, en estéreo.
