using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// ⚠️ CRITICAL FIX: Disable file watching in production
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);

// Add Database Context Service
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=waste_management.db"));

// Enable Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

// Report Generation module
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

app.UseSession();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();