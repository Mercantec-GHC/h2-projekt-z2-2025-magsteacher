using DomainModels;
using System.Net.Http.Json;

namespace Blazor.Services
{
    public partial class APIService
    {
        public async Task<BookingGetDto[]?> GetBookingsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<BookingGetDto[]>("api/bookings");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved hentning af bookinger: " + ex.Message);
                return null;
            }
        }

        public async Task<BookingGetDto?> GetBookingAsync(string id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<BookingGetDto>($"api/bookings/{id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fejl ved hentning af booking {id}: " + ex.Message);
                return null;
            }
        }

        public async Task<BookingGetDto[]?> GetMyBookingsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<BookingGetDto[]>("api/bookings/mine");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved hentning af mine bookinger: " + ex.Message);
                return null;
            }
        }

        public async Task<BookingGetDto[]?> GetBookingsByRoomAsync(string roomId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<BookingGetDto[]>($"api/bookings/room/{roomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fejl ved hentning af bookinger for rum {roomId}: " + ex.Message);
                return null;
            }
        }

        public async Task<BookingGetDto?> CreateBookingAsync(BookingPostDto bookingDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/bookings", bookingDto);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<BookingGetDto>();
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved oprettelse af booking: " + ex.Message);
                return null;
            }
        }

        public async Task<bool> UpdateBookingAsync(string id, BookingPutDto bookingDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/bookings/{id}", bookingDto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fejl ved opdatering af booking {id}: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> DeleteBookingAsync(string id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/bookings/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fejl ved sletning af booking {id}: " + ex.Message);
                return false;
            }
        }

        // Room availability search
        public async Task<RoomAvailabilityResultDto[]?> SearchRoomAvailabilityAsync(RoomAvailabilityQueryDto query)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/bookings/availability", query);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<RoomAvailabilityResultDto[]>();
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved søgning efter ledige rum: " + ex.Message);
                return null;
            }
        }

        // Get specific room availability
        public async Task<object?> GetRoomAvailabilityAsync(string roomId, DateTime checkInDate, DateTime checkOutDate)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<object>(
                    $"api/bookings/room-availability/{roomId}?checkInDate={checkInDate:yyyy-MM-dd}&checkOutDate={checkOutDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fejl ved hentning af tilgængelighed for rum {roomId}: " + ex.Message);
                return null;
            }
        }
    }
}
