using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;
using ShoeShop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ShoeShop
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add DbContext
            builder.Services.AddDbContext<PRN232_ShoeShopContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            // Add Controllers + OData
            builder.Services.AddControllers()
    .AddOData(opt =>
    {
        var odataBuilder = new ODataConventionModelBuilder();
        odataBuilder.EntitySet<Shoe>("Shoes");
        odataBuilder.EntitySet<Brand>("Brands");
        odataBuilder.EntitySet<Category>("Categories");
        opt.EnableQueryFeatures();
        opt.Select().Filter().OrderBy().Expand().Count().SetMaxTop(100);
        opt.AddRouteComponents("odata", odataBuilder.GetEdmModel());
    });

            // Authentication: Cookie-based
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/account/login.html";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                });

            // Authorization: role-based policies
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                options.AddPolicy("StaffOrAdmin", policy => policy.RequireRole("Staff", "Admin"));
            });

            // Add services to the container.
            
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            // Serve default files (index.html) from wwwroot and static files
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapControllers();

            // Seed roles and an admin user for testing if they do not exist
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<PRN232_ShoeShopContext>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                    // Ensure database can be accessed
                    db.Database.EnsureCreated();

                    if (!db.Roles.Any(r => r.RoleName == "Admin"))
                    {
                        db.Roles.Add(new Role { RoleName = "Admin" });
                    }
                    if (!db.Roles.Any(r => r.RoleName == "Staff"))
                    {
                        db.Roles.Add(new Role { RoleName = "Staff" });
                    }
                    db.SaveChanges();

                    // Create default admin user if missing
                    if (!db.Users.Any(u => u.Email == "admin@shoeshop.local"))
                    {
                        var adminRole = db.Roles.First(r => r.RoleName == "Admin");
                        var admin = new User
                        {
                            FullName = "Administrator",
                            Email = "admin@shoeshop.local",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                            RoleId = adminRole.RoleId,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        db.Users.Add(admin);
                        db.SaveChanges();
                        logger.LogInformation("Seeded default admin: admin@shoeshop.local / Admin@123");
                    }
                }
                catch (Exception ex)
                {
                    // do not crash application if seeding fails; log error
                    var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
                    logger?.LogError(ex, "Error while seeding database");
                }
            }

            app.Run();
        }
    }
}
