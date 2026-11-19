using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Endpoints;
using UFWebsite.Models;
using UFWebsite.Services;

var builder = WebApplication.CreateBuilder(args);

// ----- Database -----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        // Loopia runs SQL Server 2019 (compatibility level 150). Telling EF Core this
        // makes it only generate SQL that SQL Server 2019 understands.
        sql => sql.UseCompatibilityLevel(150)));

// ----- Accounts, passwords and roles (ASP.NET Core Identity) -----
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Ticshare.Auth";
    options.Cookie.HttpOnly = true;                      // JavaScript can't read the cookie
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/login";
    // The API answers 401/403 instead of redirecting to the admin login page.
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 401;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 403;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// Only users with the Admin role get into /admin.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AllowAnonymousToPage("/Admin/Login");
});

// Send statuses to JavaScript as text ("Available") instead of numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddScoped<TicketFlow>();
builder.Services.AddScoped<EmailSender>();

// Stops spam on the contact form: at most 5 messages per 10 minutes from the same computer.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("kontakt", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "okänd",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Du har skickat flera meddelanden på kort tid. Vänta en stund och försök igen." }, token);
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath!, "index.html"));
});

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapAuthEndpoints();
app.MapMatchEndpoints();
app.MapSellerEndpoints();
app.MapOrderEndpoints();
app.MapContactEndpoints();

// Creates or updates the database tables when the site starts.
// Needed on Loopia, where we can't run "dotnet ef database update" against the server.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

await SeedData.CreateAdminAsync(app.Services);

app.Run();