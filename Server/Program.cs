using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using STA_Ecommerce.Server.Data;
using STA_Ecommerce.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// CORS mejorado - Más restrictivo en producción
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Desarrollo: permisivo
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Producción: restrictivo
            // IMPORTANTE: Cambiar por tu dominio real
            policy.WithOrigins("https://tudominio.com", "https://www.tudominio.com")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Rate Limiting - Protección contra abuso
builder.Services.AddRateLimiter(options =>
{
    // Límite general para la API
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });

    // Límite estricto para login
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Límite para scraping de productos
    options.AddFixedWindowLimiter("scraping", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Demasiadas solicitudes. Por favor, espera un momento."
        }, cancellationToken: token);
    };
});

// Base de datos (SQLite para MVP, migrar a PostgreSQL/SQL Server en producción)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       "Data Source=sta_ecommerce.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
   // options.UseSqlite(connectionString);

    // En producción, descomentar esto para PostgreSQL:
    //options.UseNpgsql(connectionString);

    // O para SQL Server:
     options.UseSqlServer(connectionString);
});

// Identity con configuración mejorada
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        // Configuración de contraseña más segura
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Lockout para prevenir brute force
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // Configuración de usuario
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ??
    throw new InvalidOperationException("JWT Key no configurada");

// Validar que la clave sea lo suficientemente larga
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT Key debe tener al menos 32 caracteres");
}

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (builder.Environment.IsDevelopment())
            {
                Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
            }
            return Task.CompletedTask;
        }
    };
});

// HttpClient Factory con configuración mejorada
builder.Services.AddHttpClient("ProductFetcher", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
});

// Servicios de la aplicación
builder.Services.AddScoped<IProductDataFetcherService, ProductDataFetcherService>();
builder.Services.AddScoped<IClickTrackingService, ClickTrackingService>();

// Memory Cache para optimización
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// HTTPS obligatorio en producción
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
else
{
    // En desarrollo también es recomendable
    app.UseHttpsRedirection();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseCors();

// Rate limiting
app.UseRateLimiter();

// Response caching
app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

// Crear BD y usuario admin por defecto
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var config = services.GetRequiredService<IConfiguration>();

        const string adminRole = "Admin";

        // Obtener credenciales desde configuración
        var adminEmail = config["AdminUser:Email"] ?? "admin@sta.local";
        var adminPassword = config["AdminUser:Password"] ?? "Admin123$";

        // Crear rol de administrador si no existe
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
            logger.LogInformation("Rol de administrador creado");
        }

        // Crear usuario administrador si no existe
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, adminRole);
                logger.LogInformation("Usuario administrador creado: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Error al crear usuario administrador: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al inicializar la base de datos");
    }
}

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();