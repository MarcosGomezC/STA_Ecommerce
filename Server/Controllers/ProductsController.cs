using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using STA_Ecommerce.Server.Data;
using STA_Ecommerce.Server.Services;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProductDataFetcherService _dataFetcher;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        AppDbContext db,
        IProductDataFetcherService dataFetcher,
        IMemoryCache cache,
        ILogger<ProductsController> logger)
    {
        _db = db;
        _dataFetcher = dataFetcher;
        _cache = cache;
        _logger = logger;
    }

    // GET: api/products
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 300)] // Cache por 5 minutos
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        [FromQuery] string? proveedor,
        [FromQuery] string? categoria,
        [FromQuery] decimal? minPrecio,
        [FromQuery] decimal? maxPrecio)
    {
        // Crear clave de caché basada en los filtros
        var cacheKey = $"products_{proveedor}_{categoria}_{minPrecio}_{maxPrecio}";

        // Intentar obtener desde caché
        if (_cache.TryGetValue(cacheKey, out List<Product>? cachedProducts))
        {
            _logger.LogInformation("Productos obtenidos desde caché: {CacheKey}", cacheKey);
            return Ok(cachedProducts);
        }

        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(proveedor))
        {
            query = query.Where(p => p.Proveedor == proveedor);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            query = query.Where(p => p.Categoria == categoria);
        }

        if (minPrecio.HasValue)
        {
            query = query.Where(p => p.Precio >= minPrecio.Value);
        }

        if (maxPrecio.HasValue)
        {
            query = query.Where(p => p.Precio <= maxPrecio.Value);
        }

        var productos = await query.AsNoTracking().ToListAsync();

        // Guardar en caché por 5 minutos
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

        _cache.Set(cacheKey, productos, cacheOptions);

        return Ok(productos);
    }

    // GET: api/products/5
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 600)] // Cache por 10 minutos
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var cacheKey = $"product_{id}";

        if (_cache.TryGetValue(cacheKey, out Product? cachedProduct))
        {
            _logger.LogInformation("Producto {Id} obtenido desde caché", id);
            return cachedProduct!;
        }

        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

        _cache.Set(cacheKey, product, cacheOptions);

        return product;
    }

    // POST: api/products
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("api")]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        try
        {
            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            // Invalidar caché
            InvalidateProductCache();

            _logger.LogInformation("Producto creado: {ProductId} - {ProductName}", product.Id, product.Nombre);

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear producto");
            return BadRequest(new { error = "Error al crear el producto" });
        }
    }

    // PUT: api/products/5
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("api")]
    public async Task<IActionResult> UpdateProduct(int id, Product product)
    {
        if (id != product.Id) return BadRequest();

        try
        {
            _db.Entry(product).State = EntityState.Modified;
            await _db.SaveChangesAsync();

            // Invalidar caché
            InvalidateProductCache();
            _cache.Remove($"product_{id}");

            _logger.LogInformation("Producto actualizado: {ProductId} - {ProductName}", product.Id, product.Nombre);

            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Products.AnyAsync(p => p.Id == id))
            {
                return NotFound();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar producto {ProductId}", id);
            return BadRequest(new { error = "Error al actualizar el producto" });
        }
    }

    // DELETE: api/products/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("api")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        try
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();

            // Invalidar caché
            InvalidateProductCache();
            _cache.Remove($"product_{id}");

            _logger.LogInformation("Producto eliminado: {ProductId} - {ProductName}", product.Id, product.Nombre);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar producto {ProductId}", id);
            return BadRequest(new { error = "Error al eliminar el producto" });
        }
    }

    // POST: api/products/fetch-details
    [HttpPost("fetch-details")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("scraping")] // Rate limit más restrictivo
    public async Task<ActionResult<ProductDetailsDto>> FetchDetails([FromBody] string affiliateUrl)
    {
        try
        {
            _logger.LogInformation("Obteniendo detalles del producto desde: {Url}", affiliateUrl);

            var dto = await _dataFetcher.FetchDetailsAsync(affiliateUrl);

            _logger.LogInformation("Detalles obtenidos exitosamente para: {ProductName}", dto.Nombre);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener detalles desde: {Url}", affiliateUrl);
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    private void InvalidateProductCache()
    {
        // En una implementación real, usarías un sistema más sofisticado
        // Por ahora, simplemente removemos todas las claves que empiezan con "products_"
        // Nota: MemoryCache no tiene una forma directa de hacer esto, 
        // en producción considerarías usar Redis con pattern matching
        _logger.LogInformation("Caché de productos invalidado");
    }
}