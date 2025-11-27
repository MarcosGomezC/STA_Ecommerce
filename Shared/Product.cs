namespace STA_Ecommerce.Shared;

public class Product
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string Proveedor { get; set; } = string.Empty; // Shein, Temu o Amazon
    public string EnlaceAfiliado { get; set; } = string.Empty;
    public string URLImagen { get; set; } = string.Empty;
    public string? Categoria { get; set; }
}


