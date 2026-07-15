# Arquitectura

## Regla central

La UI no conoce CDP ni detalles de navegador. La UI solicita una inspección al núcleo, y el núcleo coordina servicios mediante interfaces.

## Componentes

```text
Julco.UI
  -> Julco.Capture
  -> Julco.Browser
  -> Julco.Configuration
  -> Julco.Export
  -> Julco.History
      -> Julco.Core
      -> Julco.Cdp
          -> Chrome / Edge via CDP
```

## Módulos iniciales

- `Julco.Core`: modelos, contratos y tipos compartidos.
- `Julco.Browser`: selección del adaptador correcto y ciclo de vida de conexión.
- `Julco.Cdp`: implementación futura de Chrome DevTools Protocol.
- `Julco.Capture`: overlay, selección de pantalla y coordenadas.
- `Julco.Configuration`: carga y guardado local de preferencias.
- `Julco.Export`: exportadores de resultados de inspección.
- `Julco.History`: historial de inspecciones recientes.
- `Julco.UI`: panel lateral, pestañas y experiencia de usuario.

## Contrato de adaptador

El adaptador de navegador debe exponer operaciones de alto nivel:

- descubrir targets inspeccionables,
- conectar a un target concreto,
- inspeccionar un punto mediante una solicitud completa,
- encontrar elementos en una región,
- devolver snapshots de DOM, CSS y accesibilidad,
- resaltar nodo.

El resto de la aplicación debe trabajar con `InspectionResult`, no con comandos CDP sueltos.

## Modelo de inspección

```text
BrowserTarget
  identifica pestaña, URL, proceso y bounds de ventana

InspectionRequest
  target + punto/region + opciones

InspectionResult
  elemento + DOM + CSS + accesibilidad + advertencias
```

Este modelo evita acoplar el panel lateral a detalles como `DOM.getNodeForLocation`, `CSS.getMatchedStylesForNode` o IDs internos de CDP.

## Coordenadas

La conversión debe tratarse como un problema de primer nivel:

- coordenadas físicas de pantalla,
- bounds de ventana,
- área real del viewport,
- zoom de navegador,
- `devicePixelRatio`,
- escala de Windows,
- monitores múltiples.

La primera implementación validará punto único antes de región.

## Lupa tecnica

El overlay puede evolucionar a una sesion persistente:

```text
LensOverlaySession
  -> mueve/redimensiona marco
  -> emite cambio
  -> BrowserManager inspecciona centro o region
  -> UI refresca panel
```

La lupa pertenece a `Julco.Capture`; la inspeccion sigue pasando por `Julco.Browser` y los adaptadores. Esto evita que el overlay conozca CDP.

## Dependencias permitidas

- `Julco.Core` no depende de otros proyectos.
- `Julco.Browser` depende de `Julco.Core`.
- `Julco.Cdp` depende de `Julco.Core`.
- `Julco.Capture` depende de `Julco.Core`.
- `Julco.Configuration` depende de `Julco.Core`.
- `Julco.Export` depende de `Julco.Core`.
- `Julco.History` depende de `Julco.Core`.
- `Julco.UI` puede depender de los módulos anteriores.

## Dependencias prohibidas

- `Julco.Core` no debe depender de UI.
- `Julco.UI` no debe llamar directamente a comandos CDP.
- Un adaptador no debe depender de otro adaptador.
- La captura de pantalla no debe contener lógica de DOM.
- Exportación, historial y portapapeles no deben conocer CDP.

## Regla para funcionalidades futuras

Firefox, plugins, IA, exportadores y reconstrucción visual deben entrar como módulos nuevos detrás de contratos existentes o contratos nuevos en `Julco.Core`. No deben añadir dependencias directas hacia `Julco.UI`.
