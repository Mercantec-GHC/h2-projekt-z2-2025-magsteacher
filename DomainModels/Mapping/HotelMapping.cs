using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels.Mapping
{
    public class HotelMapping
    {
        public HotelGetDto ToHotelWithBookingsGetDto(Hotel hotel)
        {
            return new HotelGetDto
            {
                Id = hotel.Id,
                Name = hotel.Name,
                Address = hotel.Address               
            };
        }

        public Hotel ToHotelFromDto(HotelPostDto hotelPostDto)
        {
            return new Hotel
            {
                Id = Guid.NewGuid().ToString(),
                Name = hotelPostDto.Name,
                Address = hotelPostDto.Address,
                CreatedAt = DateTime.UtcNow.AddHours(2),
                UpdatedAt = DateTime.UtcNow.AddHours(2)
            };
        }
    }
}
