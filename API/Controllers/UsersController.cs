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
    /// Implementerer struktureret fejlhåndtering med standardiserede responser.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDBContext _context;
        private readonly JwtService _jwtService;
        private readonly LoginAttemptService _loginAttemptService;
        private readonly ILogger<UsersController> _logger;

        /// <summary>
        /// Initialiserer en ny instans af UsersController.
        /// </summary>
        /// <param name="context">Database context til adgang til brugerdata.</param>
        /// <param name="jwtService">Service til håndtering af JWT tokens.</param>
        /// <param name="loginAttemptService">Service til håndtering af login forsøg og rate limiting.</param>
        /// <param name="logger">Logger til fejlrapportering.</param>
        public UsersController(AppDBContext context, JwtService jwtService, LoginAttemptService loginAttemptService, ILogger<UsersController> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _loginAttemptService = loginAttemptService;
            _logger = logger;
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
            try
            {
                _logger.LogInformation("Henter alle brugere - anmodet af administrator");
                
                var users = await _context.Users
                    .Include(u => u.Role)
                    .ToListAsync();

                _logger.LogInformation("Hentet {UserCount} brugere succesfuldt", users.Count);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af alle brugere");
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af brugere");
            }
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
            try
            {
                _logger.LogInformation("Henter bruger med ID: {UserId}", id);

                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    _logger.LogWarning("Bruger med ID {UserId} ikke fundet", id);
                    return NotFound();
                }

                _logger.LogInformation("Bruger med ID {UserId} hentet succesfuldt", id);
                return UserMapping.ToUserGetDto(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af bruger med ID: {UserId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af bruger");
            }
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
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(string id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            try
            {
                _logger.LogInformation("Opdaterer bruger med ID: {UserId}", id);

                _context.Entry(user).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Bruger med ID {UserId} opdateret succesfuldt", id);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency konflikt ved opdatering af bruger: {UserId}", id);
                
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved opdatering af bruger: {UserId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved opdatering af bruger");
            }
        }

        /// <summary>
        /// Registrerer en ny bruger i systemet.
        /// </summary>
        /// <param name="dto">Registreringsdata for den nye bruger.</param>
        /// <returns>Bekræftelse på brugeroprettelsen.</returns>
        /// <response code="200">Brugeren blev oprettet succesfuldt.</response>
        /// <response code="400">Ugyldig forespørgsel - email eksisterer allerede eller manglende data.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                _logger.LogInformation("Registrerer ny bruger med email: {Email}", dto.Email);

                if (_context.Users.Any(u => u.Email == dto.Email))
                {
                    _logger.LogWarning("Forsøg på at registrere bruger med eksisterende email: {Email}", dto.Email);
                    return BadRequest("En bruger med denne email findes allerede.");
                }

                // Hash password
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

                // Find standard User rolle
                var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
                if (userRole == null)
                {
                    _logger.LogError("Standard brugerrolle ikke fundet i systemet");
                    return BadRequest("Standard brugerrolle ikke fundet.");
                }

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

                _logger.LogInformation("Ny bruger registreret succesfuldt: {Email}", dto.Email);

                return Ok(new { message = "Bruger oprettet!", user.Email, role = userRole.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved registrering af bruger: {Email}", dto?.Email);
                return StatusCode(500, "Der opstod en intern serverfejl ved oprettelse af bruger");
            }
        }

        /// <summary>
        /// Login endpoint der autentificerer bruger og returnerer JWT token.
        /// Implementerer rate limiting og progressive delays for at forhindre brute force angreb.
        /// </summary>
        /// <param name="dto">Login credentials indeholdende email og adgangskode.</param>
        /// <returns>JWT token og brugerinformation ved succesfuldt login.</returns>
        /// <response code="200">Login godkendt - returnerer token og brugerinfo.</response>
        /// <response code="401">Ikke autoriseret - forkert email eller adgangskode.</response>
        /// <response code="429">For mange forsøg - konto midlertidigt låst.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            try
            {
                _logger.LogInformation("Login forsøg for email: {Email}", dto.Email);

                // Tjek om email er låst på grund af for mange mislykkede forsøg
                if (_loginAttemptService.IsLockedOut(dto.Email))
                {
                    var remainingSeconds = _loginAttemptService.GetRemainingLockoutSeconds(dto.Email);
                    _logger.LogWarning("Login forsøg for låst email: {Email}, {RemainingSeconds} sekunder tilbage", 
                        dto.Email, remainingSeconds);
                    
                    return StatusCode(429, new { 
                        message = "Konto midlertidigt låst på grund af for mange mislykkede login forsøg.",
                        remainingLockoutSeconds = remainingSeconds
                    });
                }

                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == dto.Email);
                    
                if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.HashedPassword))
                {
                    _logger.LogWarning("Mislykket login forsøg for email: {Email}", dto.Email);
                    
                    // Registrer mislykket forsøg og få delay tid
                    var delaySeconds = _loginAttemptService.RecordFailedAttempt(dto.Email);
                    
                    if (delaySeconds > 0)
                    {
                        _logger.LogInformation("Påføring af {DelaySeconds} sekunders delay for email: {Email}", 
                            delaySeconds, dto.Email);
                        
                        // Påfør progressiv delay
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                        
                        return Unauthorized(new { 
                            message = "Forkert email eller adgangskode",
                            delayApplied = delaySeconds
                        });
                    }
                    else
                    {
                        // Konto er nu låst
                        var remainingSeconds = _loginAttemptService.GetRemainingLockoutSeconds(dto.Email);
                        return StatusCode(429, new { 
                            message = "For mange mislykkede forsøg. Konto er nu midlertidigt låst.",
                            remainingLockoutSeconds = remainingSeconds
                        });
                    }
                }
                
                // Succesfuldt login - ryd fejl cache
                _loginAttemptService.RecordSuccessfulLogin(dto.Email);
                
                user.LastLogin = DateTime.UtcNow.AddHours(2);
                await _context.SaveChangesAsync();

                // Generer JWT token
                var token = _jwtService.GenerateToken(user);

                _logger.LogInformation("Succesfuldt login for bruger: {Email}", dto.Email);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved login for email: {Email}", dto?.Email);
                return StatusCode(500, "Der opstod en intern serverfejl ved login");
            }
        }

        /// <summary>
        /// Henter information om den nuværende bruger baseret på JWT token.
        /// </summary>
        /// <returns>Detaljeret brugerinformation inklusiv roller, info og bookinger.</returns>
        /// <response code="200">Brugerinformation blev hentet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="404">Brugeren blev ikke fundet i databasen.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                // Hent ID fra token (typisk sat som 'sub' claim ved oprettelse af JWT)
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId == null)
                {
                    return Unauthorized("Bruger-ID ikke fundet i token.");
                }

                _logger.LogInformation("Henter nuværende bruger info for ID: {UserId}", userId);

                // Slå brugeren op i databasen
                var user = await _context.Users
                    .Include(u => u.Role) // inkluder relaterede data
                    .Include(u => u.Info) // inkluder brugerinfo hvis relevant
                    .Include(u => u.Bookings) // inkluder bookinger
                        .ThenInclude(b => b.Room) // inkluder rum for hver booking
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    _logger.LogWarning("Bruger med ID {UserId} ikke fundet i database", userId);
                    return NotFound("Brugeren blev ikke fundet i databasen.");
                }

                _logger.LogInformation("Nuværende bruger info hentet succesfuldt for ID: {UserId}", userId);

                // Returner ønskede data - fx til profilsiden
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af nuværende bruger");
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af brugerinfo");
            }
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
            try
            {
                _logger.LogInformation("Sletter bruger med ID: {UserId}", id);

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Bruger med ID {UserId} ikke fundet for sletning", id);
                    return NotFound();
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Bruger med ID {UserId} slettet succesfuldt", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved sletning af bruger: {UserId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved sletning af bruger");
            }
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
            try
            {
                _logger.LogInformation("Tildeler rolle {RoleId} til bruger {UserId}", dto.RoleId, id);

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Bruger med ID {UserId} ikke fundet for rolletildeling", id);
                    return NotFound("Bruger ikke fundet.");
                }

                var role = await _context.Roles.FindAsync(dto.RoleId);
                if (role == null)
                {
                    _logger.LogWarning("Rolle med ID {RoleId} ikke fundet", dto.RoleId);
                    return BadRequest("Ugyldig rolle.");
                }

                user.RoleId = dto.RoleId;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Rolle {RoleName} tildelt til bruger {UserEmail}", role.Name, user.Email);

                return Ok(new { message = "Rolle tildelt til bruger!", user.Email, role = role.Name });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency konflikt ved tildeling af rolle til bruger: {UserId}", id);
                
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved tildeling af rolle til bruger: {UserId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved tildeling af rolle");
            }
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
            try
            {
                _logger.LogInformation("Henter brugere med rolle: {RoleName}", roleName);

                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                if (role == null)
                {
                    _logger.LogWarning("Rolle {RoleName} ikke fundet", roleName);
                    return BadRequest("Ugyldig rolle.");
                }

                var users = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.RoleId == role.Id)
                    .ToListAsync();

                _logger.LogInformation("Hentet {UserCount} brugere med rolle {RoleName}", users.Count, roleName);
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af brugere med rolle: {RoleName}", roleName);
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af brugere");
            }
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
            try
            {
                _logger.LogInformation("Fjerner rolle fra bruger: {UserId}", id);

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Bruger med ID {UserId} ikke fundet for rollefjernelse", id);
                    return NotFound("Bruger ikke fundet.");
                }

                // Find standard User rolle
                var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
                if (userRole == null)
                {
                    _logger.LogError("Standard brugerrolle ikke fundet i systemet");
                    return BadRequest("Standard brugerrolle ikke fundet.");
                }

                // Sæt til default rolle
                user.RoleId = userRole.Id;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Rolle fjernet fra bruger {UserEmail}, sat til standard rolle", user.Email);

                return Ok(new { message = "Rolle fjernet fra bruger. Tildelt standard rolle.", user.Email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved fjernelse af rolle fra bruger: {UserId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved fjernelse af rolle");
            }
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
            try
            {
                _logger.LogInformation("Henter alle tilgængelige roller");

                var roles = await _context.Roles
                    .Select(r => new { 
                        id = r.Id,
                        name = r.Name, 
                        description = r.Description,
                    })
                    .ToListAsync();

                _logger.LogInformation("Hentet {RoleCount} roller succesfuldt", roles.Count);
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af roller");
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af roller");
            }
        }

        /// <summary>
        /// Henter login forsøg status for en email adresse. Kun til administratorer.
        /// </summary>
        /// <param name="email">Email adressen der skal tjekkes.</param>
        /// <returns>Information om login forsøg status.</returns>
        /// <response code="200">Login status hentet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - kun administratorer har adgang.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize(Roles = "Admin")]
        [HttpGet("login-status/{email}")]
        public IActionResult GetLoginStatus(string email)
        {
            try
            {
                _logger.LogInformation("Henter login status for email: {Email}", email);

                var attemptInfo = _loginAttemptService.GetLoginAttemptInfo(email);
                var isLockedOut = _loginAttemptService.IsLockedOut(email);
                var remainingLockoutSeconds = _loginAttemptService.GetRemainingLockoutSeconds(email);

                var status = new
                {
                    email = email,
                    isLockedOut = isLockedOut,
                    failedAttempts = attemptInfo?.FailedAttempts ?? 0,
                    lastAttempt = attemptInfo?.LastAttempt,
                    lockoutUntil = attemptInfo?.LockoutUntil,
                    remainingLockoutSeconds = remainingLockoutSeconds
                };

                _logger.LogInformation("Login status hentet for {Email}: {FailedAttempts} fejl, låst: {IsLockedOut}", 
                    email, status.failedAttempts, isLockedOut);

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af login status for email: {Email}", email);
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af login status");
            }
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

    /// <summary>
    /// DTO til rolle tildeling
    /// </summary>
    public class AssignRoleDto
    {
        /// <summary>
        /// ID på rollen der skal tildeles
        /// </summary>
        public string RoleId { get; set; } = string.Empty;
    }
}