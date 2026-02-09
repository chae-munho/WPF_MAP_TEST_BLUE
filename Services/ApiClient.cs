using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Map.Models;

namespace Map.Services
{
    public sealed class ApiClient
    {
        // 앱 전체 공용 HttpClient 1개
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        private readonly Uri _baseUri;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiClient(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl is required.", nameof(baseUrl));

            _baseUri = new Uri(baseUrl.TrimEnd('/'));
        }

        // APIs 
        public async Task<DataResponse?> GetDataAsync(CancellationToken ct = default)
        {
            var url = new Uri(_baseUri, "/api/getdata");
            using var res = await _http.GetAsync(url, ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<DataResponse>(json, _jsonOptions);
        }

        public async Task PostSetDataAsync(int operation, int value, int train, CancellationToken ct = default)
        {
            var url = new Uri(_baseUri, "/api/setdata");

            var req = new SetDataRequest
            {
                operation = operation,
                value = value,
                train = train
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
        }

        public async Task<GpsResponse?> GetNextPosAsync(CancellationToken ct = default)
        {
            var url = new Uri(_baseUri, "/api/nextpos");
            using var res = await _http.GetAsync(url, ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GpsResponse>(json, _jsonOptions);
        }
    }
}
