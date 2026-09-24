using StackExchange.Redis;
using CosmicStoreAPI.Error;
using CosmicStoreAPI.Middleware;
using CosmicStoreAPI.Util;
using Data;
using Data.Classes;
using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Data.Util;
using Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;

if (args.Any(a => string.Equals(a, "graph-auth", StringComparison.OrdinalIgnoreCase)))
{
    var setupBuilder = WebApplication.CreateBuilder(args);
    using var loggerFactory = LoggerFactory.Create(logging =>
    {
        logging.AddSimpleConsole(options => options.SingleLine = true);
        logging.SetMinimumLevel(LogLevel.Information);
    });

    await GraphMailAuth.ConnectInteractiveAsync(
        setupBuilder.Configuration,
        loggerFactory.CreateLogger("GraphMailAuth"));
    return;
}

if (args.Any(a => string.Equals(a, "graph-check", StringComparison.OrdinalIgnoreCase)))
{
    var setupBuilder = WebApplication.CreateBuilder(args);
    using var loggerFactory = LoggerFactory.Create(logging =>
    {
        logging.AddSimpleConsole(options => options.SingleLine = true);
        logging.SetMinimumLevel(LogLevel.Information);
    });
    var logger = loggerFactory.CreateLogger("GraphMailAuth");

    try
    {
        await GraphMailAuth.AssertDelegatedSessionAsync(setupBuilder.Configuration);
        Console.WriteLine("Microsoft Graph mail is signed in and ready to send.");
    }
    catch (Exception)
    {
        Console.WriteLine("Microsoft Graph mail is not ready. From the repo root run: .\\Scripts\\connect-outlook-graph.ps1");
        Environment.ExitCode = 1;
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);
ProductionGuard.EnsureReady(builder.Configuration, builder.Environment);

builder.Services.Configure<HostOptions>(options =>
{
    // SQL blips in background workers must not take the App Service down with 503.
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var jwtIssuer = builder.Configuration["JWT:ValidIssuer"];
if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    jwtIssuer = StoreUrls.PublicOrigin(builder.Configuration).TrimEnd('/') + "/";
}

var corsOrigins = StoreUrls.CorsOrigins(builder.Configuration, builder.Environment.IsDevelopment());

// ==========================================
// 1. DATABASE SETUP
// ==========================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDbContext<ApplicationDbStoreContext>(options =>
    options.UseSqlServer(connectionString));

// ==========================================
// 2. IDENTITY & AUTHENTICATION
// ==========================================
builder.Services.Configure<TokenSettings>(builder.Configuration.GetSection("JWT"));
builder.Services.PostConfigure<TokenSettings>(settings =>
{
    if (string.IsNullOrWhiteSpace(settings.ValidIssuer))
    {
        settings.ValidIssuer = jwtIssuer;
    }

    if (string.IsNullOrWhiteSpace(settings.ValidAudience))
    {
        settings.ValidAudience = "User";
    }
});

builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.SignIn.RequireConfirmedEmail = false;
    opt.User.RequireUniqueEmail = true;
    opt.Password.RequireUppercase = true;
    opt.Password.RequiredLength = 8; // Fixed: Swapped back to RequiredLength
    opt.Lockout.AllowedForNewUsers = true;
    opt.Lockout.MaxFailedAccessAttempts = AccountLockouts.MaxFailedAccessAttempts;
    opt.Lockout.DefaultLockoutTimeSpan = AccountLockouts.TemporaryDuration;
})
.AddEntityFrameworkStores<ApplicationDbStoreContext>()
.AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(o => {
    o.TokenLifespan = TimeSpan.FromHours(3);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidateIssuer = true,
        ValidateLifetime = true,
        ValidAudience = builder.Configuration["JWT:ValidAudience"] ?? "User",
        ValidIssuer = jwtIssuer,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:SecretKey"]!))
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (context.Principal is null || GuestPrincipal.IsGuest(context.Principal))
            {
                return;
            }

            var userId = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                context.Fail("Invalid token");
                return;
            }

            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByIdAsync(userId);
            if (user == null || await userManager.IsLockedOutAsync(user))
            {
                context.Fail("Account locked");
            }
        }
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// ==========================================
// 3. REDIS & CACHING
// ==========================================
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("ConnectionStrings:RedisConnection is required in Production.");
    }

    redisConnectionString = "localhost:6379";
}

builder.Services.AddSingleton<IConnectionMultiplexer>(c =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString, true);
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IDatabase>(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = redisConnectionString;
});

builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddSingleton<GuestOrderRateLimiter>();

// ==========================================
// 4. DEPENDENCY INJECTION (Services & Clients)
// ==========================================
builder.Services.Configure<CjAuthRequest>(builder.Configuration.GetSection("CJDropshipping"));

builder.Services.AddHttpClient<CjAuthManager>();
builder.Services.AddHttpClient<ICJDropshippingService, CJDropshippingService>();
builder.Services.AddHostedService<CJProductSyncWorker>();
if (builder.Environment.IsProduction())
{
    builder.Services.AddHostedService<CjOrderStatusWorker>();
}
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailChangeService, EmailChangeService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IStoreUnitOfWork, StoreUnitOfWork>();
builder.Services.AddScoped<IEditCjProducts, EditCjProducts>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICjCatalogSyncService, CjCatalogSyncService>();
builder.Services.AddScoped<IStoreSettingsService, StoreSettingsService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IWishlistRegistryService, WishlistRegistryService>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddTransient<ExceptionMiddleware>();
builder.Services.AddHttpClient(); // Generic client for the worker

// ==========================================
// 5. CONTROLLERS & API BEHAVIOR
// ==========================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Product <-> ProductImages is a valid EF graph. Without this, save/publish
        // (and any endpoint that returns a tracked product) throws a JSON cycle error.
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddCors();

builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ActionContext =>
    {
        var errors = ActionContext.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value?.Errors ?? Enumerable.Empty<ModelError>())
            .Select(e => e.ErrorMessage).ToList();
            
        var errorResponse = new ApiValidationResponse
        {
            Errors = errors
        };

        return new BadRequestObjectResult(errorResponse);
    };
});

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

var shouldMigrate = app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment());
if (shouldMigrate)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var storeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbStoreContext>();
        await storeDb.Database.MigrateAsync();

        var catalogDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await catalogDb.Database.MigrateAsync();

        if (app.Environment.IsDevelopment())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var leftoverGuests = await userManager.Users
                .Where(user => user.Email != null && user.Email.EndsWith("@guest.cosmicstore.local"))
                .ToListAsync();
            foreach (var leftover in leftoverGuests)
            {
                await userManager.DeleteAsync(leftover);
            }
        }
    }
    catch (Exception ex)
    {
        // Do not take the whole App Service down with a 503 when SQL/migrate fails;
        // keep the process up so Log Stream shows the error and non-DB probes can respond.
        startupLogger.LogCritical(
            ex,
            "Database migrate-on-startup failed. The API process will continue, but data endpoints will fail until SQL/App Settings are fixed.");
    }
}

// Always try to add CatalogSyncEnabled even when full MigrateAsync failed earlier.
// Settings/admin endpoints query this column; missing it returns the generic 500 message.
try
{
    using var schemaScope = app.Services.CreateScope();
    var storeDb = schemaScope.ServiceProvider.GetRequiredService<ApplicationDbStoreContext>();
    await storeDb.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID('store.StoreRuntimeSettings', 'U') IS NOT NULL
           AND COL_LENGTH('store.StoreRuntimeSettings', 'CatalogSyncEnabled') IS NULL
        BEGIN
            ALTER TABLE [store].[StoreRuntimeSettings]
            ADD [CatalogSyncEnabled] BIT NOT NULL
                CONSTRAINT [DF_StoreRuntimeSettings_CatalogSyncEnabled] DEFAULT (1);

            IF NOT EXISTS (
                SELECT 1 FROM [store].[__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260924120000_CatalogSyncEnabled')
            BEGIN
                INSERT INTO [store].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260924120000_CatalogSyncEnabled', N'10.0.0');
            END
        END
        """);
}
catch (Exception ex)
{
    startupLogger.LogError(ex, "Could not ensure CatalogSyncEnabled column on StoreRuntimeSettings.");
}

using (var bootstrapScope = app.Services.CreateScope())
{
    var roleManager = bootstrapScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await AdminBootstrap.EnsureAdminRoleAsync(roleManager);
}

if (string.IsNullOrWhiteSpace(app.Configuration["Stripe:WebhookSecret"]))
{
    app.Logger.LogWarning("Stripe:WebhookSecret is not set. Dashboard refunds and async payment events will be rejected.");
}

if (app.Environment.IsDevelopment()
    && !GraphMailAuth.IsClientCredentials(app.Configuration)
    && (!File.Exists(GraphMailAuth.TokenCachePath) || !File.Exists(GraphMailAuth.AccountIdPath)))
{
    app.Logger.LogWarning("Microsoft Graph is not signed in. From the repo root run: .\\Scripts\\connect-outlook-graph.ps1");
}

// ==========================================
// 6. HTTP REQUEST PIPELINE (Order is Strict!)
// ==========================================
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePagesWithReExecute("/errors/{0}");

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return Task.CompletedTask;
    });
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/templates"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseRouting(); // Routing must come before CORS and Auth

app.UseCors(opt =>
{
    opt.AllowAnyHeader()
       .AllowAnyMethod()
       .WithOrigins(corsOrigins);
});

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"message":"Not found"}""");
        return;
    }

    var webRoot = app.Environment.WebRootPath
        ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var index = Path.Combine(webRoot, "index.html");
    if (!File.Exists(index))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(
            "Storefront is not published. From the repo root run Scripts/publish-store.ps1.");
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(index);
});

app.Run();
