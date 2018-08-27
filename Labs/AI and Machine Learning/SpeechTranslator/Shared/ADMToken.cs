using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.MT.Api.TestUtils
{
    /// <summary>
    /// Client to call ADM ACS in order to get an access token.
    /// </summary>
    public class ADMToken
    {
        private const string ADM_OAUTH_REQUEST = "grant_type=client_credentials&client_id={0}&client_secret={1}&scope={2}";
        private const string ADM_TOKEN_PREFIX = "Bearer {0}";

        private Uri tokenEndpointBaseUrl;
        private string scope;

        /// <summary>
        /// Creates a client to obtain an access token.
        /// </summary>
        /// <param name="tokenEndpointBaseUrl">ADM ACS endpoint</param>
        /// <param name="scope">Requested scope</param>
        public ADMToken(string tokenEndpointBaseUrl, string scope)
        {
            this.tokenEndpointBaseUrl = new Uri(tokenEndpointBaseUrl);
            this.scope = scope;
        }

        public async Task<string> GetToken(string clientId, string clientSecret) //Receives the clientID and Secret
        {
            string token = await GetTokenWithoutPrefix(clientId, clientSecret);
            return string.Format(CultureInfo.InvariantCulture, ADM_TOKEN_PREFIX, token);
        }

        public async Task<string> GetTokenWithoutPrefix(string clientId, string clientSecret)
        {
            string request = string.Format(CultureInfo.InvariantCulture, ADM_OAUTH_REQUEST, HttpUtility.UrlEncode(clientId), HttpUtility.UrlEncode(clientSecret), this.scope);
            MTAccessToken mtToken = await GetAccessTokenAsync(this.tokenEndpointBaseUrl, request);
            if (mtToken == null)
            {
                throw new Exception("Received an empty access token from ACS.");
            }
            return mtToken.access_token;
        }

        /// <summary>
        /// Get the OAuth Access Token
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="contents"></param>
        /// <returns></returns>
        private async Task<MTAccessToken> GetAccessTokenAsync(Uri uri, string contents)
        {
            using (HttpClient client = new HttpClient())
            {
                StringContent postContent = new StringContent(contents, Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpResponseMessage response = await client.PostAsync(uri, postContent);
                MTAccessToken token = JsonConvert.DeserializeObject<MTAccessToken>(await response.Content.ReadAsStringAsync());
                return token;
            }
        }

        /// <summary>
        /// ACS Access Token
        /// </summary>
        [DataContract]
        private class MTAccessToken
        {
            [DataMember]
            public string access_token { get; set; }
            [DataMember]
            public string token_type { get; set; }
            [DataMember]
            public string expires_in { get; set; }
            [DataMember]
            public string scope { get; set; }
        }


    }
}
