namespace BaseLocal;

/// <summary>Error general al guardar o cargar datos. El mensaje está pensado para ser leído por el alumno.</summary>
public class PersistenciaException : Exception
{
    public PersistenciaException(string mensaje) : base(mensaje) { }
    public PersistenciaException(string mensaje, Exception? inner) : base(mensaje, inner) { }
}

/// <summary>El archivo no es un JSON válido o está incompleto (por ejemplo, se cortó la escritura).</summary>
public sealed class ArchivoCorruptoException : PersistenciaException
{
    public string Ruta { get; }

    public ArchivoCorruptoException(string ruta, string detalle, Exception? inner = null)
        : base($"El archivo de datos '{ruta}' está dañado o incompleto: {detalle}", inner)
    {
        Ruta = ruta;
    }
}

/// <summary>
/// El archivo fue guardado con otra <see cref="Persistencia.VersionModelo"/> que la actual:
/// las clases cambiaron desde que se guardaron los datos.
/// </summary>
public sealed class VersionIncompatibleException : PersistenciaException
{
    public string Ruta { get; }
    public int VersionArchivo { get; }
    public int VersionEsperada { get; }

    public VersionIncompatibleException(string ruta, int versionArchivo, int versionEsperada)
        : base($"El archivo '{ruta}' se guardó con la versión {versionArchivo} del modelo, pero el programa " +
               $"usa la versión {versionEsperada} (Persistencia.VersionModelo). Seguramente cambiaste las clases. " +
               "Podés borrar el archivo con Persistencia.Eliminar() para empezar de cero, o volver a la versión " +
               "anterior de las clases.")
    {
        Ruta = ruta;
        VersionArchivo = versionArchivo;
        VersionEsperada = versionEsperada;
    }
}
