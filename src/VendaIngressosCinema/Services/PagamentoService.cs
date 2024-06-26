using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VendaIngressosCinema.Services
{
    public class PagamentoRequest
    {
        [JsonPropertyName("cpf")]
        public string Cpf { get; set; }
        
        [JsonPropertyName("nome")]
        public string Nome { get; set; }
        
        [JsonPropertyName("cartaoCredito")]
        public string CartaoCredito { get; set; }
        
        [JsonPropertyName("valorCompra")]
        public decimal ValorCompra {get;set;}
    }

    public class PagamentoResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
    public class PagamentoService
    {
        private readonly HttpClient _httpClient;

        public PagamentoService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<PagamentoResponse> EfetuarPagamento(PagamentoRequest request)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "api/v1/pagamento/efetuar");
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PagamentoResponse>();

        }
    }
}