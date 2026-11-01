using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Endpoints;
using UFWebsite.Models;
using UFWebsite.Services;
 
var builder = WebApplication.CreateBuilder(args);
 
// ----- Database -----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
 
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
 
var app = builder.Build();
 
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
 
app.UseHttpsRedirection();
 
app.UseRouting();
 
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
 
await SeedData.CreateAdminAsync(app.Services);
 
app.Run();
 