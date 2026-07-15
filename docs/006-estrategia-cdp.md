# Estrategia CDP para el MVP

## Objetivo

Implementar Chrome y Edge con Chrome DevTools Protocol sin depender de OCR ni de reconstrucción visual.

## Secuencia inicial

1. Descubrir endpoint CDP local.
2. Listar targets con `/json/list`.
3. Elegir target asociado a la ventana bajo el cursor.
4. Conectar al WebSocket del target.
5. Activar dominios necesarios:
   - `DOM`
   - `CSS`
   - `Page`
   - `Runtime`
   - `Overlay`
   - `Accessibility`
   - `Runtime`
   - `Log`
   - `Debugger`
6. Mapear coordenadas de pantalla a viewport.
7. Usar `DOM.getNodeForLocation`.
8. Obtener:
   - `DOM.getOuterHTML`
   - `CSS.getComputedStyleForNode`
   - `CSS.getMatchedStylesForNode`
   - `Accessibility.getPartialAXTree`
   - mensajes de consola, excepciones y scripts cargados
9. Devolver `InspectionResult`.

## Primera prueba tecnica

La primera prueba real debe inspeccionar un elemento simple en una página local o estática:

- validar target correcto,
- validar coordenadas,
- validar tag,
- validar outerHTML,
- validar computed style.

## Riesgo principal

CDP no siempre está disponible si el navegador no expone debugging remoto. El MVP debe detectar este caso y mostrar un estado claro, sin intentar técnicas invasivas.

## Regla de seguridad

No ejecutar JavaScript arbitrario para reconstruir la página. Si se usa `Runtime.evaluate`, debe estar justificado, aislado y limitado a inspección.
