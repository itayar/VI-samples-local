// This class takes care of Video Indexer API calls
//
//

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Web;
using System.Globalization;
using System.Collections.Specialized;

namespace VideoIndexerLibrary
{
  
    public class VideoIndexer
    {
        private string _accountId;
        private string _location;
        private string _subscriptionKey;
        private static HttpClientHandler handler = new HttpClientHandler() { AllowAutoRedirect = false };
        private static HttpClient client = new HttpClient(handler);
        private static NameValueCollection queryString = HttpUtility.ParseQueryString(string.Empty);

        private const string apiUrl = "https://api.videoindexer.ai";
        private Dictionary<string, VideoIndexerVideoToken> videoAccessTokens = new Dictionary<string, VideoIndexerVideoToken>();


        public VideoIndexer(string accountId, string location, string subscriptionkey)
        {
            _accountId = accountId;
            _location = location;
            _subscriptionKey = subscriptionkey;
        }


        public async Task<string> GetAccountAccessTokenAsync()
        {
            return await GetAccessTokenAsync($"{apiUrl}/auth/{_location}/Accounts/{_accountId}/AccessToken?allowEdit=true").ConfigureAwait(false);
        }


        public async Task<string> GetVideoAccessTokenAsync(string videoId)
        {
            if (videoAccessTokens.ContainsKey(videoId) && videoAccessTokens[videoId].expirationTime < DateTime.Now.AddMinutes(-5))
            {
                // there is already a video access token, let's use it
            }
            else
            {
                // no token or expired token
                string token = await GetAccessTokenAsync($"{apiUrl}/auth/{_location}/Accounts/{_accountId}/Videos/{videoId}/AccessToken?allowEdit=true").ConfigureAwait(false);
                videoAccessTokens[videoId] = new VideoIndexerVideoToken() { token = token, expirationTime = DateTime.Now.AddMinutes(60) }; // token is valid one hour
            }
            return videoAccessTokens[videoId].token;
        }

        
        public async Task<string> GetInsightsAsync(string videoId)
        {
            string videoAccessToken = await GetVideoAccessTokenAsync(videoId).ConfigureAwait(false);
            queryString.Clear();
            queryString["accessToken"] = videoAccessToken;
            Uri requestUri = new Uri($"{apiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoId}/Index?{queryString}");

            HttpResponseMessage insightsRequestResult = await client.GetAsync(requestUri).ConfigureAwait(false);

            if (!insightsRequestResult.IsSuccessStatusCode)
            {
                throw new Exception(insightsRequestResult.ReasonPhrase);
            }

            return await insightsRequestResult.Content.ReadAsStringAsync().ConfigureAwait(false);
        }


        public async Task<Stream> GetVideoThumbnailAsync(string videoId, string thumbnailId)
        {
            string videoAccessToken = await GetVideoAccessTokenAsync(videoId).ConfigureAwait(false);
            queryString.Clear();
            queryString["accessToken"] = videoAccessToken;
            queryString["format"] = "Jpeg";
            Uri requestUri = new Uri($"{apiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoId}/Thumbnails/{thumbnailId}?{queryString}");

            HttpResponseMessage thumbnailRequestResult = await client.GetAsync(requestUri).ConfigureAwait(false);

            if (!thumbnailRequestResult.IsSuccessStatusCode)
            {
                throw new Exception(thumbnailRequestResult.ReasonPhrase);
            }

            return await thumbnailRequestResult.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }


        public async Task<Uri> GetPlayerWidgetAsync(string videoId)
        {
            queryString.Clear();
            return await GetWidgetAsync(videoId, "PlayerWidget", queryString).ConfigureAwait(false);
        }


        public async Task<Uri> GetVideoInsightsWidgetAsync(string videoId, bool allowEdit)
        {
            queryString.Clear();
            queryString["allowEdit"] = allowEdit.ToString(CultureInfo.InvariantCulture);

            return await GetWidgetAsync(videoId, "InsightsWidget", queryString).ConfigureAwait(false);
        }

        private async Task<Uri> GetWidgetAsync(string videoId, string widgetAPIstr, NameValueCollection queryString)
        {
            string videoAccessToken = await GetVideoAccessTokenAsync(videoId).ConfigureAwait(false);
            queryString["accessToken"] = videoAccessToken;

            Uri requestUri = new Uri($"{apiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoId}/{widgetAPIstr}?{queryString}");

            HttpResponseMessage insightsRequestResult = await client.GetAsync(requestUri).ConfigureAwait(false);

            if (insightsRequestResult.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
            {
                return insightsRequestResult.Headers.Location;
            }
            else
            {
                throw new Exception(insightsRequestResult.ReasonPhrase);
            }
        }

        private async Task<string> GetAccessTokenAsync(string requestUrl)
        {
            // Request headers
            client.DefaultRequestHeaders.Add("x-ms-client-request-id", "");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);

            var requestResult = await client.GetAsync(new Uri(requestUrl)).ConfigureAwait(false);

            if (!requestResult.IsSuccessStatusCode)
            {
                throw new Exception(requestResult.ReasonPhrase);
            }
            client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

            return (await requestResult.Content.ReadAsStringAsync().ConfigureAwait(false)).Replace("\"", "");
        }
    }
}