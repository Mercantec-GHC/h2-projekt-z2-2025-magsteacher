using API.Data;
using DomainModels;
using DomainModels.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Controllers
{
    /// <summary>
    /// Controller til håndtering af booking-relaterede operationer.
    /// Indeholder funktionalitet til CRUD operationer for bookinger.
    /// Implementerer struktureret fejlhåndtering med logging og try-catch.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly AppDBContext _context;
        private readonly ILogger<BookingsController> _logger;

        /// <summary>
        /// Initialiserer en ny instans af BookingsController.
        /// </summary>
        /// <param name="context">Database context til adgang til bookingdata.</param>
        /// <param name="logger">Logger til fejlrapportering.</param>
        public BookingsController(AppDBContext context, ILogger<BookingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Henter alle bookinger fra systemet. Kun tilgængelig for administratorer.
        /// </summary>
        /// <returns>En liste af alle bookinger med bruger og rum information.</returns>
        /// <response code="200">Bookingerne blev hentet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - kun administratorer har adgang.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookingGetDto>>> GetBookings()
        {
            try
            {
                _logger.LogInformation("Henter alle bookinger - anmodet af administrator");

                var bookings = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hotel)
                    .ToListAsync();

                _logger.LogInformation("Hentet {BookingCount} bookinger succesfuldt", bookings.Count);
                return Ok(BookingMapping.ToBookingGetDtos(bookings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af alle bookinger");
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af bookinger");
            }
        }

        /// <summary>
        /// Henter en specifik booking baseret på ID.
        /// </summary>
        /// <param name="id">Unikt ID for bookingen.</param>
        /// <returns>Bookingens information.</returns>
        /// <response code="200">Bookingen blev fundet og returneret.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - kan kun se egne bookinger.</response>
        /// <response code="404">Booking med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<BookingGetDto>> GetBooking(string id)
        {
            try
            {
                _logger.LogInformation("Henter booking med ID: {BookingId}", id);

                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hotel)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (booking == null)
                {
                    _logger.LogWarning("Booking med ID {BookingId} ikke fundet", id);
                    return NotFound();
                }

                // Tjek om brugeren har adgang til denne booking
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin");
                
                if (!isAdmin && booking.UserId != currentUserId)
                {
                    _logger.LogWarning("Bruger {UserId} forsøgte at få adgang til booking {BookingId} som tilhører bruger {BookingUserId}", 
                        currentUserId, id, booking.UserId);
                    return Forbid();
                }

                _logger.LogInformation("Booking med ID {BookingId} hentet succesfuldt", id);
                return Ok(BookingMapping.ToBookingGetDto(booking));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af booking med ID: {BookingId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af booking");
            }
        }

        /// <summary>
        /// Henter alle bookinger for den nuværende bruger.
        /// </summary>
        /// <returns>Liste af brugerens bookinger.</returns>
        /// <response code="200">Bookingerne blev hentet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<BookingGetDto>>> GetMyBookings()
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized("Bruger-ID ikke fundet i token");
                }

                _logger.LogInformation("Henter bookinger for bruger: {UserId}", currentUserId);

                var bookings = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hotel)
                    .Where(b => b.UserId == currentUserId)
                    .ToListAsync();

                _logger.LogInformation("Hentet {BookingCount} bookinger for bruger {UserId}", bookings.Count, currentUserId);
                return Ok(BookingMapping.ToBookingGetDtos(bookings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af brugerens bookinger");
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af bookinger");
            }
        }

        /// <summary>
        /// Henter alle bookinger for et specifikt rum.
        /// </summary>
        /// <param name="roomId">Rum ID for at filtrere bookinger.</param>
        /// <returns>Liste af bookinger for det angivne rum.</returns>
        /// <response code="200">Bookingerne blev hentet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - kun administratorer har adgang.</response>
        /// <response code="404">Rum med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize(Roles = "Admin")]
        [HttpGet("room/{roomId}")]
        public async Task<ActionResult<IEnumerable<BookingGetDto>>> GetBookingsByRoom(string roomId)
        {
            try
            {
                _logger.LogInformation("Henter bookinger for rum: {RoomId}", roomId);

                // Tjek om rummet eksisterer
                var roomExists = await _context.Rooms.AnyAsync(r => r.Id == roomId);
                if (!roomExists)
                {
                    _logger.LogWarning("Rum med ID {RoomId} ikke fundet", roomId);
                    return NotFound("Rum ikke fundet");
                }

                var bookings = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hotel)
                    .Where(b => b.RoomId == roomId)
                    .ToListAsync();

                _logger.LogInformation("Hentet {BookingCount} bookinger for rum {RoomId}", bookings.Count, roomId);
                return Ok(BookingMapping.ToBookingGetDtos(bookings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af bookinger for rum: {RoomId}", roomId);
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af bookinger");
            }
        }

        /// <summary>
        /// Opdaterer en eksisterende booking.
        /// </summary>
        /// <param name="id">ID på bookingen der skal opdateres.</param>
        /// <param name="bookingPutDto">Opdaterede bookingdata.</param>
        /// <returns>Bekræftelse på opdateringen.</returns>
        /// <response code="204">Bookingen blev opdateret succesfuldt.</response>
        /// <response code="400">Ugyldig forespørgsel eller booking overlap.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - kan kun opdatere egne bookinger.</response>
        /// <response code="404">Booking med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBooking(string id, BookingPutDto bookingPutDto)
        {
            if (id != bookingPutDto.Id)
            {
                return BadRequest("ID i URL matcher ikke booking ID");
            }

            try
            {
                _logger.LogInformation("Opdaterer booking med ID: {BookingId}", id);

                var booking = await _context.Bookings.FindAsync(id);
                if (booking == null)
                {
                    _logger.LogWarning("Booking med ID {BookingId} ikke fundet for opdatering", id);
                    return NotFound();
                }

                // Tjek om brugeren har adgang til denne booking
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin");
                
                if (!isAdmin && booking.UserId != currentUserId)
                {
                    _logger.LogWarning("Bruger {UserId} forsøgte at opdatere booking {BookingId} som tilhører bruger {BookingUserId}", 
                        currentUserId, id, booking.UserId);
                    return Forbid();
                }

                // Valider datoer
                if (bookingPutDto.EndDate <= bookingPutDto.StartDate)
                {
                    return BadRequest("Slut dato skal være efter start dato");
                }

                if (bookingPutDto.StartDate < DateTime.UtcNow.Date)
                {
                    return BadRequest("Start dato kan ikke være i fortiden");
                }

                // Tjek for overlappende bookinger (ekskludér den nuværende booking)
                var hasOverlap = await _context.Bookings
                    .AnyAsync(b => b.RoomId == bookingPutDto.RoomId 
                                && b.Id != id
                                && b.BookingStatus != "Cancelled"
                                && b.StartDate < bookingPutDto.EndDate 
                                && b.EndDate > bookingPutDto.StartDate);

                if (hasOverlap)
                {
                    _logger.LogWarning("Booking overlap opdaget for rum {RoomId} i periode {StartDate} til {EndDate}", 
                        bookingPutDto.RoomId, bookingPutDto.StartDate, bookingPutDto.EndDate);
                    return BadRequest("Det valgte rum er allerede booket i den angivne periode");
                }

                // Hent rum for at få prisen
                var room = await _context.Rooms.FindAsync(bookingPutDto.RoomId);
                if (room == null)
                {
                    return BadRequest("Det angivne rum eksisterer ikke");
                }

                BookingMapping.UpdateBookingFromPutDto(booking, bookingPutDto, room.PricePerNight);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Booking med ID {BookingId} opdateret succesfuldt", id);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency konflikt ved opdatering af booking: {BookingId}", id);
                
                if (!await BookingExists(id))
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
                _logger.LogError(ex, "Fejl ved opdatering af booking: {BookingId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved opdatering af booking");
            }
        }

        /// <summary>
        /// Opretter en ny booking i systemet.
        /// </summary>
        /// <param name="bookingPostDto">Data for den nye booking.</param>
        /// <returns>Den oprettede booking.</returns>
        /// <response code="201">Bookingen blev oprettet succesfuldt.</response>
        /// <response code="400">Ugyldig forespørgsel eller booking overlap.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<BookingGetDto>> PostBooking(BookingPostDto bookingPostDto)
        {
            try
            {
                _logger.LogInformation("Opretter ny booking for bruger {UserId} i rum {RoomId}", bookingPostDto.UserId, bookingPostDto.RoomId);

                // Tjek om brugeren har adgang til at oprette booking for denne bruger
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin");
                
                if (!isAdmin && bookingPostDto.UserId != currentUserId)
                {
                    _logger.LogWarning("Bruger {UserId} forsøgte at oprette booking for bruger {BookingUserId}", 
                        currentUserId, bookingPostDto.UserId);
                    return Forbid("Du kan kun oprette bookinger for dig selv");
                }

                // Valider datoer
                if (bookingPostDto.EndDate <= bookingPostDto.StartDate)
                {
                    return BadRequest("Slut dato skal være efter start dato");
                }

                if (bookingPostDto.StartDate < DateTime.UtcNow.Date)
                {
                    return BadRequest("Start dato kan ikke være i fortiden");
                }

                // Tjek om brugeren eksisterer
                var userExists = await _context.Users.AnyAsync(u => u.Id == bookingPostDto.UserId);
                if (!userExists)
                {
                    _logger.LogWarning("Forsøg på at oprette booking for ikke-eksisterende bruger: {UserId}", bookingPostDto.UserId);
                    return BadRequest("Den angivne bruger eksisterer ikke");
                }

                // Hent rum for at få pris og tjekke kapacitet
                var room = await _context.Rooms.FindAsync(bookingPostDto.RoomId);
                if (room == null)
                {
                    _logger.LogWarning("Forsøg på at oprette booking for ikke-eksisterende rum: {RoomId}", bookingPostDto.RoomId);
                    return BadRequest("Det angivne rum eksisterer ikke");
                }

                // Tjek om rummet har kapacitet til antal gæster
                if (room.Capacity < bookingPostDto.NumberOfGuests)
                {
                    _logger.LogWarning("Rum {RoomId} har kun kapacitet til {Capacity} gæster, men {NumberOfGuests} gæster anmodet", 
                        bookingPostDto.RoomId, room.Capacity, bookingPostDto.NumberOfGuests);
                    return BadRequest($"Rummet har kun kapacitet til {room.Capacity} gæster");
                }

                // Tjek for overlappende bookinger
                var hasOverlap = await _context.Bookings
                    .AnyAsync(b => b.RoomId == bookingPostDto.RoomId 
                                && b.BookingStatus != "Cancelled"
                                && b.StartDate < bookingPostDto.EndDate 
                                && b.EndDate > bookingPostDto.StartDate);

                if (hasOverlap)
                {
                    _logger.LogWarning("Booking overlap opdaget for rum {RoomId} i periode {StartDate} til {EndDate}", 
                        bookingPostDto.RoomId, bookingPostDto.StartDate, bookingPostDto.EndDate);
                    return BadRequest("Det valgte rum er allerede booket i den angivne periode");
                }

                var booking = BookingMapping.ToBookingFromPostDto(bookingPostDto, room.PricePerNight);
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Ny booking oprettet succesfuldt med ID: {BookingId}", booking.Id);

                // Hent den oprettede booking med relaterede data
                var createdBooking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hotel)
                    .FirstOrDefaultAsync(b => b.Id == booking.Id);

                return CreatedAtAction("GetBooking", new { id = booking.Id }, BookingMapping.ToBookingGetDto(createdBooking!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved oprettelse af booking for bruger {UserId} i rum {RoomId}", 
                    bookingPostDto?.UserId, bookingPostDto?.RoomId);
                return StatusCode(500, "Der opstod en intern serverfejl ved oprettelse af booking");
            }
        }

        /// <summary>
        /// Sletter en booking fra systemet.
        /// </summary>
        /// <param name="id">ID på bookingen der skal slettes.</param>
        /// <returns>Bekræftelse på sletningen.</returns>
        /// <response code="204">Bookingen blev slettet succesfuldt.</response>
        /// <response code="401">Ikke autoriseret - manglende eller ugyldig token.</response>
        /// <response code="403">Forbudt - kan kun slette egne bookinger.</response>
        /// <response code="404">Booking med det angivne ID blev ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(string id)
        {
            try
            {
                _logger.LogInformation("Sletter booking med ID: {BookingId}", id);

                var booking = await _context.Bookings.FindAsync(id);
                if (booking == null)
                {
                    _logger.LogWarning("Booking med ID {BookingId} ikke fundet for sletning", id);
                    return NotFound();
                }

                // Tjek om brugeren har adgang til denne booking
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole("Admin");
                
                if (!isAdmin && booking.UserId != currentUserId)
                {
                    _logger.LogWarning("Bruger {UserId} forsøgte at slette booking {BookingId} som tilhører bruger {BookingUserId}", 
                        currentUserId, id, booking.UserId);
                    return Forbid();
                }

                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Booking med ID {BookingId} slettet succesfuldt", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved sletning af booking: {BookingId}", id);
                return StatusCode(500, "Der opstod en intern serverfejl ved sletning af booking");
            }
        }

        /// <summary>
        /// Søger efter ledige rum baseret på avancerede kriterier.
        /// </summary>
        /// <param name="query">Søgeparametre for rum tilgængelighed.</param>
        /// <returns>Liste af ledige rum der matcher kriterierne.</returns>
        /// <response code="200">Søgningen blev udført succesfuldt.</response>
        /// <response code="400">Ugyldig søgeforespørgsel.</response>
        /// <response code="404">Hotel ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [HttpPost("availability")]
        public async Task<ActionResult<IEnumerable<RoomAvailabilityResultDto>>> SearchAvailability(RoomAvailabilityQueryDto query)
        {
            try
            {
                _logger.LogInformation("Søger efter ledige rum for hotel {HotelId} fra {CheckIn} til {CheckOut} for {Guests} gæster", 
                    query.HotelId, query.CheckInDate, query.CheckOutDate, query.NumberOfGuests);

                // Valider datoer
                if (query.CheckOutDate <= query.CheckInDate)
                {
                    return BadRequest("Check-out dato skal være efter check-in dato");
                }

                if (query.CheckInDate < DateTime.UtcNow.Date)
                {
                    return BadRequest("Check-in dato kan ikke være i fortiden");
                }

                // Tjek om hotellet eksisterer
                var hotelExists = await _context.Hotels.AnyAsync(h => h.Id == query.HotelId);
                if (!hotelExists)
                {
                    _logger.LogWarning("Hotel med ID {HotelId} ikke fundet", query.HotelId);
                    return NotFound("Hotel ikke fundet");
                }

                // Byg kompleks query for rum
                var roomsQuery = _context.Rooms
                    .Include(r => r.Hotel)
                    .Include(r => r.Bookings)
                    .Where(r => r.HotelId == query.HotelId);

                // Filtrer baseret på grundlæggende kriterier
                roomsQuery = roomsQuery.Where(r => r.Capacity >= query.NumberOfGuests);

                // Anvend valgfrie filtre
                if (query.MinimumBeds.HasValue)
                    roomsQuery = roomsQuery.Where(r => r.NumberOfBeds >= query.MinimumBeds.Value);

                if (!string.IsNullOrEmpty(query.RoomType))
                    roomsQuery = roomsQuery.Where(r => r.RoomType.ToLower().Contains(query.RoomType.ToLower()));

                if (query.MaxPricePerNight.HasValue)
                    roomsQuery = roomsQuery.Where(r => r.PricePerNight <= query.MaxPricePerNight.Value);

                if (query.RequireBalcony == true)
                    roomsQuery = roomsQuery.Where(r => r.HasBalcony);

                if (query.RequireSeaView == true)
                    roomsQuery = roomsQuery.Where(r => r.HasSeaView);

                if (query.RequireWifi == true)
                    roomsQuery = roomsQuery.Where(r => r.HasWifi);

                if (query.RequireAirConditioning == true)
                    roomsQuery = roomsQuery.Where(r => r.HasAirConditioning);

                if (query.RequireMinibar == true)
                    roomsQuery = roomsQuery.Where(r => r.HasMinibar);

                if (query.RequireAccessible == true)
                    roomsQuery = roomsQuery.Where(r => r.IsAccessible);

                if (query.PreferredFloor.HasValue)
                    roomsQuery = roomsQuery.Where(r => r.FloorNumber == query.PreferredFloor.Value);

                if (query.MinimumSquareMeters.HasValue)
                    roomsQuery = roomsQuery.Where(r => r.SquareMeters >= query.MinimumSquareMeters.Value);

                var rooms = await roomsQuery.ToListAsync();

                // Tjek tilgængelighed for hvert rum
                var availabilityResults = new List<RoomAvailabilityResultDto>();
                var nights = (query.CheckOutDate - query.CheckInDate).Days;

                foreach (var room in rooms)
                {
                    // Tjek for overlappende bookinger
                    var hasOverlap = room.Bookings.Any(b => 
                        b.BookingStatus != "Cancelled" &&
                        b.StartDate < query.CheckOutDate && 
                        b.EndDate > query.CheckInDate);

                    var isAvailable = !hasOverlap;
                    var availabilityResult = RoomMapping.ToRoomAvailabilityResultDto(room, isAvailable, nights);
                    
                    availabilityResults.Add(availabilityResult);
                }

                // Sortér resultater - ledige rum først, derefter efter pris
                availabilityResults = availabilityResults
                    .OrderByDescending(r => r.IsAvailable)
                    .ThenBy(r => r.PricePerNight)
                    .ToList();

                _logger.LogInformation("Fundet {TotalRooms} rum, {AvailableRooms} ledige for hotel {HotelId}", 
                    availabilityResults.Count, 
                    availabilityResults.Count(r => r.IsAvailable), 
                    query.HotelId);

                return Ok(availabilityResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved søgning efter ledige rum for hotel {HotelId}", query?.HotelId);
                return StatusCode(500, "Der opstod en intern serverfejl ved søgning efter ledige rum");
            }
        }

        /// <summary>
        /// Henter detaljeret tilgængelighedsinfo for et specifikt rum i en given periode.
        /// </summary>
        /// <param name="roomId">Rum ID.</param>
        /// <param name="checkInDate">Check-in dato.</param>
        /// <param name="checkOutDate">Check-out dato.</param>
        /// <returns>Detaljeret tilgængelighedsinfo for rummet.</returns>
        /// <response code="200">Tilgængelighedsinfo hentet succesfuldt.</response>
        /// <response code="400">Ugyldig forespørgsel.</response>
        /// <response code="404">Rum ikke fundet.</response>
        /// <response code="500">Der opstod en intern serverfejl.</response>
        [HttpGet("room-availability/{roomId}")]
        public async Task<ActionResult<object>> GetRoomAvailability(string roomId, 
            [FromQuery] DateTime checkInDate, 
            [FromQuery] DateTime checkOutDate)
        {
            try
            {
                _logger.LogInformation("Henter tilgængelighed for rum {RoomId} fra {CheckIn} til {CheckOut}", 
                    roomId, checkInDate, checkOutDate);

                // Valider datoer
                if (checkOutDate <= checkInDate)
                {
                    return BadRequest("Check-out dato skal være efter check-in dato");
                }

                var room = await _context.Rooms
                    .Include(r => r.Hotel)
                    .Include(r => r.Bookings)
                    .FirstOrDefaultAsync(r => r.Id == roomId);

                if (room == null)
                {
                    _logger.LogWarning("Rum med ID {RoomId} ikke fundet", roomId);
                    return NotFound("Rum ikke fundet");
                }

                // Find overlappende bookinger
                var overlappingBookings = room.Bookings
                    .Where(b => b.BookingStatus != "Cancelled" &&
                               b.StartDate < checkOutDate && 
                               b.EndDate > checkInDate)
                    .Select(b => new
                    {
                        b.Id,
                        b.StartDate,
                        b.EndDate,
                        b.BookingStatus,
                        b.NumberOfGuests,
                        UserEmail = b.User?.Email
                    })
                    .ToList();

                var isAvailable = !overlappingBookings.Any();
                var nights = (checkOutDate - checkInDate).Days;

                var availabilityInfo = new
                {
                    RoomId = room.Id,
                    RoomNumber = room.Number,
                    RoomType = room.RoomType,
                    HotelName = room.Hotel?.Name ?? string.Empty,
                    CheckInDate = checkInDate,
                    CheckOutDate = checkOutDate,
                    Nights = nights,
                    IsAvailable = isAvailable,
                    PricePerNight = room.PricePerNight,
                    TotalPrice = room.PricePerNight * nights,
                    Capacity = room.Capacity,
                    NumberOfBeds = room.NumberOfBeds,
                    OverlappingBookings = overlappingBookings,
                    RoomFeatures = new
                    {
                        room.HasBalcony,
                        room.HasSeaView,
                        room.HasWifi,
                        room.HasAirConditioning,
                        room.HasMinibar,
                        room.IsAccessible,
                        room.FloorNumber,
                        room.SquareMeters,
                        room.Description
                    }
                };

                _logger.LogInformation("Tilgængelighed for rum {RoomId}: {IsAvailable}", roomId, isAvailable);
                return Ok(availabilityInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved hentning af tilgængelighed for rum {RoomId}", roomId);
                return StatusCode(500, "Der opstod en intern serverfejl ved hentning af rumtilgængelighed");
            }
        }

        /// <summary>
        /// Hjælpemetode til at kontrollere om en booking eksisterer.
        /// </summary>
        /// <param name="id">ID på bookingen der skal kontrolleres.</param>
        /// <returns>True hvis bookingen eksisterer, ellers false.</returns>
        private async Task<bool> BookingExists(string id)
        {
            return await _context.Bookings.AnyAsync(e => e.Id == id);
        }
    }
}
