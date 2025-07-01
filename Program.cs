using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Configure Database and Identity ---

// Get the connection string from appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Add the ApplicationDbContext to the services container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    // Configure EF Core to use the PostgreSQL provider (Npgsql).
    options.UseNpgsql(connectionString));

// Add ASP.NET Core Identity services.
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // You can configure Identity options here, e.g., password requirements.
    options.SignIn.RequireConfirmedAccount = false; // Set to false for easier development/testing.
})
    // Tell Identity to use our ApplicationDbContext to store its data.
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --- 2. Add other services ---

// Add services for Razor Pages.
builder.Services.AddRazorPages();


// --- 3. Build the application ---
var app = builder.Build();


// --- 4. Configure the HTTP request pipeline ---

// Configure error handling for development vs. production.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: UseAuthentication must come before UseAuthorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();


// --- 5. Run the application ---
app.Run();
