﻿using System;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using CompatApiClient.Compression;
using CompatApiClient.POCOs;
using CompatApiClient.Utils;
using Newtonsoft.Json;

namespace CompatApiClient
{
    public class Client: IDisposable
    {
        private readonly HttpClient client;
        private readonly MediaTypeFormatterCollection formatters;

        public Client()
        {
            client = HttpClientFactory.Create(new CompressionMessageHandler());
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new JsonContractResolver(NamingStyles.Underscore),
                NullValueHandling = NullValueHandling.Ignore
            };
            formatters = new MediaTypeFormatterCollection(new[] {new JsonMediaTypeFormatter {SerializerSettings = settings}});
        }

        //todo: cache results
        public async Task<CompatResult> GetCompatResultAsync(RequestBuilder requestBuilder, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var url = requestBuilder.Build();
            var tries = 0;
            do
            {
                try
                {
                    using (var message = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                        try
                        {
                            await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
                            var result = await response.Content.ReadAsAsync<CompatResult>(formatters, cancellationToken).ConfigureAwait(false);
                            result.RequestBuilder = requestBuilder;
                            result.RequestDuration = DateTime.UtcNow - startTime;
                            return result;
                        }
                        catch (Exception e)
                        {
                            ConsoleLogger.PrintError(e, response, false);
                        }
                }
                catch (Exception e)
                {
                    ApiConfig.Log.Warn(e);
                }
                tries++;
            } while (tries < 3);
            throw new HttpRequestException("Couldn't communicate with the API");
        }

        public async Task<UpdateInfo> GetUpdateAsync(CancellationToken cancellationToken, string commit = null)
        {
            if (string.IsNullOrEmpty(commit))
                commit = "somecommit";
            var tries = 3;
            do
            {
                try
                {
                    using (var message = new HttpRequestMessage(HttpMethod.Get, "https://update.rpcs3.net/?c=" + commit))
                    using (var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                        try
                        {
                            return await response.Content.ReadAsAsync<UpdateInfo>(formatters, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            ConsoleLogger.PrintError(e, response, false);
                        }
                }
                catch (Exception e)
                {
                    ApiConfig.Log.Warn(e);
                }
                tries++;
            } while (tries < 3);
            return null;
        }

        public void Dispose()
        {
            client?.Dispose();
        }
    }
}