using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        /// <summary>
        /// Tjekker om API'en kører korrekt.
        /// </summary>
        /// <returns>Status og besked om API'ens tilstand.</returns>
        /// <response code="200">API'en er kørende.</response>
        [HttpGet("healthcheck")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "OK", message = "API'en er kørende!" });
        }

        /// <summary>
        /// Tjekker om databasen er tilgængelig (dummy indtil EFCore er sat op).
        /// </summary>
        /// <returns>Status og besked om databaseforbindelse.</returns>
        /// <response code="200">Database er kørende eller fejlbesked gives.</response>
    
        [HttpGet("dbhealthcheck")]
        public IActionResult DBHealthCheck()
        {
            // Indtil vi har opsat EFCore, returnerer vi bare en besked

            try {
                // using (var context = new ApplicationDbContext())
                // {
                //     context.Database.CanConnect();
                // }
                throw new Exception("I har endnu ikke lært at opsætte EFCore! Det kommer senere!");
            }
            catch (Exception ex)
            {
                return Ok(new { status = "Error", message = "Fejl ved forbindelse til database: " + ex.Message });
            }
            return Ok(new { status = "OK", message = "Database er kørende!" });
        }

        /// <summary>
        /// Simpelt ping-endpoint til at teste API'en.
        /// </summary>
        /// <returns>Status og "Pong" besked.</returns>
        /// <response code="200">API'en svarede med Pong.</response>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "OK", message = "Pong 🏓" });
        }
    }
}
