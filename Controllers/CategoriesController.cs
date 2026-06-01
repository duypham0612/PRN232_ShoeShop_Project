using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using ShoeShop.Models;

namespace ShoeShop.Controllers
{
    public class CategoriesController : ODataController
    {
        private readonly PRN232_ShoeShopContext _context;
        public CategoriesController(PRN232_ShoeShopContext context) => _context = context;

        [EnableQuery]
        public IActionResult Get() => Ok(_context.Categories);
    }
}