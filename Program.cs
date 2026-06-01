using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;
using ShoeShop.Models;
using Microsoft.EntityFrameworkCore;
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

            app.UseAuthorization();
            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}
