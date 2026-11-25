# VRKit · Módulo Objetos e inventario

Cosas que se recogen, se guardan, se comen, se mezclan o se rompen.
**Depende de:** Básico (usa `VRItem`, `ObjectiveTracker`, `ProgressSave`). **Para:** exploración, cozy, alquimia, terror, recolección, supervivencia.

| Script | Qué hace | Detalles |
|---|---|---|
| `Collectible` | Moneda, gema, seta, página… que se recoge | *Touch* (tocar con la mano o atravesar con la cabeza) o *Grab*. Puntos, objetivo, contador guardado (`Saved Counter`), curación, reaparición. Con `Unique Id`, una vez cogido no vuelve nunca. |
| `Consumable` | Se come o se bebe **acercándolo a la boca** | Cura, cuenta usos (bocados o tragos), puede dejar otro objeto al acabarse (frasco vacío). *On Consumed* para efectos. |
| `BackpackStorage` | Mochila invisible sobre el hombro | Suelta un objeto en la zona = guardado; mano vacía + grip en la zona = sacas el último. Capacidad y filtro por categoría (`VRItem`). |
| `CraftingStation` + `CraftingRecipe` | Caldero, yunque, horno, mesa de alquimia | Recetas con **Create › VRKit › Receta** (ingredientes por id y cantidad → prefab resultado). Fabrica sola (*Auto Craft*) o con `Craft()`. Si la mezcla ya no lleva a ninguna receta, «explota» y devuelve los ingredientes. |
| `Flashlight` | Linterna, antorcha, varita de luz | Gatillo para encender. Batería opcional. |
| `Breakable` | Jarrones, cajas, cristales | Cambia el objeto por sus trozos con un golpe fuerte o al morir. |

## Menú GameObject › VRKit › Objetos e inventario
Moneda coleccionable · Poción para beber · Caldero con recetas e ingredientes (dos cestas, dos recetas y un cartel) · Mochila al hombro (va en el XR Origin) · Linterna.

## Cosas a tener en cuenta
- Los ingredientes necesitan `VRItem` (con su **id**) y un Rigidbody.
- La zona de la mochila es hija de un `BodyFollower`. Ajustad su posición si no os queda cómoda al alcanzarla por encima del hombro.
- En terror, una linterna con batería y un `TriggerZone` que la apague crea más tensión que cualquier enemigo.
