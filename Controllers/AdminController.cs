using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShop.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ShoeShop.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : ControllerBase
    {
        private readonly PRN232_ShoeShopContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(PRN232_ShoeShopContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Example: admin has full rights: read/write/delete
        [HttpGet("info")]
        public IActionResult Info()
        {
            return Ok(new { message = "Admin area - full access" });
        }

        [HttpDelete("resource/{id}")]
        public IActionResult DeleteResource(int id)
        {
            // ... perform delete
            return Ok(new { message = $"Deleted resource {id}" });
        }

        // GET api/admin/users?search=...
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search)
        {
            var q = _context.Users.AsNoTracking().Include(u => u.Role).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(u => EF.Functions.Like(u.FullName, $"%{s}%") || EF.Functions.Like(u.Email, $"%{s}%"));
            }

            var users = await q.OrderBy(u => u.UserId).Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.Address,
                Role = u.Role != null ? u.Role.RoleName : null,
                u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToListAsync();

            return Ok(users);
        }

        // GET api/admin/users/{id}
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.Phone,
                user.Address,
                Role = user.Role?.RoleName,
                user.RoleId,
                user.IsActive,
                user.CreatedAt
            });
        }

        public class CreateUserRequest
        {
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public int? RoleId { get; set; }
        }

        // POST api/admin/users
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
        {
            var errors = ValidateUserInput(req.FullName, req.Email, req.Password, req.Phone, req.Address, requirePassword: true);
            if (errors.Any()) return BadRequest(string.Join(" ", errors));

            if (await _context.Users.AnyAsync(u => u.Email == req.Email))
                return Conflict("Email already in use.");

            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            int roleId = req.RoleId ?? 2; // default to staff
            // ensure role exists
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null)
            {
                var staffRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Staff");
                if (staffRole != null) roleId = staffRole.RoleId;
            }

            var user = new User
            {
                FullName = req.FullName.Trim(),
                Email = req.Email.Trim(),
                PasswordHash = hash,
                Phone = req.Phone?.Trim(),
                Address = req.Address?.Trim(),
                RoleId = roleId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, new { userId = user.UserId });
        }

        public class UpdateUserRequest
        {
            public string FullName { get; set; }
            public string Email { get; set; }
            // Password intentionally omitted for edit
            public string Phone { get; set; }
            public string Address { get; set; }
            public int? RoleId { get; set; }
            public bool? IsActive { get; set; }
        }

        // PUT api/admin/users/{id}
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest req)
        {
            var errors = ValidateUserInput(req.FullName, req.Email, null, req.Phone, req.Address, requirePassword: false);
            if (errors.Any()) return BadRequest(string.Join(" ", errors));

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.Email) && req.Email.Trim() != user.Email)
            {
                if (await _context.Users.AnyAsync(u => u.Email == req.Email.Trim() && u.UserId != id))
                    return Conflict("Email already in use by another user.");
                user.Email = req.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(req.FullName)) user.FullName = req.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(req.Phone)) user.Phone = req.Phone.Trim();
            if (!string.IsNullOrWhiteSpace(req.Address)) user.Address = req.Address.Trim();
            if (req.IsActive.HasValue) user.IsActive = req.IsActive.Value;

            if (req.RoleId.HasValue)
            {
                var role = await _context.Roles.FindAsync(req.RoleId.Value);
                if (role != null) user.RoleId = role.RoleId;
            }

            // Password update not allowed in edit endpoint

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE api/admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private List<string> ValidateUserInput(string fullName, string email, string password, string phone, string address, bool requirePassword)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(fullName)) errors.Add("Full name is required.");
            if (string.IsNullOrWhiteSpace(email)) errors.Add("Email is required.");
            else if (!new EmailAddressAttribute().IsValid(email)) errors.Add("Email format is invalid.");

            if (requirePassword)
            {
                if (string.IsNullOrWhiteSpace(password)) errors.Add("Password is required.");
                else
                {
                    var pwdPattern = new Regex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[\\W_]).{8,}$");
                    if (!pwdPattern.IsMatch(password)) errors.Add("Password must be at least 8 characters and include uppercase, lowercase, number and special character.");
                }
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phonePattern = new Regex("^\\+?[0-9]{7,15}$");
                if (!phonePattern.IsMatch(phone)) errors.Add("Phone number is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(address) && address.Length > 255) errors.Add("Address is too long (max 255 characters).");

            return errors;
        }
    }
}
