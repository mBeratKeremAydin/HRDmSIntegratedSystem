using HRDms.Data.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Session (Oturum) servisini ekliyoruz.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // 30 dakika hareketsiz kalýrsa oturum düþer.
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Session middleware'ini aktif ediyoruz (UseRouting'den sonra olmalý)
app.UseSession();

app.MapControllerRoute(
    name: "default",
    // DÜZELTÝLEN KISIM BURASI: Home yerine Account yazdýk.
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();