# Decisión tecnológica inicial

## Decisión

Para el MVP, Julco usará:

- C# / .NET 8 o superior
- WPF para la aplicación Windows inicial
- Chrome DevTools Protocol para Chrome y Edge
- WebSocket para conexión CDP
- arquitectura modular con proyectos separados

## Motivos

WPF ofrece una ruta pragmática para overlays de escritorio, atajos globales, ventanas laterales y APIs nativas de Windows con baja fricción. No es la UI más moderna de Microsoft, pero es estable, documentada y muy adecuada para herramientas internas o de productividad en Windows.

C# reduce el costo de integración con Windows frente a Electron o Tauri cuando el MVP requiere coordenadas de pantalla, DPI, múltiples monitores y ventanas superpuestas.

CDP es la opción más robusta para navegadores Chromium porque expone DOM, CSS, runtime, accessibility, overlay y capturas sin depender de OCR.

## Alternativas consideradas

| Opción | Ventajas | Costos / riesgos |
| --- | --- | --- |
| Electron + TypeScript | Gran ecosistema frontend, CDP natural | Más memoria, integración nativa de Windows más pesada |
| Tauri + Rust/TS | Menor memoria que Electron, buen empaquetado | Más complejidad en overlay nativo y CDP si el equipo no domina Rust |
| WinUI 3 | UI moderna | Mayor fricción inicial, tooling más sensible |
| C++ nativo | Máximo control | Mucho mayor costo de desarrollo y mantenimiento |
| WPF + C# | Productivo, estable, buen acceso a Windows | UI menos moderna si no se cuida diseño |

## Revisión futura

La decisión puede revisarse después de validar:

- overlay con DPI mixto,
- hit testing preciso en CDP,
- conexión estable con múltiples ventanas,
- empaquetado e instalador.

## Decisión de alcance

La primera implementación real debe limitarse a Chromium CDP para Chrome y Edge. Brave, Opera y Vivaldi reutilizarán el mismo adaptador cuando la base esté estable, pero no deben entrar antes de validar el flujo completo en Chrome y Edge.
