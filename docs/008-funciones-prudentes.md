# Funciones prudentes agregadas

## Configuración

Se agregó configuración tipada con valores por defecto para tema, idioma, atajo, capturas, exportación e historial.

Implementaciones iniciales:

- `InMemorySettingsStore`
- `JsonSettingsStore`

## Historial

Se agregó historial en memoria con límite máximo. Guarda entradas ligeras, no snapshots completos.

Motivo: el historial del MVP debe ser útil, pero no debe almacenar datos excesivos de páginas inspeccionadas.

## Exportación

Se agregaron exportadores para:

- JSON,
- OuterHTML,
- computed CSS,
- reglas CSS,
- XPath,
- selector CSS,
- accesibilidad.

## Portapapeles

Se agregó `IClipboardService` en el núcleo y una implementación WPF inicial en la UI.

## Regla de diseño

Estas funciones no conocen CDP, navegador ni overlay. Consumen `InspectionResult`, lo que mantiene el sistema extensible.
