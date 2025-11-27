namespace STA_Ecommerce.Shared;

public static class ProductCategory
{
    public const string RopaModa = "Ropa y Moda";
    public const string Electronica = "Electrónica";
    public const string HogarDecoracion = "Hogar y Decoración";
    public const string BellezaCuidado = "Belleza y Cuidado Personal";
    public const string Accesorios = "Accesorios";

    public static readonly string[] All = new[]
    {
        RopaModa,
        Electronica,
        HogarDecoracion,
        BellezaCuidado,
        Accesorios
    };

    public static string GetCategoryIcon(string category)
    {
        return category switch
        {
            RopaModa => "👕",
            Electronica => "📱",
            HogarDecoracion => "🏠",
            BellezaCuidado => "💄",
            Accesorios => "👜",
            _ => "📦"
        };
    }

    public static string GetCategoryColor(string category)
    {
        return category switch
        {
            RopaModa => "primary",
            Electronica => "info",
            HogarDecoracion => "success",
            BellezaCuidado => "warning",
            Accesorios => "secondary",
            _ => "dark"
        };
    }
}

