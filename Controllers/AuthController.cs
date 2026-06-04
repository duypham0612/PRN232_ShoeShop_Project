using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShop.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace ShoeShop.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PRN232_ShoeShopContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(PRN232_ShoeShopContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class LoginResponse
        {
            public int UserId { get; set; }
            public string FullName { get; set; }
            public int RoleId { get; set; }
            public string RedirectTo { get; set; }
        }

        public class RegisterRequest
        {
            [Required]
            public string FullName { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            public string Password { get; set; }

            public string Phone { get; set; }
            public string Address { get; set; }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.Email) || string.IsNullOrEmpty(req.Password))
            {
                return BadRequest("Email and password are required.");
            }

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user == null)
            {
                return Unauthorized("Email or password is incorrect.");
            }

            // If account is locked/inactive return explicit message
            if (!user.IsActive)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "This account has been locked");
            }

            bool passwordMatches = false;

            // If password hash looks like bcrypt (starts with $2a$/$2b$/$2y$) use BCrypt.Verify
            var stored = user.PasswordHash ?? string.Empty;
            try
            {
                if (stored.StartsWith("$2a$") || stored.StartsWith("$2b$") || stored.StartsWith("$2y$") || stored.StartsWith("$2$"))
                {
                    passwordMatches = BCrypt.Net.BCrypt.Verify(req.Password, stored);
                }
                else
                {
                    // Legacy formats handling:
                    // 1) Plaintext stored password (not recommended) -> direct compare
                    if (stored == req.Password)
                    {
                        passwordMatches = true;
                        _logger.LogWarning("User {Email} had plaintext password stored. Migrating to bcrypt.", user.Email);
                    }
                    else
                    {
                        // 2) MD5 hex
                        if (Regex.IsMatch(stored, "^[a-fA-F0-9]{32}$"))
                        {
                            using var md5 = MD5.Create();
                            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(req.Password));
                            var md5Hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            if (md5Hex == stored.ToLowerInvariant())
                            {
                                passwordMatches = true;
                                _logger.LogWarning("User {Email} had MD5 password hash. Migrating to bcrypt.", user.Email);
                            }
                        }
                    }

                    // If we matched by legacy method, rehash with bcrypt and save
                    if (passwordMatches)
                    {
                        try
                        {
                            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
                            _context.Users.Update(user);
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to migrate password hash for user {Email}", user.Email);
                        }
                    }
                }
            }
            catch (BCrypt.Net.SaltParseException ex)
            {
                // Stored hash is invalid for BCrypt
                _logger.LogWarning(ex, "Invalid password hash format for user {Email}", user.Email);
                return Unauthorized("Stored password has invalid format. Please reset your password.");
            }

            if (!passwordMatches)
            {
                return Unauthorized("Invalid email or password.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Staff")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            var resp = new LoginResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                RoleId = user.RoleId,
                RedirectTo = (user.Role != null && user.Role.RoleName == "Admin") || user.RoleId == 1 ? "/admin/users.html" : "/"
            };

            return Ok(resp);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            // Basic model validation
            if (req == null)
            {
                return BadRequest("Request body is required.");
            }

            // Trim inputs
            req.FullName = req.FullName?.Trim();
            req.Email = req.Email?.Trim();
            req.Phone = req.Phone?.Trim();
            req.Address = req.Address?.Trim();

            var errors = new List<string>();

            // Required fields
            if (string.IsNullOrWhiteSpace(req.FullName)) errors.Add("Full name is required.");
            if (string.IsNullOrWhiteSpace(req.Email)) errors.Add("Email is required.");
            if (string.IsNullOrWhiteSpace(req.Password)) errors.Add("Password is required.");

            // Email format
            if (!string.IsNullOrWhiteSpace(req.Email) && !new EmailAddressAttribute().IsValid(req.Email))
            {
                errors.Add("Email format is invalid.");
            }

            // Password strength: min 8 chars, at least 1 upper, 1 lower, 1 digit, 1 special
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                var pwd = req.Password;
                var pwdPattern = new Regex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[\\W_]).{8,}$");
                if (!pwdPattern.IsMatch(pwd))
                {
                    errors.Add("Password must be at least 8 characters and include uppercase, lowercase, number and special character.");
                }
            }

            // Phone basic check if provided
            if (!string.IsNullOrWhiteSpace(req.Phone))
            {
                var phonePattern = new Regex("^\\+?[0-9]{7,15}$");
                if (!phonePattern.IsMatch(req.Phone))
                {
                    errors.Add("Phone number is invalid.");
                }
            }

            // Address length
            if (!string.IsNullOrWhiteSpace(req.Address) && req.Address.Length > 255)
            {
                errors.Add("Address is too long (max 255 characters).");
            }

            if (errors.Any())
            {
                return BadRequest(string.Join(" ", errors));
            }

            // Check email uniqueness
            var exists = await _context.Users.AnyAsync(u => u.Email == req.Email);
            if (exists)
            {
                return Conflict("Email has already been taken.");
            }

            // Hash password
            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            // Default role: Staff (RoleId = 2). If not found, try to find role by name 'Staff'.
            int roleId = 2;
            var staffRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == 2 || r.RoleName == "Staff");
            if (staffRole != null) roleId = staffRole.RoleId;

            var user = new User
            {
                FullName = req.FullName,
                Email = req.Email,
                PasswordHash = hash,
                Phone = req.Phone,
                Address = req.Address,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Do not sign in automatically. Return created info and suggested redirect
            return CreatedAtAction(nameof(Register), new { id = user.UserId }, new { userId = user.UserId, redirectTo = "/account/login.html" });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }
    }
}
