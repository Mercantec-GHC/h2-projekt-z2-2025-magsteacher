using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels
{
    // Booking.cs
    public class Booking : Common
    {
        public string UserId { get; set; } = string.Empty;
        public User? User { get; set; }

        public string RoomId { get; set; } = string.Empty;
        public Room? Room { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
