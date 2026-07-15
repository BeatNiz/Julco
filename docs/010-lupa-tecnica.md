# Lupa tecnica redimensionable

## Concepto

Julco puede ofrecer un encuadre persistente que el usuario mueve y redimensiona sobre la pantalla. Ese encuadre funciona como una lupa tecnica: mientras cambia de posicion o tamaño, Julco refresca la inspeccion de lo que queda dentro del marco.

## Comportamiento esperado

- El usuario crea un marco sobre la pantalla.
- El marco puede moverse.
- El marco puede redimensionarse desde bordes o esquinas.
- Puede fijarse temporalmente para evitar movimientos accidentales.
- Puede cambiar zoom visual sin alterar las coordenadas reales de inspeccion.
- El panel lateral se refresca usando:
  - el punto central del marco, para inspeccion principal,
  - la region completa, para listar elementos dentro del encuadre.

## Relacion con DOM real

La lupa no debe reconstruir HTML por OCR. Si el navegador es accesible, el marco solo sirve para determinar coordenadas y region. La informacion tecnica sale de CDP:

- `DOM.getNodeForLocation` para el punto central,
- DOM/CSS/Accessibility para el nodo,
- busqueda regional cuando esa fase este implementada.

## Fallback futuro

Si no hay DOM real, el mismo encuadre podria alimentar un modulo visual futuro. Ese modulo debe ser independiente y opcional.

## Modelo actual

Se agregaron:

- `SelectionMode.Lens`
- `LensFrameState`
- `ILensOverlayService`
- `ILensOverlaySession`
- `InspectionTrigger.LensCenter`
- `InspectionTrigger.LensRegion`

La UI ya incluye una primera ventana de lupa real en WPF:

- ventana transparente siempre encima,
- movimiento desde la barra superior,
- redimensionado desde la esquina inferior derecha,
- centro visual,
- controles `Centro` y `Auto` movidos al panel principal de Julco para que la lupa pueda hacerse pequeña sin perder botones,
- modo compacto automatico cuando solo hay un monitor,
- modo amplio automatico cuando hay dos o mas monitores,
- colocacion de Julco en monitor secundario cuando exista,
- ventanas de resultado colocadas en el monitor disponible mas conveniente,
- soporte DPI `PerMonitorV2` para mezclas de escala/resolucion entre monitores,
- botones de resultado `DOM`, `CSS`, `Consola` y `Atributos` que abren ventanas pequenas bajo demanda,
- barra modular inspirada en Starship con estado de CDP, navegador, pestana, lupa, monitor y resultado,
- uso de `FiraCode Nerd Font` cuando esta instalada para iconos compactos,
- calculo de centro mediante `PointToScreen` para mejorar soporte con DPI/escalado.

La tasa de refresco del monitor no afecta directamente el calculo de coordenadas; Julco no asume Hz fijo.

## Limitaciones actuales

La inspeccion del centro usa una evaluacion controlada via CDP con `document.elementFromPoint`. No es OCR ni IA, pero todavia puede requerir ajustes finos en navegadores con barras, zoom o escalado inusual.
