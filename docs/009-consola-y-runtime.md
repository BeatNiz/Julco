# Consola y runtime de la pagina

## Idea

Julco puede obtener parte de la misma informacion que se ve al abrir DevTools con `F12`, pero no debe controlar DevTools como interfaz visual. La ruta mantenible es usar Chrome DevTools Protocol, que es la API oficial usada por DevTools.

## Dominios CDP relevantes

- `Runtime`: contexto JavaScript, eventos de consola y excepciones.
- `Log`: mensajes de log del navegador.
- `Debugger`: scripts cargados y ubicaciones de codigo.
- `Page`: frames y ciclo de vida.
- `DOM` y `CSS`: estructura y estilos reales.

## Politica de seguridad

Por defecto Julco puede observar:

- mensajes de consola,
- excepciones,
- scripts disponibles,
- ubicaciones de errores,
- metadatos del runtime.

Por defecto Julco no debe ejecutar JavaScript arbitrario en la pagina.

Si en el futuro se necesita `Runtime.evaluate`, debe estar detras de una opcion explicita como `AllowControlledRuntimeEvaluation` y usarse solo para consultas puntuales, documentadas y no invasivas.

## Uso en inspeccion

El resultado de inspeccion ahora puede incluir `RuntimeConsoleSnapshot`, que agrupa:

- mensajes de consola,
- excepciones,
- scripts conocidos,
- bandera que indica si se uso evaluacion runtime.

Esto permite una pestaña `Console` en la UI sin mezclar el panel lateral con comandos CDP.

## Relacion con fallback

No es un fallback visual ni IA. Es una via oficial complementaria para enriquecer la inspeccion cuando el DOM/CSS estan disponibles. En casos sin DOM real, como canvas o video, la consola puede dar pistas tecnicas, pero no reconstruye la interfaz por si sola.
