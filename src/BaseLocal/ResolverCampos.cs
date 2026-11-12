using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BaseLocal;

/// <summary>
/// Serializa los CAMPOS de los objetos (públicos y privados) en lugar de las propiedades, igual que hacía
/// BinaryFormatter. Así funcionan sin anotaciones: setters privados, propiedades de solo lectura,
/// campos readonly, clases sin constructor sin parámetros, etc.
/// </summary>
internal sealed class ResolverCampos : DefaultContractResolver
{
    public static readonly ResolverCampos Instancia = new();

    private const BindingFlags Todos =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private ResolverCampos()
    {
        IgnoreSerializableInterface = true;
        IgnoreSerializableAttribute = true;
    }

    protected override JsonObjectContract CreateObjectContract(Type objectType)
    {
        var contrato = base.CreateObjectContract(objectType);

        if (objectType.IsAbstract || objectType.IsInterface || objectType.IsValueType)
            return contrato;

        // Nunca usar constructores con parámetros: se usa el constructor sin parámetros (público o privado)
        // si existe, y si no, se crea el objeto sin llamar a ningún constructor.
        var ctor = objectType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        contrato.OverrideCreator = null;
        contrato.DefaultCreator = ctor != null
            ? () => ctor.Invoke(null)
            : () => RuntimeHelpers.GetUninitializedObject(objectType);
        contrato.DefaultCreatorNonPublic = false;
        return contrato;
    }

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        // Jerarquía de la clase base hacia la derivada.
        var jerarquia = new List<Type>();
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            jerarquia.Insert(0, t);

        var campos = jerarquia
            .SelectMany(t => t.GetFields(Todos))
            .Where(DebeGuardarse)
            .ToList();

        var propiedades = new List<JsonProperty>();
        foreach (var grupo in campos.GroupBy(NombreLimpio))
        {
            // Si una clase base y una derivada tienen un campo privado con el mismo nombre, se prefija el nombre.
            var hayChoque = grupo.Count() > 1;
            foreach (var campo in grupo)
            {
                var p = CreateProperty(campo, MemberSerialization.Fields);
                p.PropertyName = hayChoque ? $"{campo.DeclaringType!.Name}.{grupo.Key}" : grupo.Key;
                p.Ignored = false;
                p.Readable = true;
                p.Writable = true;
                p.ShouldSerialize = null;
                p.ShouldDeserialize = null;
                p.ValueProvider = new ReflectionValueProvider(campo); // permite asignar campos readonly
                propiedades.Add(p);
            }
        }
        return propiedades;
    }

    private static string NombreLimpio(FieldInfo campo) => EsCampoDeAutoPropiedad(campo, out var nombre) ? nombre : campo.Name;

    private static bool EsCampoDeAutoPropiedad(FieldInfo campo, out string nombre)
    {
        const string sufijo = ">k__BackingField";
        if (campo.Name.StartsWith('<') && campo.Name.EndsWith(sufijo, StringComparison.Ordinal))
        {
            nombre = campo.Name[1..^sufijo.Length];
            return true;
        }
        nombre = campo.Name;
        return false;
    }

    private static bool DebeGuardarse(FieldInfo campo)
    {
        if (typeof(Delegate).IsAssignableFrom(campo.FieldType)) return false; // eventos y delegados
        if (campo.FieldType.IsPointer || campo.FieldType.IsByRef) return false;
        if (TieneIgnore(campo)) return false;

        if (campo.Name.Contains('<'))
        {
            // Sólo los campos de auto-propiedades; el resto de lo generado por el compilador se ignora.
            if (!EsCampoDeAutoPropiedad(campo, out var nombre)) return false;
            var propiedad = campo.DeclaringType!.GetProperty(nombre, Todos | BindingFlags.Static);
            if (propiedad != null && TieneIgnore(propiedad)) return false;
        }
        return true;
    }

    // Reconoce [NonSerialized] y [JsonIgnore] (de Newtonsoft o de System.Text.Json).
    private static bool TieneIgnore(MemberInfo miembro) =>
        miembro.GetCustomAttributes(inherit: true)
               .Any(a => a is NonSerializedAttribute || a.GetType().Name == "JsonIgnoreAttribute");
}
