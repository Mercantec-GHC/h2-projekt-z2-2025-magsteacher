using API.Data;
using DomainModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DomainModels.Mapping;
using API.Services;
using System.Security.Claims;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDBContext _context;
        private readonly JwtService _jwtService;

        public UsersController(AppDBContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        // GET: api/Users
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.Include(u => u.Role).ToListAsync();
        }

        // GET: api/Users/UUID
        [HttpGet("{id}")]
        public async Task<ActionResult<UserGetDto>> GetUser(string id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            return UserMapping.ToUserGetDto(user);
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(string id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Users
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (_context.Users.Any(u => u.Email == dto.Email))
                return BadRequest("En bruger med denne email findes allerede.");

            // Hash password
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Find standard User rolle
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole == null)
                return BadRequest("Standard brugerrolle ikke fundet.");

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = dto.Email,
                HashedPassword = hashedPassword,
                Username = dto.Username,
                PasswordBackdoor = dto.Password,
                RoleId = userRole.Id,
                CreatedAt = DateTime.UtcNow.AddHours(2),
                UpdatedAt = DateTime.UtcNow.AddHours(2),

            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bruger oprettet!", user.Email, role = userRole.Name });
        }

        /// <summary>
        /// Login endpoint der returnerer JWT token
        /// </summary>
        /// <param name="dto">Login credentials</param>
        /// <returns>JWT token og brugerinfo</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);
                
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.HashedPassword))
                return Unauthorized("Forkert email eller adgangskode");
            
            user.LastLogin = DateTime.UtcNow.AddHours(2);
            await _context.SaveChangesAsync();

            // Generer JWT token
            var token = _jwtService.GenerateToken(user);

            return Ok(new { 
                message = "Login godkendt!", 
                token = token,
                user = new {
                    id = user.Id,
                    email = user.Email,
                    username = user.Username,
                    role = user.Role?.Name ?? "User"
                }
            });
        }

        /// <summary>
        /// Hent information om den nuværende bruger baseret på JWT token
        /// </summary>
        /// <returns>Brugerens information</returns>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            // 1. Hent ID fra token (typisk sat som 'sub' claim ved oprettelse af JWT)
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
                return Unauthorized("Bruger-ID ikke fundet i token.");

            // 2. Slå brugeren op i databasen
            var user = await _context.Users
                .Include(u => u.Role) // inkluder relaterede data
                .Include(u => u.Info) // inkluder brugerinfo hvis relevant
                .Include(u => u.Bookings) // inkluder bookinger
                    .ThenInclude(b => b.Room) // inkluder rum for hver booking
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound("Brugeren blev ikke fundet i databasen.");

            // 3. Returnér ønskede data - fx til profilsiden
            return Ok(new
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin,
                Role = user.Role?.Name ?? "User",
                RoleDescription = user.Role?.Description,
                // UserInfo hvis relevant
                Info = user.Info != null ? new {
                    user.Info.FirstName,
                    user.Info.LastName,
                    user.Info.Phone
                } : null,
                // Bookinger hvis relevant
                Bookings = user.Bookings.Select(b => new {
                    b.Id,
                    b.StartDate,
                    b.EndDate,
                    b.CreatedAt,
                    b.UpdatedAt,
                    Room = b.Room != null ? new {
                        b.Room.Id,
                        b.Room.Number,
                        b.Room.Capacity,
                        HotelId = b.Room.HotelId
                    } : null
                }).ToList()
            });
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Users/{id}/role
        [HttpPut("{id}/role")]
        public async Task<IActionResult> AssignUserRole(string id, AssignRoleDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("Bruger ikke fundet.");
            }

            var role = await _context.Roles.FindAsync(dto.RoleId);
            if (role == null)
            {
                return BadRequest("Ugyldig rolle.");
            }

            user.RoleId = dto.RoleId;
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok(new { message = "Rolle tildelt til bruger!", user.Email, role = role.Name });
        }

        // GET: api/Users/role/{roleName}
        [HttpGet("role/{roleName}")]
        public async Task<ActionResult<IEnumerable<User>>> GetUsersByRole(string roleName)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                return BadRequest("Ugyldig rolle.");
            }

            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.RoleId == role.Id)
                .ToListAsync();

            return users;
        }

        // DELETE: api/Users/{id}/role
        [HttpDelete("{id}/role")]
        public async Task<IActionResult> RemoveUserRole(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("Bruger ikke fundet.");
            }

            // Find standard User rolle
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole == null)
                return BadRequest("Standard brugerrolle ikke fundet.");

            // Sæt til default rolle
            user.RoleId = userRole.Id;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Rolle fjernet fra bruger. Tildelt standard rolle.", user.Email });
        }

        // GET: api/Users/roles
        [HttpGet("roles")]
        public async Task<ActionResult<object>> GetAvailableRoles()
        {
            var roles = await _context.Roles
                .Select(r => new { 
                    id = r.Id,
                    name = r.Name, 
                    description = r.Description,
                })
                .ToListAsync();

            return Ok(roles);
        }

        private bool UserExists(string id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }

    // DTO til rolle tildeling
    public class AssignRoleDto
    {
        public string RoleId { get; set; } = string.Empty;
    }
}
