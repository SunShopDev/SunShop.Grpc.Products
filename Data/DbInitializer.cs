using Microsoft.EntityFrameworkCore;
using SunShop.Grpc.Products.Models;
using BC = BCrypt.Net.BCrypt;

namespace SunShop.Grpc.Products.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(ProductsDbContext context, ILogger logger)
    {
        try
        {
            // Asegurar que la base de datos esté creada
            logger.LogInformation("Verificando existencia de base de datos...");
            await context.Database.EnsureCreatedAsync();

            // Aplicar migraciones pendientes
            if (context.Database.GetPendingMigrations().Any())
            {
                logger.LogInformation("Aplicando migraciones pendientes...");
                await context.Database.MigrateAsync();
            }

            // Verificar si ya existen Productos
            if (await context.Products.AnyAsync())
            {
                logger.LogInformation("Base de datos ya contiene Productos. Omitiendo inicialización.");
                return;
            }

            logger.LogInformation("Inicializando base de datos con datos de prueba...");


            var ProductsForInicializer = new List<Product>
            {
                 new()
                    {
                        Name = "Laptop Dell XPS 15",
                        Description = "Laptop de alto rendimiento con procesador Intel Core i7, 16GB RAM, SSD 512GB",
                        Price = 1299.99m,
                        Stock = 15,
                        Category = "Electrónica",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new()
                    {
                        Name = "Mouse Logitech MX Master 3",
                        Description = "Mouse inalámbrico ergonómico con precisión de 4000 DPI",
                        Price = 99.99m,
                        Stock = 50,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Teclado Mecánico Corsair K95",
                        Description = "Teclado mecánico RGB con switches Cherry MX",
                        Price = 189.99m,
                        Stock = 30,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Monitor Samsung 27\" 4K",
                        Description = "Monitor 4K UHD de 27 pulgadas con tecnología HDR",
                        Price = 449.99m,
                        Stock = 20,
                        Category = "Electrónica",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Silla Ergonómica Herman Miller",
                        Description = "Silla de oficina ergonómica con soporte lumbar ajustable",
                        Price = 799.99m,
                        Stock = 10,
                        Category = "Muebles",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Webcam Logitech C920",
                        Description = "Webcam Full HD 1080p con micrófono estéreo",
                        Price = 79.99m,
                        Stock = 40,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Auriculares Sony WH-1000XM4",
                        Description = "Auriculares inalámbricos con cancelación de ruido activa",
                        Price = 349.99m,
                        Stock = 25,
                        Category = "Audio",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Escritorio Ajustable en Altura",
                        Description = "Escritorio de pie eléctrico con altura ajustable de 60cm a 120cm",
                        Price = 599.99m,
                        Stock = 8,
                        Category = "Muebles",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Hub USB-C 7 en 1",
                        Description = "Adaptador multipuerto USB-C con HDMI, USB 3.0 y lector SD",
                        Price = 49.99m,
                        Stock = 60,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Lámpara de Escritorio LED",
                        Description = "Lámpara LED regulable con carga inalámbrica Qi integrada",
                        Price = 69.99m,
                        Stock = 35,
                        Category = "Iluminación",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "SSD Samsung 970 EVO 1TB",
                        Description = "Disco sólido NVMe M.2 de alta velocidad con 1TB de capacidad",
                        Price = 129.99m,
                        Stock = 45,
                        Category = "Almacenamiento",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                     new()
                    {
                        Name = "Router Wi-Fi 6 ASUS",
                        Description = "Router Wi-Fi 6 de doble banda con cobertura de 3000 pies cuadrados",
                        Price = 179.99m,
                        Stock = 18,
                        Category = "Redes",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    }
            };

            // Agregar Productos a la base de datos
            await context.Products.AddRangeAsync(ProductsForInicializer);
            await context.SaveChangesAsync();

            logger.LogInformation($"Base de datos inicializada exitosamente con {ProductsForInicializer.Count} Productos.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al inicializar la base de datos.");
            throw;
        }
    }
}
