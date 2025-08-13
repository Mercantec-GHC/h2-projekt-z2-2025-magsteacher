using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels
{
    // Hotel.cs
    public class Hotel : Common
    {
        public required string Name { get; set; }
        public string Address { get; set; } = "";

        public List<Room> Rooms { get; set; } = new(); // 1:n
    }

    // DTO for hotel creation / POST
    public class HotelPostDto
    {
        public required string Name { get; set; }
        public string Address { get; set; } = "";
    }

    // DTO for hotel retrieval / GET
    public class HotelGetDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string Address { get; set; } = "";

    }

    // DTO for hotel update / PUT
    public class HotelPutDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string Address { get; set; } = "";
    }
}