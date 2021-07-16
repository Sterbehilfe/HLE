﻿using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HLE.HttpRequests
{
    /// <summary>
    /// A class that performs a Http POST request on creation of the object.
    /// </summary>
    public class HttpPost
    {
        /// <summary>
        /// The URL of the request.
        /// </summary>
        public string URL { get; }

        /// <summary>
        /// The complete answer as a string.
        /// </summary>
        public string Result { get; }

        /// <summary>
        /// The header content that will be sent to the URL.
        /// </summary>
        public HttpContent HeaderContent { get; }

        /// <summary>
        /// The answer stored in a <see cref="JsonElement"/>, if the answer was a json compatible string.
        /// </summary>
        public JsonElement Data { get; }

        /// <summary>
        /// True, if the answer was a json compatible string, otherwise false.
        /// </summary>
        public bool ValidJsonData { get; } = true;

        private readonly HttpClient _httpClient = new();

        /// <summary>
        /// The main constructor of <see cref="HttpPost"/>.<br />
        /// The request will be executed in the constructor.
        /// </summary>
        /// <param name="url">The URL to which the request will be send to.</param>
        /// <param name="headers">The header content that will be sent to the URL.</param>
        public HttpPost(string url, List<KeyValuePair<string, string>> headers)
        {
            URL = url;
            HeaderContent = new FormUrlEncodedContent(headers);
            Result = PostRequest().Result;
            try
            {
                Data = JsonSerializer.Deserialize<JsonElement>(Result);
            }
            catch (JsonException)
            {
                ValidJsonData = false;
            }
        }

        private async Task<string> PostRequest()
        {
            HttpResponseMessage response = await _httpClient.PostAsync(URL, HeaderContent);
            return await response.Content.ReadAsStringAsync();
        }
    }
}