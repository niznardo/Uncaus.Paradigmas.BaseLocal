namespace EjemploTienda;

// Modelo de ejemplo. Fíjense que NO hay atributos ni nada especial para persistir:
// herencia, clases abstractas, interfaces y referencias en ambos sentidos funcionan tal cual.

public interface IVendible
{
    string Nombre { get; }
    decimal PrecioFinal();
}

public abstract class Producto : IVendible
{
    public string Nombre { get; private set; }
    public decimal Precio { get; protected set; }

    protected Producto(string nombre, decimal precio) { Nombre = nombre; Precio = precio; }

    public abstract decimal PrecioFinal();
    public override string ToString() => $"{Nombre} - ${PrecioFinal():0.00}";
}

public class ProductoNacional : Producto
{
    public ProductoNacional(string nombre, decimal precio) : base(nombre, precio) { }
    public override decimal PrecioFinal() => Precio * 1.21m;
}

public class ProductoImportado : Producto
{
    public decimal Arancel { get; }
    public ProductoImportado(string nombre, decimal precio, decimal arancel) : base(nombre, precio) { Arancel = arancel; }
    public override decimal PrecioFinal() => Precio * (1 + Arancel) * 1.21m;
}

public class Cliente
{
    public string Nombre { get; }
    public List<Pedido> Pedidos { get; } = new();           // Cliente -> Pedido
    public Cliente(string nombre) { Nombre = nombre; }
    public override string ToString() => $"{Nombre} ({Pedidos.Count} pedidos)";
}

public class Pedido
{
    public Cliente Cliente { get; }                          // Pedido -> Cliente (referencia circular)
    public DateTime Fecha { get; } = DateTime.Now;
    public List<IVendible> Items { get; } = new();           // lista de interfaz: se guarda la clase real de cada uno

    public Pedido(Cliente cliente) { Cliente = cliente; cliente.Pedidos.Add(this); }
    public decimal Total() => Items.Sum(i => i.PrecioFinal());
    public override string ToString() => $"{Fecha:g} - {Cliente.Nombre} - {Items.Count} items - ${Total():0.00}";
}

/// <summary>
/// Clase RAÍZ: contiene TODO lo que el programa necesita recordar.
/// Es lo único que se le pasa a Persistencia.Guardar / Cargar.
/// </summary>
public class Sistema
{
    public List<Cliente> Clientes { get; } = new();
    public List<Producto> Productos { get; } = new();
    public List<Pedido> Pedidos { get; } = new();
}
