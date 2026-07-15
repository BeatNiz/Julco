# Julco

Julco es una aplicación de escritorio para Windows orientada a inspeccionar interfaces web visibles en pantalla. Su experiencia debe parecerse a la herramienta de recortes de Windows, pero su objetivo principal es obtener DOM real, CSS real y datos técnicos del elemento seleccionado usando APIs oficiales del navegador.

## Principios

- Priorizar DOM real sobre OCR o reconstrucción visual.
- Usar Chrome DevTools Protocol para navegadores Chromium en el MVP.
- Mantener la UI separada de los detalles de cada navegador.
- Diseñar la arquitectura para admitir Firefox, plugins y reconstrucción visual futura sin contaminar el núcleo.
- No modificar páginas ni enviar datos a Internet.

## Alcance prudente inicial

Esta base no implementa todo Julco. Deja listo lo necesario para trabajar iterativamente:

- documentación técnica inicial,
- decisión tecnológica inicial,
- estructura de carpetas,
- contratos de dominio orientados a targets, snapshots y coordenadas,
- adaptador CDP como esqueleto,
- UI WPF mínima como punto de entrada,
- prueba mínima de modelo.

## MVP

El MVP debe incluir:

- Chrome y Edge,
- selección tipo `Win + Shift + S`,
- inspección mediante CDP,
- HTML, CSS y computed style,
- captura del elemento,
- copiar HTML y CSS,
- tema oscuro,
- historial simple.

## Estructura

```text
docs/                 Decisiones, arquitectura, riesgos y roadmap
src/Julco.Core        Modelos y contratos puros
src/Julco.Browser     Coordinación de navegadores y adaptadores
src/Julco.Cdp         Adaptador Chrome DevTools Protocol
src/Julco.Capture     Selección de pantalla y traducción de coordenadas
src/Julco.Configuration Configuración local
src/Julco.Export      Exportación de resultados
src/Julco.History     Historial de inspecciones
src/Julco.UI          Aplicación Windows
tests/                Pruebas automatizadas
```

## Flujo tecnico previsto

```text
Atajo global
  -> overlay de seleccion
  -> lupa tecnica opcional
  -> ventana bajo el cursor
  -> target de navegador
  -> adaptador CDP
  -> mapeo pantalla/viewport
  -> DOM.getNodeForLocation
  -> snapshots DOM/CSS/Accessibility
  -> panel lateral
```

## Contratos centrales

- `BrowserTarget`: pestaña o pagina inspeccionable expuesta por el navegador.
- `InspectionRequest`: punto o region seleccionada mas opciones de captura.
- `InspectionResult`: paquete tecnico de DOM, CSS, accesibilidad y advertencias.
- `CoordinateMapping`: base para convertir coordenadas de pantalla a viewport.
- `IBrowserAdapter`: limite entre Julco y cada navegador soportado.
- `IInspectionExporter`: limite para exportar HTML, CSS, JSON y selectores.
- `IInspectionHistoryStore`: historial desacoplado de UI y navegador.
- `IClipboardService`: portapapeles como servicio reemplazable.
- `RuntimeConsoleSnapshot`: contexto de consola/runtime obtenido via CDP.
- `ILensOverlaySession`: encuadre redimensionable/movible para refrescar inspecciones.

## Siguiente paso recomendado

Instalar el SDK de .NET 8 o superior y validar la solución con:

```powershell
dotnet test
```

En esta máquina `dotnet` no está disponible todavía, por eso la validación de compilación queda pendiente.
