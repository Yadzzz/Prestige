using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Server.Client.AI
{
    public class AiCommandResolverService
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;

        public AiCommandResolverService(string workerEndpoint, HttpClient httpClient = null!)
        {
            _endpoint = workerEndpoint.TrimEnd('/') + "/resolve-command";
            _http = httpClient ?? new HttpClient();
        }

        public async Task<AiCommandResolutionResult?> ResolveAsync(
            string message,
            IEnumerable<string> commands)
        {
            var payload = new
            {
                message,
                commands = commands.ToArray()
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.PostAsync(_endpoint, content);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync();

            try
            {
                return JsonSerializer.Deserialize<AiCommandResolutionResult>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }
    }
}
