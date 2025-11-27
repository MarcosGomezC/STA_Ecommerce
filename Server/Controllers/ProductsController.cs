using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public ProductsController(AppDbContext db, IProductDataFetcherService dataFetcher)
    {
        _db = db;
        _dataFetcher = dataFetcher;
    }

    // GET: api/products
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        [FromQuery] string? proveedor,
        [FromQuery] string? categoria,
        [FromQuery] decimal? minPrecio,
        [FromQuery] decimal? maxPrecio)
    {
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
        return Ok(productos);
    }

    // GET: api/products/5
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        return product;
    }

    // POST: api/products
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    // PUT: api/products/5
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateProduct(int id, Product product)
    {
        if (id != product.Id) return BadRequest();

        _db.Entry(product).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/products/5
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/products/fetch-details
    [HttpPost("fetch-details")]
    [Authorize]
    public async Task<ActionResult<ProductDetailsDto>> FetchDetails([FromBody] string affiliateUrl)
    {
        try
        {
            var dto = await _dataFetcher.FetchDetailsAsync(affiliateUrl);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }
}


