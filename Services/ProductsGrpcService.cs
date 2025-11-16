using FluentValidation;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using SunShop.Grpc.Products.Data;
using SunShop.Grpc.Products.Models;
using SunShop.Grpc.Products.Protos;

namespace SunShop.Grpc.Products.Services;

public class ProductsGrpcService : global::SunShop.Grpc.Products.Protos.Products.ProductsBase
{
    private readonly ProductsDbContext _context;
    private readonly ILogger<ProductsGrpcService> _logger;
    private readonly IValidator<GetProductsRequest> _getProductsValidator;
    private readonly IValidator<GetProductRequest> _getProductValidator;
    private readonly IValidator<SearchProductsRequest> _searchValidator;
    private readonly IValidator<CreateProductRequest> _createProductValidator;
    private readonly IValidator<UpdateProductRequest> _updateProductValidator;
    private readonly IValidator<DeleteProductRequest> _deleteProductValidator;

    public ProductsGrpcService(ProductsDbContext context, ILogger<ProductsGrpcService> logger,
                            IValidator<GetProductRequest> getProductValidator,
                            IValidator<GetProductsRequest> getProductsValidator,
                            IValidator<SearchProductsRequest> searchValidator,
                            IValidator<CreateProductRequest> createProductValidator,
                            IValidator<UpdateProductRequest> updateProductValidator,
                            IValidator<DeleteProductRequest> deleteProductValidator)
    {
        _context = context;
        _logger = logger;
        _getProductValidator = getProductValidator;
        _getProductsValidator = getProductsValidator;
        _searchValidator = searchValidator;
        _createProductValidator = createProductValidator;
        _updateProductValidator = updateProductValidator;
        _deleteProductValidator = deleteProductValidator;
    }

    public override async Task GetProducts(GetProductsRequest request, IServerStreamWriter<ProductResponse> responseStream, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("GetProducts llamado con paginación: " + $"Página={request.PageNumber}, Tamaño={request.PageSize}");

            #region Validaciones

            var validationResult = await _getProductsValidator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));

                _logger.LogWarning($"Validación fallida: {errors}");

                throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
            }
            #endregion

            #region Filtros

            var query = _context.Products.AsQueryable();


            if (request.ActiveOnly)
            {
                query = query.Where(p => p.IsActive);
            }
            #endregion

            #region Ordenamiento            
            query = query.OrderBy(p => p.Name);
            #endregion

            #region Buscar Productos - Con Paginación
            // Calcular paginación
            var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
            var pageSize = request.PageSize > 0 ? request.PageSize : 10;
            var skip = (pageNumber - 1) * pageSize;

            // Obtener Productos con paginación
            var Products = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {Products.Count} Productos");
            #endregion

            #region Devolver Productos            
            foreach (var Product in Products)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("GetProducts cancelado por el cliente");
                    break;
                }

                var response = MapToProductResponse(Product);

                await responseStream.WriteAsync(response);
            }

            _logger.LogInformation($"GetProducts completado - {Products.Count} Productos enviados");
            #endregion
        }

        #region Manejo de Excepciones
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lista de Productos");

            throw new RpcException(new Status(StatusCode.Internal, "Error interno al procesar la solicitud"));
        }
        #endregion
    }

    public override async Task<ProductResponse> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation($"GetProduct llamado para ID: {request.Id}");

            #region Validaciones
            var validationResult = await _getProductValidator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));

                _logger.LogWarning($"Validación fallida: {errors}");

                throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
            }
            #endregion

            #region Buscar Producto
            var Product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.Id);

            if (Product == null)
            {
                _logger.LogWarning($"Product con ID {request.Id} no encontrado");

                throw new RpcException(new Status(StatusCode.NotFound, $"Product con ID {request.Id} no existe"));
            }

            _logger.LogInformation($"Product {Product.Id} encontrado exitosamente");
            #endregion

            #region Devolver Producto
            return MapToProductResponse(Product);
            #endregion
        }

        #region Manejo de Excepciones
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al obtener Producto con ID {request.Id}");

            throw new RpcException(new Status(StatusCode.Internal, "Error interno al procesar la solicitud"));
        }
        #endregion

    }

    /// <summary>
    /// Busca productos por término (Server Streaming)
    /// </summary>
    public override async Task SearchProducts(
        SearchProductsRequest request,
        IServerStreamWriter<ProductResponse> responseStream,
        ServerCallContext context)
    {
        try
        {
            _logger.LogInformation($"SearchProducts llamado con término: '{request.SearchTerm}'");

            // Validar solicitud
            var validationResult = await _searchValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning($"Validación fallida: {errors}");
                throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
            }

            // Construir query de búsqueda
            var searchTerm = request.SearchTerm.ToLower();
            var query = _context.Products
                .Where(p => p.IsActive &&
                    (p.Name.ToLower().Contains(searchTerm) ||
                     p.Description.ToLower().Contains(searchTerm) ||
                     p.Category.ToLower().Contains(searchTerm)));

            // Ordenar por relevancia (nombre primero)
            query = query.OrderBy(p => p.Name.ToLower().Contains(searchTerm) ? 0 : 1)
                .ThenBy(p => p.Name);

            // Aplicar paginación
            var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
            var pageSize = request.PageSize > 0 ? request.PageSize : 10;
            var skip = (pageNumber - 1) * pageSize;

            var products = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation($"Búsqueda encontró {products.Count} productos");

            // Enviar resultados en streaming
            foreach (var product in products)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("SearchProducts cancelado por el cliente");
                    break;
                }

                var response = MapToProductResponse(product);
                await responseStream.WriteAsync(response);
            }

            _logger.LogInformation($"SearchProducts completado - {products.Count} resultados enviados");
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar productos");
            throw new RpcException(new Status(StatusCode.Internal,
                "Error interno al procesar la solicitud"));
        }
    }

    public override async Task<ProductResponse> CreateProduct(CreateProductRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation($"CreateProduct llamado para: {request.Name}");

            #region Validaciones
            var validationResult = await _createProductValidator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));

                _logger.LogWarning($"Validación fallida: {errors}");

                throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
            }

            // Verificar si ya existe un Producto con el mismo nombre
            var existingProductByName = await _context.Products.FirstOrDefaultAsync(p => p.Name == request.Name);

            if (existingProductByName != null)
            {
                _logger.LogWarning($"Producto con Nombre '{request.Name}' ya existe");

                throw new RpcException(new Status(StatusCode.AlreadyExists, $"Ya existe un Producto con el Nombre '{request.Name}'"));
            }
            #endregion

            #region Crea Producto
            var Product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = Convert.ToDecimal(request.Price),
                Stock = request.Stock,
                Category = request.Category,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.Products.Add(Product);

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Producto creado exitosamente con ID: {Product.Id}");
            #endregion

            #region Devolver Producto Creado
            return MapToProductResponse(Product);
            #endregion
        }

        #region Manejo de Excepciones
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear Producto");

            throw new RpcException(new Status(StatusCode.Internal, "Error interno al procesar la solicitud"));
        }
        #endregion
    }

    public override async Task<ProductResponse> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation($"UpdateProduct llamado para ID: {request.Id}");

            #region Validaciones
            var validationResult = await _updateProductValidator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));

                _logger.LogWarning($"Validación fallida: {errors}");

                throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
            }

            var Product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.Id);

            if (Product == null)
            {
                _logger.LogWarning($"Product con ID {request.Id} no encontrado");

                throw new RpcException(new Status(StatusCode.NotFound, $"Producto con ID {request.Id} no existe"));
            }

            // Verificar si el nuevo nombre ya existe en otro Producto
            if (Product.Name != request.Name)
            {
                var existingProductByName = await _context.Products.FirstOrDefaultAsync(p => p.Name == request.Name && p.Id != request.Id);

                if (existingProductByName != null)
                {
                    _logger.LogWarning($"Producto con Nombre '{request.Name}' ya existe");

                    throw new RpcException(new Status(StatusCode.AlreadyExists, $"Ya existe otro Producto con el Nombre '{request.Name}'"));
                }
            }
            #endregion

            #region Actualiza Producto
            Product.Name = request.Name;
            Product.Description = request.Description;
            Product.Price = Convert.ToDecimal(request.Price);
            Product.Stock = request.Stock;
            Product.Category = request.Category;
            Product.IsActive = request.IsActive;
            Product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Update(Product);

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Producto {Product.Id} actualizado exitosamente");
            #endregion

            #region Devolver Producto Actualizado
            return MapToProductResponse(Product);
            #endregion
        }

        #region Manejo de Excepciones
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al actualizar Producto con ID {request.Id}");

            throw new RpcException(new Status(StatusCode.Internal, "Error interno al procesar la solicitud"));
        }
        #endregion
    }

    public override async Task<DeleteProductResponse> DeleteProduct(DeleteProductRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation($"DeleteProduct llamado para ID: {request.Id}");

            #region Validaciones
            var validationResult = await _deleteProductValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));

                _logger.LogWarning($"Validación fallida: {errors}");

                throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
            }
            #endregion

            #region Busca Producto
            var Product = await _context.Products.FirstOrDefaultAsync(u => u.Id == request.Id);
            if (Product == null)
            {
                _logger.LogWarning($"Producto con ID {request.Id} no encontrado");

                throw new RpcException(new Status(StatusCode.NotFound, $"Producto con ID {request.Id} no existe"));
            }
            #endregion

            #region Elimina Producto Logicamente

            Product.IsActive = false;

            _context.Products.Update(Product);

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Producto {Product.Id} eliminado exitosamente (lógico)");
            #endregion

            #region Devolver Mensaje De Eliminación
            return new DeleteProductResponse
            {
                Success = true,
                Message = $"Producto con ID {request.Id} eliminado exitosamente"
            };
            #endregion
        }

        #region Manejo de Excepciones
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al eliminar Producto con ID {request.Id}");

            throw new RpcException(new Status(StatusCode.Internal, "Error interno al procesar la solicitud"));
        }
        #endregion
    }

    private ProductResponse MapToProductResponse(Product Product)
    {
        return new ProductResponse
        {
            Id = Product.Id,
            Name = Product.Name,
            Description = Product.Description,
            Price = Convert.ToDouble(Product.Price),
            Stock = Product.Stock,
            Category = Product.Category,
            CreatedAt = Product.CreatedAt.ToString("o"),
            UpdatedAt = Product.UpdatedAt?.ToString("o") ?? string.Empty,
            IsActive = Product.IsActive
        };
    }
}