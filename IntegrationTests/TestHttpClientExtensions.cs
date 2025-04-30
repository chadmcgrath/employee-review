using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EmployeeReview.IntegrationTests.Helpers
{
    public static class HttpClientExtensions
    {
        public static Task<HttpResponseMessage> PostAsJsonAsync<T>(
            this HttpClient httpClient, string url, T data)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(data),
                Encoding.UTF8,
                "application/json");

            return httpClient.PostAsync(url, content);
        }

        public static Task<HttpResponseMessage> PutAsJsonAsync<T>(
            this HttpClient httpClient, string url, T data)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(data),
                Encoding.UTF8,
                "application/json");

            return httpClient.PutAsync(url, content);
        }

        public static async Task<T> ReadAsJsonAsync<T>(this HttpContent content)
        {
            var dataAsString = await content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(dataAsString);
        }
    }
}