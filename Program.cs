// Program.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Models;
using TisaWasteManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context Service
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=waste_management.db"));

// ===== STEP 1: Enable Session =====
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

// ===== Report Generation module: register the PDF/Excel report service =====
// "Scoped" means one instance is created per web request - same lifetime as
// the ApplicationDbContext it works alongside.
builder.Services.AddScoped<IReportGenerator, ReportGenerator>();

// ===== SMS Module: register the SMS-sending service =====
// AddHttpClient() lets us request an IHttpClientFactory in SmsService,
// which is used to call the TextBee API.
// "Scoped" = one instance per web request, same as ISmsService above.
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISmsService, SmsService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

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

// ===== Admin Account module: seed one default Admin account =====
// Phase 1 used a hardcoded "admin" / "Admin" login. Now that Admin login
// checks the AdminAccount table (see AccountController.Login), the table
// needs at least one row to exist, or nobody could log in as Admin at all.
//
// This block runs every time the app starts, but only actually creates an
// account the FIRST time - once AdminAccount has at least one row, the
// "if (!context.AdminAccount.Any())" check below skips it on every later
// startup. This is simpler for a beginner project than writing a separate
// SQL script or a one-off console tool.
//
// IMPORTANT: change this password after your first login in a real deployment.
//using (var scope = app.Services.CreateScope())
//{
//    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

//    if (!context.AdminAccount.Any())
//    {
//        var seedHasher = new PasswordHasher<AdminAccount>();
//        var defaultAdmin = new AdminAccount
//        {
//            Username = "admin",
//            CreatedDate = DateTime.Now
//        };
//        defaultAdmin.PasswordHash = seedHasher.HashPassword(defaultAdmin, "Admin123!");

//        context.AdminAccount.Add(defaultAdmin);
//        context.SaveChanges();
//    }
//}

app.Run();
