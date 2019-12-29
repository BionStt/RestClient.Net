﻿using RestClientDotNet.Abstractions;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA2000

namespace RestClientDotNet
{

    public sealed class RestClient : IRestClient
    {
        #region Public Properties
        public IHttpClientFactory HttpClientFactory { get; }
        public IZip Zip { get; set; }
        public IRestHeaders DefaultRequestHeaders { get; }
        public TimeSpan Timeout { get; set; }
        public ISerializationAdapter SerializationAdapter { get; }
        public ITracer Tracer { get; }
        public bool ThrowExceptionOnFailure { get; set; } = true;
        public Uri BaseUri { get; }
        public string Name { get; }
        public IHttpRequestProcessor HttpRequestProcessor { get; }
        public Func<HttpClient, Func<HttpRequestMessage>, CancellationToken, Task<HttpResponseMessage>> SendHttpRequestFunc { get; }
        #endregion

        #region Func
        private static readonly Func<HttpClient, Func<HttpRequestMessage>, CancellationToken, Task<HttpResponseMessage>> DefaultSendHttpRequestMessageFunc = (httpClient, httpRequestMessageFunc, cancellationToken) =>
        {
            var httpRequestMessage = httpRequestMessageFunc.Invoke();
            return httpClient.SendAsync(httpRequestMessage, cancellationToken);
        };
        #endregion

        #region Constructors
        public RestClient(
            ISerializationAdapter serializationAdapter)
        : this(
            serializationAdapter,
            default(Uri))
        {
        }

        public RestClient(
        ISerializationAdapter serializationAdapter,
        Uri baseUri)
        : this(
            serializationAdapter,
            baseUri,
            null)
        {
        }

        public RestClient(
            ISerializationAdapter serializationAdapter,
            Uri baseUri,
            TimeSpan timeout)
        : this(
              serializationAdapter,
              new DefaultHttpClientFactory(),
              null,
              baseUri,
              timeout,
              null,
              null)
        {
        }

        public RestClient(
            ISerializationAdapter serializationAdapter,
            Uri baseUri,
            ITracer tracer)
        : this(
          serializationAdapter,
          new DefaultHttpClientFactory(),
          tracer,
          baseUri,
          default,
          null,
          null)
        {
        }

        public RestClient(
            ISerializationAdapter serializationAdapter,
            IHttpClientFactory httpClientFactory)
        : this(
          serializationAdapter,
          httpClientFactory,
          null,
          null,
          default,
          null,
          null)
        {
        }

        public RestClient(
            ISerializationAdapter serializationAdapter,
            IHttpClientFactory httpClientFactory,
            ITracer tracer,
            Uri baseUri,
            TimeSpan timeout,
            string name,
            IHttpRequestProcessor httpRequestProcessor)
            : this(serializationAdapter, httpClientFactory, tracer, baseUri, timeout, name, httpRequestProcessor, null)
        {
        }

        public RestClient(
        ISerializationAdapter serializationAdapter,
        IHttpClientFactory httpClientFactory,
        ITracer tracer,
        Uri baseUri,
        TimeSpan timeout,
        string name,
        IHttpRequestProcessor httpRequestProcessor,
        Func<HttpClient, Func<HttpRequestMessage>, CancellationToken, Task<HttpResponseMessage>> sendHttpRequestFunc)
        {
            SerializationAdapter = serializationAdapter;
            Tracer = tracer;
            BaseUri = baseUri;
            Timeout = timeout;
            DefaultRequestHeaders = new RestRequestHeaders();
            Name = name ?? nameof(RestClient);
            HttpRequestProcessor = httpRequestProcessor ?? new DefaultHttpRequestProcessor();
            HttpClientFactory = httpClientFactory ?? new DefaultHttpClientFactory();
            SendHttpRequestFunc = sendHttpRequestFunc ?? DefaultSendHttpRequestMessageFunc;
        }

        #endregion

        #region Implementation
        async Task<RestResponseBase<TResponseBody>> IRestClient.SendAsync<TResponseBody, TRequestBody>(RestRequest<TRequestBody> restRequest)
        {
            var httpClient = HttpClientFactory.CreateClient(Name);

            //Note: if HttpClient naming is not handled properly, this may alter the HttpClient of another RestClient
            if (httpClient.Timeout != Timeout && Timeout != default) httpClient.Timeout = Timeout;
            if (BaseUri != null) httpClient.BaseAddress = BaseUri;

            byte[] requestBodyData = null;

            if (DefaultHttpRequestProcessor.UpdateVerbs.Contains(restRequest.HttpVerb))
            {
                requestBodyData = SerializationAdapter.Serialize(restRequest.Body, restRequest.Headers);
            }

            var httpResponseMessage = await SendHttpRequestFunc.Invoke(
                httpClient,
                () => HttpRequestProcessor.GetHttpRequestMessage(restRequest, requestBodyData),
                restRequest.CancellationToken
                );

            Tracer?.Trace(restRequest.HttpVerb, httpResponseMessage.RequestMessage.RequestUri, requestBodyData, TraceType.Request, null, restRequest.Headers);

            return await ProcessResponseAsync<TResponseBody, TRequestBody>(restRequest, httpClient, httpResponseMessage);
        }

        private async Task<RestResponseBase<TResponseBody>> ProcessResponseAsync<TResponseBody, TRequestBody>(RestRequest<TRequestBody> restRequest, HttpClient httpClient, HttpResponseMessage httpResponseMessage)
        {
            byte[] responseData = null;

            if (Zip != null)
            {
                //This is for cases where an unzipping utility needs to be used to unzip the content. This is actually a bug in UWP
                var gzipHeader = httpResponseMessage.Content.Headers.ContentEncoding.FirstOrDefault(h =>
                    !string.IsNullOrEmpty(h) && h.Equals("gzip", StringComparison.OrdinalIgnoreCase));
                if (gzipHeader != null)
                {
                    var bytes = await httpResponseMessage.Content.ReadAsByteArrayAsync();
                    responseData = Zip.Unzip(bytes);
                }
            }

            if (responseData == null)
            {
                responseData = await httpResponseMessage.Content.ReadAsByteArrayAsync();
            }

            var restResponse = await HttpRequestProcessor.GetRestResponseAsync<TResponseBody>(httpResponseMessage);

            Tracer?.Trace(
                httpResponseMessage.RequestMessage.Method.ToHttpVerb(),
                httpResponseMessage.RequestMessage.RequestUri,
                responseData,
                TraceType.Response,
                (int)httpResponseMessage.StatusCode,
                restHeadersCollection);

            if (restResponse.IsSuccess || !ThrowExceptionOnFailure)
            {
                return restResponse;
            }

            throw new HttpStatusException($"A status code of {restResponse.StatusCode} was returned.\r\nRequest Uri: {httpResponseMessage.RequestMessage.RequestUri}", restResponse, this);

        }




        #endregion
    }
}

#pragma warning restore CA2000