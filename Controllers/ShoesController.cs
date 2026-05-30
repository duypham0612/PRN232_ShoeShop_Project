using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ShoeShop.Models;

namespace ShoeShop.Controllers
{
    // Lớp này kế thừa ODataController để kích hoạt định tuyến tự động /odata/Shoes
    public class ShoesController : ODataController
    {
        private readonly PRN232_ShoeShopContext _context;

        public ShoesController(PRN232_ShoeShopContext context)
        {
            _context = context;
        }

        // 1. ODATA: GET /odata/Shoes
        // Hỗ trợ: filter, sort, paging, expand ($expand=Brand,Category)
        [EnableQuery(MaxTop = 100)]
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(_context.Shoes);
        }

        // 2. ODATA: GET /odata/Shoes(5)
        [EnableQuery]
        [HttpGet]
        public IActionResult Get([FromODataUri] int key)
        {
            var shoe = _context.Shoes.FirstOrDefault(s => s.ShoeId == key);
            if (shoe == null) return NotFound();
            return Ok(shoe);
        }

        // 3. API CRUD: POST /odata/Shoes (Tạo mới + Validation)
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Shoe shoe)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Validation logic theo yêu cầu
            if (shoe.Price <= 0) return BadRequest("Giá (Price) phải lớn hơn 0.");
            if (shoe.StockQuantity < 0) return BadRequest("Số lượng kho (Stock) không được âm.");
            if (string.IsNullOrEmpty(shoe.ShoeName)) return BadRequest("Tên giày không được để trống.");

            _context.Shoes.Add(shoe);
            await _context.SaveChangesAsync();

            return Created(shoe);
        }

        // 4. API CRUD: PUT /odata/Shoes(5) (Cập nhật)
        [HttpPut]
        public async Task<IActionResult> Put([FromODataUri] int key, [FromBody] Shoe update)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (key != update.ShoeId) return BadRequest();

            var existing = await _context.Shoes.FindAsync(key);
            if (existing == null) return NotFound();

            // Cập nhật giá trị
            existing.ShoeName = update.ShoeName;
            existing.Price = update.Price;
            existing.StockQuantity = update.StockQuantity;
            existing.Status = update.Status;
            existing.BrandId = update.BrandId;
            existing.CategoryId = update.CategoryId;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Shoes.Any(s => s.ShoeId == key)) return NotFound();
                else throw;
            }

            return Updated(existing);
        }

        // 5. API CRUD: DELETE /odata/Shoes(5) (Xóa)
        [HttpDelete]
        public async Task<IActionResult> Delete([FromODataUri] int key)
        {
            var shoe = await _context.Shoes.FindAsync(key);
            if (shoe == null) return NotFound();

            _context.Shoes.Remove(shoe);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}