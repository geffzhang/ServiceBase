﻿namespace ServiceBase
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    // https://msdn.microsoft.com/de-de/library/dn589788.aspx
    /* Example
     *
     * using (var client = new HttpClient(new RetryHttpClientHandler(3)))
     * {
     *      // Client will try 3 times to make a POST request until a success
     *      // code is returned
     *      await client.PostAsync("http://domain.com/foo", content);
     * }
     *
     * FYI: http://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
     */

    public class RetryHttpClientHandler : DelegatingHandler
    {
        private int maxRetries;

        public RetryHttpClientHandler(int maxRetries = 3)
            : this(new HttpClientHandler(), maxRetries)
        {
        }

        public RetryHttpClientHandler(
            HttpMessageHandler innerHandler,
            int maxRetries = 3)
            : base(innerHandler)
        {
            this.maxRetries = maxRetries;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < this.maxRetries; i++)
            {
                HttpResponseMessage response = await base.SendAsync(
                    request,
                    cancellationToken
                );

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
            }

            return null;
        }
    }
}