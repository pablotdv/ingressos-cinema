using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VendaIngressosCinema.Services
{
    public class AntifraudeRequest
    {
        [JsonPropertyName("cpf")]
        public string Cpf { get; set; }
        [JsonPropertyName("nome")]
        public string Nome { get; set; }
        [JsonPropertyName("dataNascimento")]
        public string DataNascimento { get; set; }
        [JsonPropertyName("email")]
        public string Email { get; set; }
        [JsonPropertyName("cartaoCredito")]
        public string CartaoCredito { get; set; }
    }

    public class AntifraudeResponse 
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
    public class AntifraudeService
    {
        private readonly HttpClient _httpClient;

        public AntifraudeService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AntifraudeResponse> ValidarIngresso(AntifraudeRequest request)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "api/v1/antifraude/validar");
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AntifraudeResponse>();

        }
    }
}