using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BaseLocal;

/// <summary>
/// Limita qué tipos puede crear el archivo JSON al cargarse (el campo "$type"): sólo los del programa del
/// alumno y los tipos básicos del framework. Evita que un archivo manipulado instancie clases peligrosas.
/// </summary>
internal sealed class BinderSeguro : DefaultSerializationBinder
{
    private static readonly string[] EspaciosProhibidos =
    {
        "System.Diagnostics", "System.Reflection", "System.IO", "System.Net", "System.Threading",
        "System.Windows", "System.Runtime.InteropServices", "System.CodeDom", "System.Management",
        "System.Data", "System.Security", "System.Runtime.Loader"
    };

    private readonly HashSet<Assembly> _ensambladosPermitidos = new();

    public BinderSeguro(Type tipoRaiz)
    {
        Agregar(tipoRaiz.Assembly);
        Agregar(Assembly.GetEntryAssembly());
    }

    private void Agregar(Assembly? asm)
    {
        if (asm == null || EsDelMarco(asm)) return;
        _ensambladosPermitidos.Add(asm);
        // También los proyectos/librerías propias que ese ensamblado referencia (por ejemplo un proyecto "Modelo").
        foreach (var referencia in asm.GetReferencedAssemblies())
        {
            if (referencia.Name is null || EsNombreDelMarco(referencia.Name) || referencia.Name == "Newtonsoft.Json")
                continue;
            try { _ensambladosPermitidos.Add(Assembly.Load(referencia)); }
            catch { /* no se pudo cargar: simplemente no se permite */ }
        }
    }

    public override Type BindToType(string? assemblyName, string typeName)
    {
        var tipo = base.BindToType(assemblyName, typeName);
        Validar(tipo);
        return tipo;
    }

    private void Validar(Type tipo)
    {
        if (tipo.HasElementType) { Validar(tipo.GetElementType()!); return; }
        if (tipo.IsGenericType)
            foreach (var argumento in tipo.GetGenericArguments())
                Validar(argumento);

        if (typeof(Delegate).IsAssignableFrom(tipo))
            throw new JsonSerializationException($"El tipo '{tipo}' no está permitido en el archivo de datos.");

        if (_ensambladosPermitidos.Contains(tipo.Assembly)) return;

        if (EsDelMarco(tipo.Assembly) &&
            !EspaciosProhibidos.Any(e => tipo.Namespace != null &&
                (tipo.Namespace == e || tipo.Namespace.StartsWith(e + ".", StringComparison.Ordinal))))
            return;

        throw new JsonSerializationException($"El tipo '{tipo}' no está permitido en el archivo de datos.");
    }

    private static bool EsDelMarco(Assembly asm) => EsNombreDelMarco(asm.GetName().Name ?? "");

    private static bool EsNombreDelMarco(string nombre) =>
        nombre == "mscorlib" || nombre == "netstandard" || nombre == "System" ||
        nombre.StartsWith("System.", StringComparison.Ordinal);
}
