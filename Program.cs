using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Disable file watching in production (fixes inotify limit on Render)
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);

// Add Database Context Service
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=waste_management.db"));

// ===== STEP 1: Enable Session =====
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

// ===== Report Generation module: register the PDF/Excel report service =====
builder.Services.AddScoped<IReportGenerator, ReportGenerator>();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add SMS Service
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISmsService, SmsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// ===== STEP 1: Add Session Middleware =====
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

// ===== STEP 1: Default route = Home/Index (Landing Page) =====
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();