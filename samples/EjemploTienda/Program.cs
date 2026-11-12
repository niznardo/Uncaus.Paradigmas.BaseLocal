using BaseLocal;
using EjemploTienda;

// 1) Opcional: si cambian las clases, subir este número. Evita cargar datos de una estructura vieja.
Persistencia.VersionModelo = 1;

// 2) Opcional: guardar el archivo junto al programa para poder abrirlo y mirarlo fácilmente.
Persistencia.CarpetaDatos = AppContext.BaseDirectory;

// 3) Al iniciar: carga todo el sistema, o crea uno vacío si es la primera vez.
var sistema = Persistencia.CargarOCrear<Sistema>();
Console.WriteLine($"Archivo de datos: {Persistencia.ObtenerRuta()}");
Console.WriteLine($"Cargados: {sistema.Clientes.Count} clientes, {sistema.Productos.Count} productos, {sistema.Pedidos.Count} pedidos.");

while (true)
{
    Console.WriteLine("""

        1) Nuevo cliente      4) Listar clientes
        2) Nuevo producto     5) Listar pedidos
        3) Nuevo pedido       0) Salir
        """);
    Console.Write("Opción: ");

    switch (Console.ReadLine())
    {
        case "1":
            sistema.Clientes.Add(new Cliente(Pedir("Nombre")));
            break;

        case "2":
            var nombre = Pedir("Nombre");
            var precio = decimal.Parse(Pedir("Precio"));
            var importado = Pedir("¿Importado? (s/n)").ToLower() == "s";
            sistema.Productos.Add(importado
                ? new ProductoImportado(nombre, precio, 0.35m)
                : new ProductoNacional(nombre, precio));
            break;

        case "3":
            if (sistema.Clientes.Count == 0 || sistema.Productos.Count == 0)
            {
                Console.WriteLine("Primero cargá al menos un cliente y un producto.");
                break;
            }
            var cliente = Elegir("Cliente", sistema.Clientes);
            var pedido = new Pedido(cliente);
            do pedido.Items.Add(Elegir("Producto", sistema.Productos));
            while (Pedir("¿Agregar otro? (s/n)").ToLower() == "s");
            sistema.Pedidos.Add(pedido);
            break;

        case "4":
            sistema.Clientes.ForEach(c => Console.WriteLine(c));
            break;

        case "5":
            sistema.Pedidos.ForEach(p => Console.WriteLine(p));
            break;

        case "0":
            // 4) Guarda TODO de una vez. Las referencias entre objetos se mantienen.
            Persistencia.Guardar(sistema);
            Console.WriteLine("Datos guardados. ¡Hasta luego!");
            return;

        default:
            Console.WriteLine("Opción inválida.");
            continue;
    }

    // También se puede guardar después de cada cambio, para no perder nada si el programa se cierra mal.
    Persistencia.Guardar(sistema);
}

static string Pedir(string etiqueta)
{
    Console.Write($"{etiqueta}: ");
    return Console.ReadLine() ?? "";
}

static T Elegir<T>(string etiqueta, List<T> opciones)
{
    for (var i = 0; i < opciones.Count; i++) Console.WriteLine($"  {i + 1}) {opciones[i]}");
    return opciones[int.Parse(Pedir(etiqueta + " (número)")) - 1];
}
