using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // Persistencia tiene estado estático

namespace BaseLocal.Tests;

public sealed class PersistenciaTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "BaseLocalTests_" + Guid.NewGuid().ToString("N"));
    private string Ruta => Path.Combine(_carpeta, "datos.json");

    public PersistenciaTests()
    {
        Persistencia.VersionModelo = 1;
        Persistencia.Advertencia = null;
    }

    public void Dispose()
    {
        Persistencia.VersionModelo = 1;
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    private static Sistema ArmarSistema()
    {
        var s = new Sistema();
        var ana = new Cliente("Ana") { Alta = new DateTime(2024, 3, 5, 10, 30, 0, DateTimeKind.Utc) };
        var luis = new Cliente("Luis");
        var tv = new ProductoFisico("TV", 1999.99m, 12.5) { Categoria = Categoria.Tecnologia };
        var ebook = new ProductoDigital("Ebook", 9.5m) { UrlDescarga = "http://x/y", Categoria = Categoria.Libros };

        s.Clientes.AddRange(new[] { ana, luis });
        s.Productos.AddRange(new Producto[] { tv, ebook });

        var pedido = new Pedido(ana) { Fecha = new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.FromHours(-3)) };
        pedido.Items.Add(tv);
        pedido.Items.Add(ebook);
        pedido.Items.Add(tv); // el mismo objeto dos veces
        s.Pedidos.Add(pedido);

        s.Stock["TV"] = 4;
        s.Recargos[Categoria.Tecnologia] = 0.21m;
        s.Promocion = tv;
        s.Ubicacion = new Punto { X = 3, Y = 7 };
        s.Opcional = 42;
        s.Temporal = "cambiado";
        return s;
    }

    [Fact]
    public void RoundTrip_GrafoCompleto()
    {
        Persistencia.Guardar(ArmarSistema(), Ruta);
        var s = Persistencia.Cargar<Sistema>(Ruta)!;

        Assert.Equal(2, s.Clientes.Count);
        Assert.Equal("Ana", s.Clientes[0].Nombre);                         // propiedad sólo lectura
        Assert.Equal(new DateTime(2024, 3, 5, 10, 30, 0, DateTimeKind.Utc), s.Clientes[0].Alta);
        Assert.Equal(DateTimeKind.Utc, s.Clientes[0].Alta.Kind);

        // polimorfismo
        var tv = Assert.IsType<ProductoFisico>(s.Productos[0]);
        var ebook = Assert.IsType<ProductoDigital>(s.Productos[1]);
        Assert.Equal("TV", tv.Nombre);                                     // setter privado
        Assert.Equal(1999.99m, tv.Precio);                                 // setter protegido, decimal exacto
        Assert.Equal(12.5, tv.PesoKg);
        Assert.Equal(Categoria.Tecnologia, tv.Categoria);
        Assert.Equal("http://x/y", ebook.UrlDescarga);
        Assert.Equal("TV (12,5 kg)".Replace(',', System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]), tv.Describir());

        // referencias circulares e identidad
        var pedido = s.Pedidos[0];
        Assert.Same(s.Clientes[0], pedido.Cliente);
        Assert.Same(pedido, s.Clientes[0].Pedidos[0]);
        Assert.Same(tv, pedido.Items[0]);
        Assert.Same(tv, pedido.Items[2]);
        Assert.Same(tv, s.Promocion);
        Assert.Equal(new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.FromHours(-3)), pedido.Fecha);
        Assert.Equal(TimeSpan.FromHours(-3), pedido.Fecha.Offset);

        // colecciones, structs, nullables
        Assert.Equal(4, s.Stock["TV"]);
        Assert.Equal(0.21m, s.Recargos[Categoria.Tecnologia]);
        Assert.Equal(3, s.Ubicacion.X);
        Assert.Equal(7, s.Ubicacion.Y);
        Assert.Equal(42, s.Opcional);
    }

    [Fact]
    public void NoGuarda_Eventos_NonSerialized_NiPropiedadesCalculadas()
    {
        var s = ArmarSistema();
        s.Cambio += (_, _) => { };
        Persistencia.Guardar(s, Ruta);

        var json = File.ReadAllText(Ruta);
        Assert.DoesNotContain("no guardar", json);
        Assert.DoesNotContain("cambiado", json);
        Assert.DoesNotContain("Cambio", json);
        Assert.DoesNotContain("Descuento", json);

        var cargado = Persistencia.Cargar<Sistema>(Ruta)!;
        Assert.Equal("no guardar", cargado.Temporal); // vuelve el valor inicial del constructor
        cargado.Notificar();                          // sin manejadores: no falla
    }

    [Fact]
    public void ClaseSinConstructorVacio_Y_Record()
    {
        Persistencia.Guardar(new ConDireccion { Direccion = new Direccion("Mitre", 123) }, Ruta);
        var c = Persistencia.Cargar<ConDireccion>(Ruta)!;
        Assert.Equal(new Direccion("Mitre", 123), c.Direccion);
    }

    [Fact]
    public void CamposPrivadosConMismoNombre_EnBaseYDerivada()
    {
        var d = new Derivada();
        d.SetBase(10);
        d.SetDerivada(20);
        Persistencia.Guardar(d, Ruta);
        var r = Persistencia.Cargar<Derivada>(Ruta)!;
        Assert.Equal(10, r.ValorBase());
        Assert.Equal(20, r.ValorDerivada());
    }

    [Fact]
    public void ValoresIniciales_DelConstructor_NoSeDuplican()
    {
        var o = new ConSemilla();
        o.Usuarios.Add("ana");
        Persistencia.Guardar(o, Ruta);
        var r = Persistencia.Cargar<ConSemilla>(Ruta)!;
        Assert.Equal(new[] { "admin", "ana" }, r.Usuarios);
    }

    [Fact]
    public void ArchivoInexistente_DevuelveNull_Y_CargarOCrear_CreaNuevo()
    {
        Assert.False(Persistencia.Existe(Ruta));
        Assert.Null(Persistencia.Cargar<Sistema>(Ruta));
        var nuevo = Persistencia.CargarOCrear<Sistema>(Ruta);
        Assert.Empty(nuevo.Clientes);
    }

    [Fact]
    public void GuardarDosVeces_DejaRespaldo_Y_NoDejaTemporal()
    {
        var s = new Sistema();
        s.Clientes.Add(new Cliente("Uno"));
        Persistencia.Guardar(s, Ruta);
        s.Clientes.Add(new Cliente("Dos"));
        Persistencia.Guardar(s, Ruta);

        Assert.True(File.Exists(Ruta + ".bak"));
        Assert.False(File.Exists(Ruta + ".tmp"));
        Assert.Equal(2, Persistencia.Cargar<Sistema>(Ruta)!.Clientes.Count);
    }

    [Fact]
    public void ArchivoDanado_RecuperaDelRespaldo_Y_Avisa()
    {
        var s = new Sistema();
        s.Clientes.Add(new Cliente("Uno"));
        Persistencia.Guardar(s, Ruta);
        s.Clientes.Add(new Cliente("Dos"));
        Persistencia.Guardar(s, Ruta);

        File.WriteAllText(Ruta, File.ReadAllText(Ruta)[..40]); // archivo truncado

        string? aviso = null;
        Persistencia.Advertencia = m => aviso = m;
        var r = Persistencia.Cargar<Sistema>(Ruta)!;

        Assert.Single(r.Clientes); // el respaldo tenía la versión anterior
        Assert.NotNull(aviso);
    }

    [Fact]
    public void ArchivoDanado_SinRespaldo_LanzaArchivoCorrupto()
    {
        Persistencia.Guardar(new Sistema(), Ruta);
        File.Delete(Ruta + ".bak");
        File.WriteAllText(Ruta, "{ esto no es json");
        Assert.Throws<ArchivoCorruptoException>(() => Persistencia.Cargar<Sistema>(Ruta));
    }

    [Fact]
    public void ArchivoTruncado_LanzaArchivoCorrupto()
    {
        Persistencia.Guardar(ArmarSistema(), Ruta);
        File.Delete(Ruta + ".bak");
        File.WriteAllText(Ruta, File.ReadAllText(Ruta)[..200]);
        Assert.Throws<ArchivoCorruptoException>(() => Persistencia.Cargar<Sistema>(Ruta));
    }

    [Fact]
    public void VersionDistinta_LanzaVersionIncompatible()
    {
        Persistencia.VersionModelo = 1;
        Persistencia.Guardar(new Sistema(), Ruta);

        Persistencia.VersionModelo = 2;
        var ex = Assert.Throws<VersionIncompatibleException>(() => Persistencia.Cargar<Sistema>(Ruta));
        Assert.Equal(1, ex.VersionArchivo);
        Assert.Equal(2, ex.VersionEsperada);
    }

    [Fact]
    public void Guardar_ConUnTipoNoSerializable_LanzaPersistenciaException_YNoTocaElArchivo()
    {
        Persistencia.Guardar(new Sistema(), Ruta);
        var antes = File.ReadAllText(Ruta);

        var malo = new ConStream();
        Assert.Throws<PersistenciaException>(() => Persistencia.Guardar(malo, Ruta));
        Assert.Equal(antes, File.ReadAllText(Ruta));
    }

    [Fact]
    public void TipoNoPermitido_EnElArchivo_SeRechaza()
    {
        Persistencia.Guardar(new Sistema(), Ruta);
        var json = File.ReadAllText(Ruta).Replace("\"Promocion\": null",
            "\"Promocion\": { \"$type\": \"System.Diagnostics.Process, System.Diagnostics.Process\" }");
        Assert.Contains("System.Diagnostics.Process", json);
        File.WriteAllText(Ruta, json);
        File.Delete(Ruta + ".bak");
        Assert.Throws<PersistenciaException>(() => Persistencia.Cargar<Sistema>(Ruta));
    }

    [Fact]
    public void NombreSimple_SeGuardaEnCarpetaDatos()
    {
        var anterior = Persistencia.CarpetaDatos;
        try
        {
            Persistencia.CarpetaDatos = _carpeta;
            Persistencia.Guardar(new Sistema(), "otro.json");
            Assert.True(File.Exists(Path.Combine(_carpeta, "otro.json")));
            Assert.True(Persistencia.Existe("otro.json"));
            Persistencia.Eliminar("otro.json");
            Assert.False(Persistencia.Existe("otro.json"));
        }
        finally { Persistencia.CarpetaDatos = anterior; }
    }

    [Fact]
    public void JsonResultante_EsLegible()
    {
        Persistencia.Guardar(ArmarSistema(), Ruta);
        var json = File.ReadAllText(Ruta);
        Assert.Contains("\"versionModelo\": 1", json);
        Assert.Contains("\"Nombre\": \"Ana\"", json);   // sin "<Nombre>k__BackingField"
        Assert.DoesNotContain("k__BackingField", json);
        Assert.Contains("\"Categoria\": \"Tecnologia\"", json); // enums como texto
    }

    private sealed class ConStream { public Stream Datos = new MemoryStream(); public Thread Hilo = new(() => { }); }
}
