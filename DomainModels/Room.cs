using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels
{
    // Room.cs
    public class Room : Common
    {
        public string Number { get; set; } = string.Empty;
        public int Capacity { get; set; }

        public string HotelId { get; set; } = string.Empty;
        public Hotel? Hotel { get; set; }
        public List<Booking> Bookings { get; set; } = new();
    }
}
