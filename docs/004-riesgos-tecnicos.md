# Riesgos técnicos

## DPI y coordenadas

Riesgo: las coordenadas de Windows, navegador y viewport pueden diferir por zoom, DPI mixto o monitores múltiples.

Mitigación: crear pruebas manuales específicas por escala de Windows, zoom de navegador y monitor.

## Múltiples ventanas del navegador

Riesgo: seleccionar la ventana incorrecta o conectarse a una pestaña distinta.

Mitigación: correlacionar ventana del sistema, proceso, título, bounds y targets CDP.

Estado de diseño: `BrowserManager` ya prefiere targets inspeccionables cuyo `WindowBounds` contiene el punto seleccionado. Falta implementar la obtención real de esos bounds.

## CDP no disponible

Riesgo: el navegador no fue iniciado con debugging remoto o bloquea el endpoint.

Mitigación: documentar requisitos y ofrecer guía de activación segura por navegador.

Estado de diseño: los adaptadores exponen `DiscoverTargetsAsync`; ese método debe devolver una lista vacía o advertencias controladas cuando CDP no esté disponible.

## Iframes y cross-origin

Riesgo: restricciones de origen y frames anidados dificultan ubicar el nodo real.

Mitigación: usar dominios CDP de DOM/Page/Runtime y documentar límites por caso.

## Shadow DOM cerrado

Riesgo: no todo Shadow DOM es inspeccionable.

Mitigación: exponer limitación claramente y devolver metadatos útiles cuando sea posible.

## Sitios muy dinámicos

Riesgo: el DOM puede cambiar entre selección e inspección.

Mitigación: reducir latencia, capturar snapshot técnico y registrar tiempos.
