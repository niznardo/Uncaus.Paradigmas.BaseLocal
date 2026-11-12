namespace BaseLocal.Tests;

// Modelo de prueba con todo lo que suelen usar los alumnos: herencia, interfaces, referencias circulares,
// setters privados, propiedades de solo lectura, campos readonly, enums, structs, diccionarios, etc.

public enum Categoria { Hogar, Tecnologia, Libros }

public interface IDescontable
{
    decimal Descuento { get; }
}

public abstract class Producto
{
    public string Nombre { get; private set; }
    public decimal Precio { get; protected set; }
    public Categoria Categoria { get; set; }
    public abstract string Describir();

    protected Producto(string nombre, decimal precio) { Nombre = nombre; Precio = precio; }
}

public class ProductoFisico : Producto, IDescontable
{
    public double PesoKg { get; set; }
    public decimal Descuento => 0.1m; // calculada: no debe guardarse
    public ProductoFisico(string nombre, decimal precio, double pesoKg) : base(nombre, precio) { PesoKg = pesoKg; }
    public override string Describir() => $"{Nombre} ({PesoKg} kg)";
}

public class ProductoDigital : Producto
{
    public string UrlDescarga { get; set; } = "";
    public ProductoDigital(string nombre, decimal precio) : base(nombre, precio) { }
    public override string Describir() => $"{Nombre} (descarga)";
}

public class Cliente
{
    private readonly List<Pedido> _pedidos = new();          // campo readonly
    public string Nombre { get; }                             // sólo lectura
    public IReadOnlyList<Pedido> Pedidos => _pedidos;          // expone sólo lectura
    public DateTime Alta { get; set; }
    public Cliente(string nombre) { Nombre = nombre; }
    public void AgregarPedido(Pedido p) => _pedidos.Add(p);
}

public class Pedido
{
    public Cliente Cliente { get; }                           // referencia circular Cliente <-> Pedido
    public List<Producto> Items { get; } = new();
    public DateTimeOffset Fecha { get; set; }
    public Pedido(Cliente cliente) { Cliente = cliente; cliente.AgregarPedido(this); }
}

public struct Punto { public int X; public int Y; }

public class Sistema
{
    public List<Cliente> Clientes { get; } = new();
    public List<Producto> Productos { get; } = new();
    public List<Pedido> Pedidos { get; } = new();
    public Dictionary<string, int> Stock { get; } = new();
    public Dictionary<Categoria, decimal> Recargos { get; } = new();
    public IDescontable? Promocion { get; set; }              // campo tipado como interfaz
    public Punto Ubicacion { get; set; }
    public event EventHandler? Cambio;                        // evento: no debe guardarse
    [NonSerialized] public string Temporal = "no guardar";
    public int? Opcional { get; set; }
    public void Notificar() => Cambio?.Invoke(this, EventArgs.Empty);
}

// Una clase sin constructor sin parámetros y un record
public sealed record Direccion(string Calle, int Numero);

public class ConDireccion
{
    public Direccion Direccion { get; set; } = new("", 0);
}

// Campos privados con el mismo nombre en base y derivada
public class Base { private int _valor = 1; public int ValorBase() => _valor; public void SetBase(int v) => _valor = v; }
public class Derivada : Base { private int _valor = 2; public int ValorDerivada() => _valor; public void SetDerivada(int v) => _valor = v; }

// Constructor que "siembra" datos: al cargar no deben duplicarse
public class ConSemilla
{
    public List<string> Usuarios { get; } = new() { "admin" };
}
