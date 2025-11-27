namespace STA_Ecommerce.Shared;

public class ProductDetailsDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string URLImagen { get; set; } = string.Empty;
}


