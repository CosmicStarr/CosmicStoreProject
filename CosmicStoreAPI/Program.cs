using StackExchange.Redis;
using CosmicStoreAPI.Error;
using CosmicStoreAPI.Middleware;
using Data;
using Data.Classes;
using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Data.Util;
using Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.SignIn.RequireConfirmedEmail = false;
    opt.User.RequireUniqueEmail = true;
    opt.Password.RequireUppercase = true;
    opt.Password.RequiredLength = 8; // Fixed: Swapped back to RequiredLength
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
    options.RequireHttpsMetadata = false; // Set to true when deploying to production!
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidateIssuer = true,
        ValidateLifetime = true,
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:SecretKey"]!))
    };
});

// ==========================================
// 3. REDIS & CACHING
// ==========================================
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(c =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString, true);
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IDatabase>(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = redisConnectionString;
});

builder.Services.AddSingleton<ICacheService, CacheService>();

// ==========================================
// 4. DEPENDENCY INJECTION (Services & Clients)
// ==========================================
builder.Services.Configure<CjAuthRequest>(builder.Configuration.GetSection("CJDropshipping"));

//builder.Services.AddHttpClient<CjAuthManager>();
builder.Services.AddHttpClient<ICJDropshippingService, CJDropshippingService>();
//builder.Services.AddHostedService<CJProductSyncWorker>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IStoreUnitOfWork, StoreUnitOfWork>();
builder.Services.AddScoped<IEditCjProducts, EditCjProducts>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<ExceptionMiddleware>();
builder.Services.AddHttpClient(); // Generic client for the worker

// ==========================================
// 5. CONTROLLERS & API BEHAVIOR
// ==========================================
builder.Services.AddControllers();
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

// ==========================================
// 6. HTTP REQUEST PIPELINE (Order is Strict!)
// ==========================================
app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePagesWithReExecute("/errors/{0}");

if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRouting(); // Routing must come before CORS and Auth

// CORS must sit exactly between UseRouting and UseAuthentication
app.UseCors(opt =>
{
    opt.AllowAnyHeader()
       .AllowAnyMethod()
       .WithOrigins("http://localhost:4200", "https://localhost:4200");
});

app.UseAuthentication(); // Uncommented so JWTs are processed
app.UseAuthorization();  // Uncommented so [Authorize] tags work

app.MapControllers();

app.Run();