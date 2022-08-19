using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI
{
    public class GithubClient
    {
        private readonly HttpClient _httpClient;
        private readonly OctoLogger _log;

        public GithubClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider, string personalAccessToken)
        {
            _log = log;
            _httpClient = httpClient;

            if (_httpClient != null)
            {
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                _httpClient.DefaultRequestHeaders.Add("GraphQL-Features", "import_api,mannequin_claiming");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", versionProvider?.GetCurrentVersion()));
                if (versionProvider?.GetVersionComments() is { } comments)
                {
                    _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
                }
            }
        }

        public virtual async Task<string> GetNonSuccessAsync(string url, HttpStatusCode status) => (await SendAsync(HttpMethod.Get, url, status: status)).Content;

        public virtual async Task<string> GetAsync(string url) => (await SendAsync(HttpMethod.Get, url)).Content;
        
        public virtual async Task<string> GetAsyncWithAcceptHeader(string url, string accept) => (await SendAsync(HttpMethod.Get, url, accept)).Content;

        public virtual async IAsyncEnumerable<JToken> GetAllAsync(string url)
        {
            var nextUrl = url;
            do
            {
                var (content, headers) = await SendAsync(HttpMethod.Get, nextUrl);
                foreach (var jToken in JArray.Parse(content))
                {
                    yield return jToken;
                }

                nextUrl = GetNextUrl(headers);
            } while (nextUrl != null);
        }

        public virtual async Task<string> PostAsync(string url, object body) =>
            (await SendAsync(HttpMethod.Post, url, body)).Content;

        public virtual async IAsyncEnumerable<JToken> PostGraphQLWithPaginationAsync(
            string url,
            object body,
            Func<JObject, JArray> resultCollectionSelector,
            Func<JObject, JObject> pageInfoSelector,
            int first = 100,
            string after = null)
        {
            if (resultCollectionSelector is null)
            {
                throw new ArgumentNullException(nameof(resultCollectionSelector));
            }

            if (pageInfoSelector is null)
            {
                throw new ArgumentNullException(nameof(pageInfoSelector));
            }

            var jBody = JObject.FromObject(body);
            jBody["variables"] ??= new JObject();
            jBody["variables"]["first"] = first;

            var hasNextPage = true;
            while (hasNextPage)
            {
                jBody["variables"]["after"] = after;

                var (content, _) = await SendAsync(HttpMethod.Post, url, jBody);
                var jContent = JObject.Parse(content);
                foreach (var jResult in resultCollectionSelector(jContent))
                {
                    yield return jResult;
                }

                var pageInfo = pageInfoSelector(jContent);
                if (pageInfo is null)
                {
                    yield break;
                }

                hasNextPage = pageInfo["hasNextPage"]?.ToObject<bool>() ?? false;
                after = pageInfo["endCursor"]?.ToObject<string>();
            }
        }

        public virtual async Task<string> PutAsync(string url, object body) =>
            (await SendAsync(HttpMethod.Put, url, body)).Content;

        public virtual async Task<string> PatchAsync(string url, object body) =>
            (await SendAsync(HttpMethod.Patch, url, body)).Content;

        public virtual async Task<string> DeleteAsync(string url) => (await SendAsync(HttpMethod.Delete, url)).Content;

        private async Task<(string Content, KeyValuePair<string, IEnumerable<string>>[] ResponseHeaders)> SendAsync(
            HttpMethod httpMethod, string url, object body = null, HttpStatusCode status = HttpStatusCode.OK, string acceptHeader = null)
        {
            url = url?.Replace(" ", "%20");
            _log.LogVerbose($"HTTP {httpMethod}: {url}");

            using var request = new HttpRequestMessage(httpMethod, new Uri(url));
            
            if (acceptHeader != null)
            {
                request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(acceptHeader));    
            }

            if (body != null)
            {
                _log.LogVerbose($"HTTP BODY: {body.ToJson()}");
                
                var payload = body.ToJson();
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            }
            
            using var response = await _httpClient.SendAsync(request);
            _log.LogVerbose($"GITHUB REQUEST ID: {ExtractHeaderValue("X-GitHub-Request-Id", response.Headers)}");
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");

            if (status == HttpStatusCode.OK)
            {
                response.EnsureSuccessStatusCode();
            }
            else if (response.StatusCode != status)
            {
                throw new HttpRequestException($"Expected status code {status} but got {response.StatusCode}", null, response.StatusCode);
            }

            return (content, response.Headers.ToArray());
        }

        private string GetNextUrl(KeyValuePair<string, IEnumerable<string>>[] headers)
        {
            var linkHeaderValue = ExtractLinkHeader(headers);

            var nextUrl = linkHeaderValue?
                .Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(link =>
                {
                    var rx = new Regex(@"<(?<url>.+)>;\s*rel=""(?<rel>.+)""");
                    var url = rx.Match(link).Groups["url"].Value;
                    var rel = rx.Match(link).Groups["rel"].Value; // first, next, last, prev

                    return (Url: url, Rel: rel);
                })
                .FirstOrDefault(x => x.Rel == "next").Url;

            return nextUrl;
        }

        private string ExtractLinkHeader(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
            ExtractHeaderValue("Link", headers);

        private string ExtractHeaderValue(string key, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
            headers.SingleOrDefault(kvp => kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
    }
}
