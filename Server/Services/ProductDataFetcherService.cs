using System.Text.RegularExpressions;
using System.Text;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Server.Services;

public interface IProductDataFetcherService
{
    Task<ProductDetailsDto> FetchDetailsAsync(string affiliateUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Servicio mejorado que intenta obtener datos reales del producto desde la URL.
/// Extrae precio, imagen y nombre usando metadatos Open Graph, JSON-LD y parsing HTML.
/// </summary>
public class ProductDataFetcherService : IProductDataFetcherService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProductDataFetcherService> _logger;

    public ProductDataFetcherService(IHttpClientFactory httpClientFactory, ILogger<ProductDataFetcherService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProductDetailsDto> FetchDetailsAsync(string affiliateUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(affiliateUrl))
        {
            throw new ArgumentException("La URL de afiliado es obligatoria.", nameof(affiliateUrl));
        }

        var proveedor = InferProvider(affiliateUrl);
        var dto = new ProductDetailsDto
        {
            Proveedor = proveedor,
            Nombre = "Producto",
            Descripcion = null,
            Precio = 0m,
            URLImagen = string.Empty
        };

        try
        {
            // Intentar obtener datos reales desde la URL
            var html = await FetchHtmlAsync(affiliateUrl, cancellationToken);
            
            if (!string.IsNullOrEmpty(html))
            {
                _logger.LogInformation("HTML obtenido, longitud: {Length} caracteres", html.Length);
                
                // Intentar extraer datos usando diferentes métodos (en orden de prioridad)
                ExtractJsonLdData(html, dto); // Primero JSON-LD (más confiable)
                ExtractOpenGraphData(html, dto);
                ExtractPriceFromHtml(html, dto, proveedor); // Búsqueda más agresiva
                ExtractImageFromHtml(html, dto);
                ExtractTitleFromHtml(html, dto);
                
                _logger.LogInformation("Datos extraídos - Precio: {Price}, Nombre: {Name}, Imagen: {Image}", 
                    dto.Precio, dto.Nombre, !string.IsNullOrEmpty(dto.URLImagen));
            }
            else
            {
                _logger.LogWarning("No se pudo obtener HTML desde {Url}", affiliateUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener datos del producto desde {Url}", affiliateUrl);
            // Continuar con valores por defecto
        }

        // Si no se obtuvo nombre, usar uno genérico
        if (string.IsNullOrWhiteSpace(dto.Nombre) || dto.Nombre == "Producto")
        {
            dto.Nombre = $"Producto de {proveedor}";
        }

        // Si no se obtuvo precio, usar 0 para que el admin lo ingrese manualmente
        if (dto.Precio == 0m)
        {
            dto.Precio = 0m; // El admin deberá ingresarlo manualmente
        }

        // Si no se obtuvo imagen, usar placeholder
        if (string.IsNullOrWhiteSpace(dto.URLImagen))
        {
            dto.URLImagen = "https://via.placeholder.com/400x400?text=Imagen+No+Disponible";
        }

        return dto;
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            
            _logger.LogInformation("Obteniendo HTML desde: {Url}", url);
            var response = await httpClient.GetAsync(url, cancellationToken);
            
            _logger.LogInformation("Respuesta HTTP: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("HTML obtenido: {Length} caracteres", content.Length);
                
                // Guardar un fragmento del HTML para debug (primeros 500 caracteres)
                if (content.Length > 500)
                {
                    _logger.LogDebug("Fragmento HTML: {Fragment}", content.Substring(0, 500));
                }
                
                return content;
            }
            else
            {
                _logger.LogWarning("Error HTTP {StatusCode} al obtener {Url}", response.StatusCode, url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener HTML desde {Url}", url);
        }
        return null;
    }

    private void ExtractOpenGraphData(string html, ProductDetailsDto dto)
    {
        // Extraer Open Graph meta tags
        var ogTitleMatch = Regex.Match(html, @"<meta\s+property=[""']og:title[""']\s+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (ogTitleMatch.Success && string.IsNullOrWhiteSpace(dto.Nombre))
        {
            dto.Nombre = HtmlDecode(ogTitleMatch.Groups[1].Value);
        }

        var ogImageMatch = Regex.Match(html, @"<meta\s+property=[""']og:image[""']\s+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (ogImageMatch.Success && string.IsNullOrWhiteSpace(dto.URLImagen))
        {
            dto.URLImagen = ogImageMatch.Groups[1].Value;
        }

        var ogDescriptionMatch = Regex.Match(html, @"<meta\s+property=[""']og:description[""']\s+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (ogDescriptionMatch.Success && string.IsNullOrWhiteSpace(dto.Descripcion))
        {
            dto.Descripcion = HtmlDecode(ogDescriptionMatch.Groups[1].Value);
        }
    }

    private void ExtractJsonLdData(string html, ProductDetailsDto dto)
    {
        // Buscar JSON-LD con datos del producto
        var jsonLdMatches = Regex.Matches(html, @"<script\s+type=[""']application/ld\+json[""']>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        foreach (Match match in jsonLdMatches)
        {
            var jsonContent = match.Groups[1].Value;
            
            // Buscar nombre
            if (string.IsNullOrWhiteSpace(dto.Nombre))
            {
                var nameMatch = Regex.Match(jsonContent, @"""name""\s*:\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (nameMatch.Success)
                {
                    dto.Nombre = HtmlDecode(nameMatch.Groups[1].Value);
                }
            }

            // Buscar precio en JSON-LD (múltiples formatos)
            if (dto.Precio == 0m)
            {
                var pricePatterns = new[]
                {
                    @"""price""\s*:\s*[""']?(\d+\.?\d{0,2})[""']?",
                    @"""priceCurrency""\s*:\s*[""']?[^""']*[""']?\s*,\s*""price""\s*:\s*[""']?(\d+\.?\d{0,2})[""']?",
                    @"""lowPrice""\s*:\s*[""']?(\d+\.?\d{0,2})[""']?",
                    @"""highPrice""\s*:\s*[""']?(\d+\.?\d{0,2})[""']?",
                    @"""offers""\s*:\s*\{[^}]*""price""\s*:\s*[""']?(\d+\.?\d{0,2})[""']?",
                };

                foreach (var pattern in pricePatterns)
                {
                    var priceMatch = Regex.Match(jsonContent, pattern, RegexOptions.IgnoreCase);
                    if (priceMatch.Success)
                    {
                        var priceStr = priceMatch.Groups[1].Value.Trim().Replace(",", "");
                        if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
                        {
                            dto.Precio = price;
                            _logger.LogInformation("Precio extraído de JSON-LD: {Price}", dto.Precio);
                            break;
                        }
                    }
                }
            }

            // Buscar imagen en JSON-LD (múltiples formatos)
            if (string.IsNullOrWhiteSpace(dto.URLImagen))
            {
                var imagePatterns = new[]
                {
                    @"""image""\s*:\s*[""']([^""']+)[""']", // "image": "url"
                    @"""image""\s*:\s*\[[""']([^""']+)[""']", // "image": ["url"]
                    @"""image""\s*:\s*\{[^}]*""url""\s*:\s*[""']([^""']+)[""']", // "image": {"url": "..."}
                    @"""image""\s*:\s*\[[^\]]*\{[^}]*""url""\s*:\s*[""']([^""']+)[""']", // "image": [{"url": "..."}]
                };
                
                foreach (var pattern in imagePatterns)
                {
                    var imageMatch = Regex.Match(jsonContent, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (imageMatch.Success)
                    {
                        var imageUrl = imageMatch.Groups[1].Value.Trim();
                        // Limpiar la URL
                        imageUrl = imageUrl.Replace("\\/", "/").Replace("\\\"", "");
                        if (!string.IsNullOrWhiteSpace(imageUrl) && 
                            (imageUrl.Contains("http") || imageUrl.Contains(".jpg") || imageUrl.Contains(".png")))
                        {
                            dto.URLImagen = imageUrl;
                            _logger.LogInformation("Imagen encontrada en JSON-LD: {Image}", dto.URLImagen);
                            break;
                        }
                    }
                }
            }
        }
    }

    private void ExtractPriceFromHtml(string html, ProductDetailsDto dto, string proveedor)
    {
        if (dto.Precio != 0m) 
        {
            _logger.LogInformation("Precio ya obtenido: {Price}", dto.Precio);
            return; // Ya se obtuvo el precio
        }

        _logger.LogInformation("Buscando precio en HTML para proveedor: {Proveedor}", proveedor);

        // Patrones más robustos y específicos por proveedor
        var pricePatterns = new List<string>();

        // Patrones generales que funcionan para todos (más agresivos)
        pricePatterns.AddRange(new[]
        {
            // JSON embebido con precio (múltiples variantes)
            @"""price""\s*:\s*[""']?\$?(\d+\.?\d{0,2})",
            @"""priceCurrency""\s*:\s*[""']?[^""']*[""']?\s*,\s*""price""\s*:\s*[""']?(\d+\.?\d{0,2})",
            @"""lowPrice""\s*:\s*[""']?(\d+\.?\d{0,2})",
            @"""highPrice""\s*:\s*[""']?(\d+\.?\d{0,2})",
            @"""price""\s*:\s*(\d+\.?\d{0,2})\s*,",
            // Data attributes (más variantes)
            @"data-price=[""'](\d+\.?\d{0,2})[""']",
            @"data-product-price=[""'](\d+\.?\d{0,2})[""']",
            @"data-current-price=[""'](\d+\.?\d{0,2})[""']",
            @"data-sale-price=[""'](\d+\.?\d{0,2})[""']",
            // Meta tags
            @"<meta[^>]*property=[""']product:price:amount[""'][^>]*content=[""'](\d+\.?\d{0,2})[""']",
            @"<meta[^>]*name=[""']price[""'][^>]*content=[""'](\d+\.?\d{0,2})[""']",
        });

        // Patrones específicos por proveedor (más agresivos)
        if (proveedor.Contains("Amazon", StringComparison.OrdinalIgnoreCase))
        {
            pricePatterns.AddRange(new[]
            {
                // Amazon específico - múltiples formatos
                @"<span[^>]*id=[""']priceblock[^""']*[""'][^>]*>.*?\$(\d+\.?\d{0,2})",
                @"<span[^>]*id=[""']priceblock[^""']*[""'][^>]*>.*?(\d+\.?\d{0,2})",
                @"<span[^>]*class=[""'][^""']*a-price[^""']*[""'][^>]*>.*?\$(\d+\.?\d{0,2})",
                @"<span[^>]*class=[""'][^""']*a-price[^""']*[""'][^>]*>.*?(\d+\.?\d{0,2})",
                @"<span[^>]*class=[""'][^""']*a-offscreen[^""']*[""'][^>]*>\$(\d+\.?\d{0,2})",
                @"<span[^>]*class=[""'][^""']*a-offscreen[^""']*[""'][^>]*>(\d+\.?\d{0,2})",
                @"""price""\s*:\s*[""']?\$?(\d+\.?\d{0,2})",
                @"""priceCurrency""\s*:\s*[""']?USD[""']?\s*,\s*""price""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"priceToPay[^>]*>.*?\$(\d+\.?\d{0,2})",
                @"priceToPay[^>]*>.*?(\d+\.?\d{0,2})",
                // Amazon JSON embebido
                @"""displayAmount""\s*:\s*[""']?\$(\d+\.?\d{0,2})",
                @"""amount""\s*:\s*(\d+\.?\d{0,2})\s*,\s*""currency""",
            });
        }
        else if (proveedor.Contains("Shein", StringComparison.OrdinalIgnoreCase))
        {
            pricePatterns.AddRange(new[]
            {
                // Shein específico
                @"""normalPrice""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"""salePrice""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"""price""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"<span[^>]*class=[""'][^""']*price[^""']*[""'][^>]*>.*?(\d+\.?\d{0,2})",
                @"data-price=[""'](\d+\.?\d{0,2})[""']",
            });
        }
        else if (proveedor.Contains("Temu", StringComparison.OrdinalIgnoreCase))
        {
            pricePatterns.AddRange(new[]
            {
                // Temu específico - más patrones
                @"""currentPrice""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"""originalPrice""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"""price""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"""salePrice""\s*:\s*[""']?(\d+\.?\d{0,2})",
                @"<span[^>]*class=[""'][^""']*price[^""']*[""'][^>]*>.*?\$?(\d+\.?\d{0,2})",
                @"<span[^>]*class=[""'][^""']*current-price[^""']*[""'][^>]*>.*?\$?(\d+\.?\d{0,2})",
                @"data-price=[""'](\d+\.?\d{0,2})[""']",
                @"data-current-price=[""'](\d+\.?\d{0,2})[""']",
                // Temu JSON
                @"priceInfo[^}]*currentPrice[""']?\s*:\s*[""']?(\d+\.?\d{0,2})",
            });
        }

        // Buscar precios en formato $XX.XX o XX.XX
        pricePatterns.AddRange(new[]
        {
            @"\$\s*(\d{1,6}\.?\d{0,2})\b", // $19.99 o $19
            @"(\d{1,6}\.\d{2})\s*USD", // 19.99 USD
            @"(\d{1,6}\.\d{2})\s*\$", // 19.99 $
        });

        // Intentar cada patrón
        var allFoundPrices = new List<decimal>();
        
        foreach (var pattern in pricePatterns)
        {
            try
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                _logger.LogDebug("Patrón '{Pattern}' encontró {Count} coincidencias", pattern, matches.Count);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var priceStr = match.Groups[1].Value.Trim();
                        // Limpiar la cadena de precio
                        priceStr = priceStr.Replace(",", "").Replace("'", ""); // Remover comas y comillas
                        
                        if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0 && price < 1000000)
                        {
                            allFoundPrices.Add(price);
                            _logger.LogDebug("Precio encontrado: {Price} del patrón: {Pattern}", price, pattern);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al procesar patrón de precio: {Pattern}", pattern);
            }
        }
        
        // Si encontramos precios, usar el más común o el primero razonable
        if (allFoundPrices.Any())
        {
            _logger.LogInformation("Se encontraron {Count} precios posibles", allFoundPrices.Count);
            
            // Filtrar precios muy pequeños (probablemente errores) y muy grandes
            var validPrices = allFoundPrices.Where(p => p >= 0.01m && p <= 100000m).ToList();
            if (validPrices.Any())
            {
                // Usar el precio más frecuente (redondeado a 2 decimales)
                var groupedPrices = validPrices
                    .Select(p => Math.Round(p, 2))
                    .GroupBy(p => p)
                    .OrderByDescending(g => g.Count())
                    .ToList();
                
                if (groupedPrices.Any())
                {
                    dto.Precio = groupedPrices.First().Key;
                    _logger.LogInformation("Precio final seleccionado: {Price} (apareció {Count} veces)", 
                        dto.Precio, groupedPrices.First().Count());
                    return;
                }
            }
            else
            {
                _logger.LogWarning("No se encontraron precios válidos (todos fuera del rango 0.01-100000)");
            }
        }
        else
        {
            _logger.LogWarning("No se encontraron precios con ningún patrón");
        }

        // Si no se encontró precio, intentar buscar en el texto visible
        if (dto.Precio == 0m)
        {
            _logger.LogInformation("No se encontró precio con patrones específicos, buscando en texto visible...");
            ExtractPriceFromVisibleText(html, dto);
        }
        
        // Si aún no se encontró, hacer una búsqueda muy agresiva
        if (dto.Precio == 0m)
        {
            _logger.LogInformation("Haciendo búsqueda agresiva de precios...");
            ExtractPriceAggressive(html, dto);
        }
    }
    
    private void ExtractPriceAggressive(string html, ProductDetailsDto dto)
    {
        // Buscar cualquier número que parezca un precio (muy agresivo)
        // Buscar patrones como: $XX.XX, XX.XX, XX,XX.XX, etc.
        var aggressivePatterns = new[]
        {
            @"\$\s*(\d{1,3}(?:[.,]\d{2})?)", // $19.99, $19,99, $19
            @"(\d{1,3}(?:[.,]\d{2})?)\s*USD", // 19.99 USD
            @"(\d{1,3}(?:[.,]\d{2})?)\s*\$", // 19.99 $
            @"price[^>]*>.*?(\d{1,3}(?:[.,]\d{2})?)", // price>19.99
            @"cost[^>]*>.*?(\d{1,3}(?:[.,]\d{2})?)", // cost>19.99
        };
        
        var foundPrices = new List<decimal>();
        
        foreach (var pattern in aggressivePatterns)
        {
            try
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var priceStr = match.Groups[1].Value.Trim()
                            .Replace(",", ".")
                            .Replace("'", "");
                        
                        // Normalizar separador decimal
                        if (priceStr.Contains(",") && !priceStr.Contains("."))
                        {
                            priceStr = priceStr.Replace(",", ".");
                        }
                        
                        if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var price) &&
                            price >= 0.50m && price <= 50000m) // Rango más razonable
                        {
                            foundPrices.Add(price);
                            _logger.LogDebug("Precio encontrado (agresivo): {Price}", price);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en búsqueda agresiva con patrón: {Pattern}", pattern);
            }
        }
        
        if (foundPrices.Any())
        {
            // Filtrar valores atípicos y usar el más común
            var validPrices = foundPrices
                .Where(p => p >= 1m && p <= 10000m)
                .Select(p => Math.Round(p, 2))
                .ToList();
            
            if (validPrices.Any())
            {
                var mostCommon = validPrices
                    .GroupBy(p => p)
                    .OrderByDescending(g => g.Count())
                    .First();
                
                dto.Precio = mostCommon.Key;
                _logger.LogInformation("Precio encontrado (búsqueda agresiva): {Price} (apareció {Count} veces)", 
                    dto.Precio, mostCommon.Count());
            }
        }
    }

    private void ExtractPriceFromVisibleText(string html, ProductDetailsDto dto)
    {
        // Buscar precios en el texto visible (entre tags)
        var visiblePricePatterns = new[]
        {
            @"<[^>]*>\s*\$?\s*(\d{1,6}\.?\d{0,2})\s*</[^>]*>",
            @"<span[^>]*>\s*\$?\s*(\d{1,6}\.?\d{0,2})\s*</span>",
            @"<div[^>]*>\s*\$?\s*(\d{1,6}\.?\d{0,2})\s*</div>",
        };

        var allPrices = new List<decimal>();

        foreach (var pattern in visiblePricePatterns)
        {
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var priceStr = match.Groups[1].Value.Trim().Replace(",", "");
                    if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price) && 
                        price >= 0.01m && price <= 100000m)
                    {
                        allPrices.Add(price);
                    }
                }
            }
        }

        if (allPrices.Any())
        {
            // Usar el precio más común que esté en un rango razonable
            var reasonablePrices = allPrices.Where(p => p >= 1m && p <= 50000m).ToList();
            if (reasonablePrices.Any())
            {
                dto.Precio = reasonablePrices
                    .GroupBy(p => Math.Round(p, 2))
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;
                _logger.LogInformation("Precio extraído del texto visible: {Price}", dto.Precio);
            }
        }
    }

    private void ExtractImageFromHtml(string html, ProductDetailsDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.URLImagen)) 
        {
            _logger.LogInformation("Imagen ya obtenida: {Image}", dto.URLImagen);
            return; // Ya se obtuvo la imagen
        }

        _logger.LogInformation("Buscando imagen en HTML...");

        // Patrones más robustos y específicos
        var imagePatterns = new List<string>();

        // Patrones generales
        imagePatterns.AddRange(new[]
        {
            // Open Graph (ya se busca antes, pero por si acaso)
            @"<meta\s+property=[""']og:image[""']\s+content=[""']([^""']+)[""']",
            // JSON-LD
            @"""image""\s*:\s*[""']([^""']+)[""']",
            @"""image""\s*:\s*\[[""']([^""']+)[""']", // Array de imágenes
            // Imágenes con atributos específicos
            @"<img[^>]*id=[""']landingImage[""'][^>]*src=[""']([^""']+)[""']", // Amazon landingImage
            @"<img[^>]*id=[""']landingImage[""'][^>]*data-src=[""']([^""']+)[""']", // Amazon lazy load
            @"<img[^>]*data-a-dynamic-image=[""']([^""']+)[""']", // Amazon dynamic image
            @"<img[^>]*class=[""'][^""']*product-image[^""']*[""'][^>]*src=[""']([^""']+)[""']",
            @"<img[^>]*class=[""'][^""']*product-image[^""']*[""'][^>]*data-src=[""']([^""']+)[""']",
            @"<img[^>]*class=[""'][^""']*main-image[^""']*[""'][^>]*src=[""']([^""']+)[""']",
            @"<img[^>]*class=[""'][^""']*main-image[^""']*[""'][^>]*data-src=[""']([^""']+)[""']",
            // Data attributes
            @"data-src=[""']([^""']+\.(jpg|jpeg|png|webp|gif))[""']",
            @"data-lazy-src=[""']([^""']+\.(jpg|jpeg|png|webp|gif))[""']",
            @"data-original=[""']([^""']+\.(jpg|jpeg|png|webp|gif))[""']",
            // Cualquier img con src
            @"<img[^>]*src=[""']([^""']+\.(jpg|jpeg|png|webp|gif))[""']",
        });

        var baseUrl = string.Empty;
        try
        {
            // Intentar extraer la URL base para convertir URLs relativas
            var urlMatch = Regex.Match(html, @"<base[^>]*href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (urlMatch.Success)
            {
                baseUrl = urlMatch.Groups[1].Value;
            }
        }
        catch { }

        foreach (var pattern in imagePatterns)
        {
            try
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var imageUrl = match.Groups[1].Value.Trim();
                        
                        // Limpiar la URL
                        imageUrl = imageUrl.Replace("\\/", "/").Replace("\\\"", "");
                        
                        // Convertir URL relativa a absoluta
                        if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            if (imageUrl.StartsWith("//"))
                            {
                                imageUrl = "https:" + imageUrl;
                            }
                            else if (imageUrl.StartsWith("/"))
                            {
                                // Intentar construir URL absoluta desde la base
                                if (!string.IsNullOrEmpty(baseUrl))
                                {
                                    var uri = new Uri(baseUrl);
                                    imageUrl = $"{uri.Scheme}://{uri.Host}{imageUrl}";
                                }
                                else
                                {
                                    continue; // No podemos construir URL absoluta
                                }
                            }
                            else
                            {
                                continue; // URL relativa sin base
                            }
                        }
                        
                        // Validar que sea una URL de imagen válida
                        if (imageUrl.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            imageUrl.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            imageUrl.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
                            imageUrl.Contains(".webp", StringComparison.OrdinalIgnoreCase) ||
                            imageUrl.Contains("image", StringComparison.OrdinalIgnoreCase))
                        {
                            dto.URLImagen = imageUrl;
                            _logger.LogInformation("Imagen encontrada: {Image}", dto.URLImagen);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al procesar patrón de imagen: {Pattern}", pattern);
            }
        }
        
        _logger.LogWarning("No se encontró imagen con ningún patrón");
    }

    private void ExtractTitleFromHtml(string html, ProductDetailsDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Nombre) && dto.Nombre != "Producto") return;

        // Buscar título en diferentes lugares
        var titlePatterns = new[]
        {
            @"<title>(.*?)</title>",
            @"<h1[^>]*class=[""'][^""']*product[^""']*title[^""']*[""'][^>]*>(.*?)</h1>",
            @"<h1[^>]*>(.*?)</h1>",
        };

        foreach (var pattern in titlePatterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                var title = HtmlDecode(match.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(title) && title.Length > 3)
                {
                    dto.Nombre = title.Trim();
                    return;
                }
            }
        }
    }

    private static string InferProvider(string url)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains("shein"))
            return "Shein";
        if (lower.Contains("temu"))
            return "Temu";
        if (lower.Contains("amazon"))
            return "Amazon";

        return "Desconocido";
    }

    private static string HtmlDecode(string html)
    {
        return System.Net.WebUtility.HtmlDecode(html);
    }
}
