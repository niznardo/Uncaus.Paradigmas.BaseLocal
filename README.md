# BaseLocal

Persistencia local para el proyecto final de **Paradigmas de Programación**: guarda **todos tus objetos en un único archivo JSON** en tu PC y los recupera juntos, sin base de datos. Reemplaza al antiguo `BinaryFormatter`.

- .NET 8 / C#. Usa `Newtonsoft.Json`, que ya viene incluida en la carpeta (sin instalar nada).
- No hace falta anotar las clases: herencia, clases abstractas, interfaces, referencias circulares, setters privados, propiedades de solo lectura, `record`, `enum`, `Dictionary`, `DateTime`, `decimal`... funcionan tal cual.

## Cómo usarla (3 pasos)

**1. Tener una clase raíz** que contenga todo lo que el programa debe recordar:

```csharp
public class Sistema
{
    public List<Cliente>  Clientes  { get; } = new();
    public List<Producto> Productos { get; } = new();
    public List<Pedido>   Pedidos   { get; } = new();
}
```

**2. Al iniciar el programa**, cargarla (o crearla vacía la primera vez):

```csharp
var sistema = Persistencia.CargarOCrear<Sistema>();
```

**3. Cuando quieras guardar** (después de cada cambio, o al salir):

```csharp
Persistencia.Guardar(sistema);
```

Eso es todo. Mirá `samples/EjemploTienda` para un ejemplo completo.

> **Importante:** guardá siempre desde **una sola raíz**. Si guardás cada lista por separado, los objetos compartidos (por ejemplo el mismo `Cliente` referenciado desde un `Pedido` y desde la lista de clientes) se duplican al cargar.

## Cómo agregarla a tu proyecto

La carpeta `BaseLocal` es autocontenida: incluye `Newtonsoft.Json.dll` en `BaseLocal\lib`, así que **no necesita internet ni NuGet** y no hay que instalar nada más.

1. Descomprimí el zip y copiá la carpeta **`BaseLocal`** dentro de la carpeta de tu solución, al lado de tu proyecto:

   ```
   MiSolucion\
     MiSolucion.sln
     MiTP\            <- tu proyecto
     BaseLocal\       <- la carpeta copiada
   ```

2. Agregala a tu solución y referenciala desde tu proyecto (desde la carpeta de la solución):

   ```bash
   dotnet sln add BaseLocal\BaseLocal.csproj
   dotnet add MiTP reference BaseLocal\BaseLocal.csproj
   ```

   **En Visual Studio:** clic derecho en la solución → Agregar → Proyecto existente → `BaseLocal\BaseLocal.csproj`. Luego clic derecho en *tu proyecto* → Agregar → Referencia de proyecto → tildar `BaseLocal`.

3. Escribí `using BaseLocal;` y listo.

> No agregues además el paquete `Newtonsoft.Json` de NuGet a tu proyecto salvo que lo necesites para otra cosa; ya viene incluida.

## Para el profesor: armar el zip de distribución

```bash
powershell -File distribucion\armar-zip.ps1
```

Genera `distribucion\BaseLocal.zip` con la carpeta `BaseLocal` (sin `bin`/`obj`) y este README.

## Opciones

| Propiedad / método | Para qué sirve |
|---|---|
| `Persistencia.VersionModelo = 2;` | **Subilo cada vez que cambies campos o propiedades** de las clases guardadas. Si el archivo es de otra versión, se lanza `VersionIncompatibleException` con un mensaje claro en lugar de cargar datos inconsistentes. |
| `Persistencia.CarpetaDatos = AppContext.BaseDirectory;` | Dónde se guarda (por defecto `%LocalAppData%\<tu programa>`). |
| `Persistencia.ObtenerRuta()` | Muestra la ruta completa del archivo. |
| `Persistencia.Existe()` / `Persistencia.Eliminar()` | Saber si hay datos guardados / empezar de cero. |
| `Guardar(obj, "otro.json")` | Usar otro nombre de archivo (o una ruta completa). |
| `Persistencia.Advertencia = Console.WriteLine;` | Recibir avisos (por ejemplo "se recuperó el respaldo"). |

## Qué pasa si algo sale mal

- **Se corta la luz mientras guarda:** la escritura es segura (archivo temporal + reemplazo). El archivo anterior no se pierde y queda una copia `datos.json.bak`.
- **El archivo está dañado:** se carga automáticamente el `.bak`; si tampoco sirve, se lanza `ArchivoCorruptoException`.
- **Cambiaste las clases:** subí `VersionModelo`; con datos viejos incompatibles lanzará `VersionIncompatibleException` (borrá el archivo con `Persistencia.Eliminar()` o volvé a la versión anterior de las clases).
- **Primera ejecución:** `Cargar` devuelve `null`; `CargarOCrear` crea un objeto nuevo.

## Qué se guarda y qué no

Se guardan los **campos** de los objetos (públicos y privados, incluidos los de las propiedades automáticas), igual que hacía `BinaryFormatter`.

No se guardan: propiedades calculadas (sin campo propio), eventos y delegados, campos marcados `[NonSerialized]` o `[JsonIgnore]`, ni miembros `static`.

## Limitaciones

- Pensada para **pocos datos**: carga y guarda todo en memoria en cada operación. No es una base de datos.
- Un solo programa a la vez sobre el mismo archivo.
- No guardes objetos del sistema ligados a recursos: `Thread`, `Stream`, controles de WinForms/WPF, `DbContext`, etc. Guardá solo tus clases de dominio.
- `Dictionary` con claves que son objetos propios: usá claves `string`, `int`, `Guid` o `enum`.
- Si usás `[OnDeserialized]` en una clase, se ejecuta al cargar (sirve para reconstruir datos calculados).
- Al cargar sólo se crean tipos de tu programa y tipos básicos del framework; un archivo manipulado a mano no puede instanciar clases peligrosas.

## Estructura del repositorio

```
src/BaseLocal            la librería
samples/EjemploTienda    ejemplo de consola (herencia, interfaces, referencias circulares)
tests/BaseLocal.Tests    pruebas automáticas (xUnit)
```

```bash
dotnet test
```
