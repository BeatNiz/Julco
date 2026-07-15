# Modelo de datos inicial

## `BrowserTarget`

Representa una página o pestaña inspeccionable. Contiene identificador del target, navegador, titulo, URL, proceso y bounds de ventana cuando estén disponibles.

## `InspectionRequest`

Representa una intención de inspección:

- target,
- punto de pantalla,
- región opcional,
- opciones de captura.

## `InspectionResult`

Representa el paquete que consume la UI:

- target inspeccionado,
- elemento,
- snapshot DOM,
- snapshot CSS,
- snapshot de accesibilidad,
- advertencias.

## `InspectedElement`

No guarda solo HTML. Guarda identidad, selectores, atributos, bounds, profundidad y relaciones básicas. El HTML vive en `DomSnapshot`.

## `CssSnapshot`

Separa:

- computed declarations,
- reglas coincidentes,
- variables CSS,
- pseudo-elementos.

## `InspectionWarning`

Permite informar límites sin romper el flujo:

- iframe cross-origin,
- Shadow DOM cerrado,
- canvas,
- target no correlacionado con ventana,
- CDP parcial.

## `RuntimeConsoleSnapshot`

Guarda informacion obtenida de la consola/runtime de la pagina:

- mensajes,
- excepciones,
- scripts cargados,
- bandera sobre uso de evaluacion runtime.

No representa codigo fuente original completo. Es contexto tecnico complementario.

## `AppSettings`

Agrupa preferencias locales:

- tema,
- idioma,
- atajo global,
- directorio de capturas,
- formato de exportación,
- límite de historial.

## `ExportPackage`

Representa contenido listo para copiar o guardar:

- formato,
- extensión,
- MIME type,
- contenido.

## `InspectionHistoryEntry`

Guarda una vista ligera del resultado inspeccionado. No conserva todo el DOM ni CSS para evitar crecimiento innecesario y exposición accidental de datos sensibles.
