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
    /// <summary>
    /// Controller til håndtering af bruger-relaterede operationer.
    /// Indeholder funktionalitet til brugeradministration, autentificering og autorisering.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDBContext _context;
        private readonly JwtService _jwtService;

        /// <summary>
        /// Initialiserer en ny instans af UsersController.
        /// </summary>
        /// <param name="context">Database context til adgang til brugerdata.</param>
        /// <param name="jwtService">Service til håndtering af JWT tokens.</param>
        public UsersController(AppDBContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Henter alle brugere fra systemet. Kun tilgængelig for administratorer.
        /// </summary>
        /// <returns>En liste af alle brugere med deres roller.</returns>
        /// <response code="200">Brugerlisten blev hentet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - kun administratorer har adgang.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.Include(u => u.Role).ToListAsync();
        }

        /// <summary>
        /// Henter en specifik bruger baseret på ID.
        /// </summary>
        /// <param name="id">Unikt ID for brugeren.</param>
        /// <returns>Brugerens information.</returns>
        /// <response code="200">Brugeren blev fundet og returneret.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - utilstrækkelige rettigheder.</response>
        /// <response code="404">Bruger med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize(Roles = "Admin, User")]
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

        /// <summary>
        /// Opdaterer en eksisterende bruger.
        /// </summary>
        /// <param name="id">ID på brugeren der skal opdateres.</param>
        /// <param name="user">Opdaterede brugerdata.</param>
        /// <returns>Bekræftelse på opdateringen.</returns>
        /// <response code="204">Brugeren blev opdateret succesfuldt.</response>
        /// <response code="400">Ugyldig forespørgsel - ID matcher ikke bruger ID.</response>
        /// <response code="404">Bruger med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        /// <remarks>
        /// For at beskytte mod overposting angreb, se https://go.microsoft.com/fwlink/?linkid=2123754
        /// </remarks>
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

        /// <summary>
        /// Registrerer en ny bruger i systemet.
        /// </summary>
        /// <param name="dto">Registreringsdata for den nye bruger.</param>
        /// <returns>Bekræftelse på brugeroprettelsen.</returns>
        /// <response code="200">Brugeren blev oprettet succesfuldt.</response>
        /// <response code="400">Ugyldig forespørgsel - email eksisterer allerede eller manglende data.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        /// <remarks>
        /// For at beskytte mod overposting angreb, se https://go.microsoft.com/fwlink/?linkid=2123754
        /// Adgangskoden bliver hashet før gemning i databasen.
        /// </remarks>
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
        /// Login endpoint der autentificerer bruger og returnerer JWT token.
        /// </summary>
        /// <param name="dto">Login credentials indeholdende email og adgangskode.</param>
        /// <returns>JWT token og brugerinformation ved succesfuldt login.</returns>
        /// <response code="200">Login godkendt - returnerer token og brugerinfo.</response>
        /// <response code="401">Ikke autoriseret - forkert email eller adgangskode.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        /// <remarks>
        /// Opdaterer brugerens sidste login tidspunkt ved succesfuldt login.
        /// </remarks>
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
        /// Henter information om den nuværende bruger baseret på JWT token.
        /// </summary>
        /// <returns>Detaljeret brugerinformation inklusiv roller, info og bookinger.</returns>
        /// <response code="200">Brugerinformation blev hentet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="404">Brugeren blev ikke fundet i databasen.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        /// <remarks>
        /// Returnerer omfattende brugerdata inklusiv relaterede entiteter som bookinger og rum.
        /// </remarks>
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

        /// <summary>
        /// Sletter en bruger fra systemet.
        /// </summary>
        /// <param name="id">ID på brugeren der skal slettes.</param>
        /// <returns>Bekræftelse på sletningen.</returns>
        /// <response code="204">Brugeren blev slettet succesfuldt.</response>
        /// <response code="404">Bruger med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
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

        /// <summary>
        /// Tildeler en rolle til en bruger.
        /// </summary>
        /// <param name="id">ID på brugeren der skal tildeles en rolle.</param>
        /// <param name="dto">Data med rolle-ID der skal tildeles.</param>
        /// <returns>Bekræftelse på rolletildelingen.</returns>
        /// <response code="200">Rollen blev tildelt succesfuldt.</response>
        /// <response code="400">Ugyldig rolle eller forespørgsel.</response>
        /// <response code="404">Bruger med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
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

        /// <summary>
        /// Henter alle brugere med en specifik rolle.
        /// </summary>
        /// <param name="roleName">Navnet på rollen der skal filtreres på.</param>
        /// <returns>En liste af brugere med den angivne rolle.</returns>
        /// <response code="200">Brugerlisten blev hentet succesfuldt.</response>
        /// <response code="400">Ugyldig rolle - rollen eksisterer ikke.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
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

        /// <summary>
        /// Fjerner en brugers rolle og sætter den til standard brugerrolle.
        /// </summary>
        /// <param name="id">ID på brugeren hvis rolle skal fjernes.</param>
        /// <returns>Bekræftelse på rollefjernelsen.</returns>
        /// <response code="200">Rollen blev fjernet og bruger sat til standard rolle.</response>
        /// <response code="400">Standard brugerrolle ikke fundet i systemet.</response>
        /// <response code="404">Bruger med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
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

        /// <summary>
        /// Henter alle tilgængelige roller i systemet.
        /// </summary>
        /// <returns>En liste af alle roller med ID, navn og beskrivelse.</returns>
        /// <response code="200">Rollerne blev hentet succesfuldt.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
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

        /// <summary>
        /// Hjælpemetode til at kontrollere om en bruger eksisterer.
        /// </summary>
        /// <param name="id">ID på brugeren der skal kontrolleres.</param>
        /// <returns>True hvis brugeren eksisterer, ellers false.</returns>
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
