using DomainModels;
using System.Net.Http.Json;
using System.Text.Json;

namespace Blazor.Services
{
    public partial class APIService
    {
        // User authentication methods
        public async Task<string?> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/users/login", loginDto);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return result; // JWT token
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved login: " + ex.Message);
                return null;
            }
        }

        public async Task<bool> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/users/register", registerDto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved registrering: " + ex.Message);
                return false;
            }
        }

        public async Task<UserGetDto[]?> GetUsersAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<UserGetDto[]>("api/users");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved hentning af brugere: " + ex.Message);
                return null;
            }
        }

        public async Task<UserGetDto?> GetUserAsync(string id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<UserGetDto>($"api/users/{id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fejl ved hentning af bruger {id}: " + ex.Message);
                return null;
            }
        }

        public async Task<UserGetDto?> GetCurrentUserAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<UserGetDto>("api/users/me");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl ved hentning af nuværende bruger: " + ex.Message);
                return null;
            }
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/users/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fejl ved sletning af bruger {id}: " + ex.Message);
                return false;
            }
        }

        // Logout method (client-side token removal)
        public void Logout()
        {
            // This would typically clear the JWT token from storage
            // Implementation depends on how you store the token
        }
    }
}
