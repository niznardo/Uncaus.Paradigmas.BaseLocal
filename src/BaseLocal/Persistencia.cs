using System.Diagnostics;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BaseLocal;

/// <summary>
/// Guarda y recupera TODO un sistema de objetos en un único archivo JSON local.
/// <para>
/// Uso típico: tener una clase raíz (por ejemplo <c>Sistema</c>) que contenga todas las listas del programa.
/// <code>
/// var sistema = Persistencia.CargarOCrear&lt;Sistema&gt;();   // al iniciar
/// ...
/// Persistencia.Guardar(sistema);                           // cuando quieras (o al salir)
/// </code>
/// </para>
/// </summary>
public static class Persistencia
{
    private const string NombreFormato = "BaseLocal";
    private const int VersionFormato = 1;
    private static readonly object Candado = new();

    private static string? _carpetaDatos;

    /// <summary>
    /// Número de versión de tus clases. Subilo (1, 2, 3...) cada vez que cambies campos o propiedades de las
    /// clases que se guardan. Si el archivo tiene otra versión, <see cref="Cargar{T}"/> lanza
    /// <see cref="VersionIncompatibleException"/> en lugar de cargar datos inconsistentes.
    /// </summary>
    public static int VersionModelo { get; set; } = 1;

    /// <summary>
    /// Carpeta donde se guardan los archivos cuando se indica sólo un nombre. Por defecto:
    /// <c>%LocalAppData%\&lt;nombre del programa&gt;</c>. Para guardar junto al .exe:
    /// <c>Persistencia.CarpetaDatos = AppContext.BaseDirectory;</c>
    /// </summary>
    public static string CarpetaDatos
    {
        get => _carpetaDatos ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "BaseLocal");
        set => _carpetaDatos = value;
    }

    /// <summary>Nombre del archivo cuando no se indica ninguno.</summary>
    public static string ArchivoPredeterminado { get; set; } = "datos.json";

    /// <summary>Se invoca con avisos no fatales (por ejemplo, "se recuperó el respaldo"). Opcional.</summary>
    public static Action<string>? Advertencia { get; set; }

    /// <summary>Devuelve la ruta completa que se usará para un nombre de archivo (útil para mostrarla).</summary>
    public static string ObtenerRuta(string? archivo = null)
    {
        archivo = string.IsNullOrWhiteSpace(archivo) ? ArchivoPredeterminado : archivo;
        return Path.IsPathRooted(archivo) ? Path.GetFullPath(archivo) : Path.GetFullPath(Path.Combine(CarpetaDatos, archivo));
    }

    /// <summary>Indica si ya existe un archivo de datos guardado.</summary>
    public static bool Existe(string? archivo = null) => File.Exists(ObtenerRuta(archivo));

    /// <summary>Borra el archivo de datos (y su respaldo). Útil para empezar de cero.</summary>
    public static void Eliminar(string? archivo = null)
    {
        var ruta = ObtenerRuta(archivo);
        lock (Candado)
        {
            foreach (var r in new[] { ruta, ruta + ".bak", ruta + ".tmp" })
                if (File.Exists(r)) File.Delete(r);
        }
    }

    /// <summary>
    /// Guarda el objeto raíz y todo lo que contiene (listas, objetos, referencias cruzadas, herencia).
    /// La escritura es segura: si el programa se cierra a mitad, el archivo anterior no se pierde.
    /// </summary>
    public static void Guardar<T>(T raiz, string? archivo = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(raiz);
        var ruta = ObtenerRuta(archivo);

        string json;
        try
        {
            json = Serializar(raiz);
        }
        catch (Exception ex) when (ex is not PersistenciaException)
        {
            throw new PersistenciaException(
                $"No se pudo guardar el objeto de tipo '{typeof(T).Name}': {Detalle(ex)}. " +
                "Revisá que no contenga tipos no serializables (hilos, streams, controles de formulario, etc.).", ex);
        }

        lock (Candado)
        {
            try
            {
                EscribirAtomico(ruta, json);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new PersistenciaException($"No se pudo escribir el archivo '{ruta}': {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Carga el objeto raíz. Devuelve <c>null</c> si todavía no existe el archivo (primera ejecución).
    /// Si el archivo está dañado y existe el respaldo (.bak), se carga el respaldo.
    /// </summary>
    /// <exception cref="VersionIncompatibleException">El archivo es de otra versión del modelo.</exception>
    /// <exception cref="ArchivoCorruptoException">El archivo (y su respaldo) no se pueden leer.</exception>
    public static T? Cargar<T>(string? archivo = null) where T : class
    {
        var ruta = ObtenerRuta(archivo);
        var respaldo = ruta + ".bak";

        lock (Candado)
        {
            if (!File.Exists(ruta))
            {
                // Caso raro: se cortó justo durante el reemplazo y sólo quedó el respaldo.
                if (!File.Exists(respaldo)) return null;
                var recuperado = Leer<T>(respaldo);
                Avisar($"No se encontró '{ruta}'; se cargó el respaldo '{respaldo}'.");
                return recuperado;
            }

            try
            {
                return Leer<T>(ruta);
            }
            catch (ArchivoCorruptoException) when (File.Exists(respaldo))
            {
                T? recuperado;
                try { recuperado = Leer<T>(respaldo); }
                catch (ArchivoCorruptoException) { throw; } // ambos dañados: se informa el error del principal
                Avisar($"El archivo '{ruta}' estaba dañado; se cargó el respaldo '{respaldo}'.");
                return recuperado;
            }
        }
    }

    /// <summary>Carga el objeto raíz o, si no hay archivo todavía, crea uno nuevo vacío (<c>new T()</c>).</summary>
    public static T CargarOCrear<T>(string? archivo = null) where T : class, new() =>
        Cargar<T>(archivo) ?? new T();

    // ------------------------------------------------------------------------------------------------

    private static JsonSerializer CrearSerializador(Type tipoRaiz) => JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = ResolverCampos.Instancia,
        SerializationBinder = new BinderSeguro(tipoRaiz),
        TypeNameHandling = TypeNameHandling.Auto,                       // guarda la clase real (herencia / interfaces)
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        PreserveReferencesHandling = PreserveReferencesHandling.Objects, // referencias circulares y objetos compartidos
        MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        NullValueHandling = NullValueHandling.Include,
        DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
        Culture = CultureInfo.InvariantCulture,
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter() }
    });

    private static string Serializar<T>(T raiz) where T : class
    {
        var serializador = CrearSerializador(typeof(T));
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
        using (var w = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2 })
        {
            w.WriteStartObject();
            w.WritePropertyName("formato"); w.WriteValue(NombreFormato);
            w.WritePropertyName("versionFormato"); w.WriteValue(VersionFormato);
            w.WritePropertyName("versionModelo"); w.WriteValue(VersionModelo);
            w.WritePropertyName("guardadoEn"); w.WriteValue(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            w.WritePropertyName("tipoRaiz"); w.WriteValue(typeof(T).FullName);
            w.WritePropertyName("datos");
            serializador.Serialize(w, raiz, typeof(T));
            w.WriteEndObject();
        }
        return sb.ToString();
    }

    private static T? Leer<T>(string ruta) where T : class
    {
        string texto;
        try
        {
            texto = File.ReadAllText(ruta, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PersistenciaException($"No se pudo leer el archivo '{ruta}': {ex.Message}", ex);
        }

        var serializador = CrearSerializador(typeof(T));
        try
        {
            using var sr = new StringReader(texto);
            using var reader = new JsonTextReader(sr);

            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                throw new ArchivoCorruptoException(ruta, "no empieza con un objeto JSON.");

            string? formato = null;
            int? version = null;
            T? datos = null;
            var datosLeidos = false;

            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                switch ((string)reader.Value!)
                {
                    case "formato":
                        formato = reader.ReadAsString();
                        break;
                    case "versionModelo":
                        version = reader.ReadAsInt32();
                        break;
                    case "datos":
                        if (formato != NombreFormato || version is null)
                            throw new ArchivoCorruptoException(ruta, "falta la cabecera de BaseLocal (formato/versión).");
                        if (version != VersionModelo)
                            throw new VersionIncompatibleException(ruta, version.Value, VersionModelo);
                        reader.Read(); // posicionarse en el valor
                        datos = serializador.Deserialize<T>(reader);
                        datosLeidos = true;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (!datosLeidos || reader.TokenType != JsonToken.EndObject)
                throw new ArchivoCorruptoException(ruta, "el contenido está incompleto.");

            return datos;
        }
        catch (JsonReaderException ex)
        {
            throw new ArchivoCorruptoException(ruta, ex.Message, ex);
        }
        catch (JsonSerializationException ex)
        {
            throw new PersistenciaException(
                $"No se pudieron reconstruir los objetos de '{ruta}': {Detalle(ex)}. " +
                "Si cambiaste las clases, subí Persistencia.VersionModelo o borrá el archivo con Persistencia.Eliminar().", ex);
        }
    }

    private static void EscribirAtomico(string ruta, string json)
    {
        var carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);

        var temporal = ruta + ".tmp";
        var respaldo = ruta + ".bak";

        using (var fs = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
        {
            sw.Write(json);
            sw.Flush();
            fs.Flush(flushToDisk: true);
        }

        if (File.Exists(ruta)) File.Replace(temporal, ruta, respaldo); // deja la versión anterior como .bak
        else File.Move(temporal, ruta);
    }

    private static string Detalle(Exception ex) => ex.InnerException is { } i ? $"{ex.Message} ({i.Message})" : ex.Message;

    private static void Avisar(string mensaje)
    {
        Debug.WriteLine($"[BaseLocal] {mensaje}");
        Advertencia?.Invoke(mensaje);
    }
}
