# Plan de pruebas

## Unitarias

- Modelos de coordenadas y rectángulos.
- Selección de target por bounds de ventana.
- Estado de lupa tecnica al mover y redimensionar.
- Generación futura de selectores.
- Normalización de datos de inspección.
- Historial simple.
- Exportadores HTML, CSS, JSON y selectores.
- Exportador de consola/runtime.
- Configuración por defecto.

## Integración

- Conexión CDP con Chrome.
- Conexión CDP con Edge.
- Descubrimiento de targets CDP.
- Correlación entre ventana y target.
- `DOM.getNodeForLocation`.
- Obtención de `outerHTML`.
- Obtención de computed style.
- Captura de mensajes de consola.
- Captura de excepciones runtime.

## Manuales obligatorias del MVP

- Windows 100%, 125% y 150% de escala.
- Zoom de navegador 80%, 100%, 125% y 150%.
- Un monitor.
- Dos monitores con distinta escala.
- Página con iframe.
- Página con Shadow DOM abierto.
- React, Vue o Angular SPA.

## No funcionales

- Tiempo desde selección hasta panel menor a 500 ms en páginas simples.
- No enviar datos a red externa.
- No modificar el DOM de la página inspeccionada salvo resaltado temporal controlado.
