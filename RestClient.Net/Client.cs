﻿
#if NET45
using RestClient.Net.Abstractions.Logging;
#else
using Microsoft.Extensions.Logging;
#endif

using RestClient.Net.Abstractions;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

#pragma warning disable CA2000

namespace RestClient.Net
{

    /// <summary>
    /// Rest client implementation using Microsoft's HttpClient class. 
    /// </summary>
    public sealed class Client : IClient
    {
        #region Public Properties

        /// <summary>
        /// Compresses and decompresses http requests 
        /// </summary>
        public IZip Zip { get; set; }

        /// <summary>
        /// Default headers to be sent with http requests
        /// </summary>
        public IHeadersCollection DefaultRequestHeaders { get; }

        /// <summary>
        /// Default timeout for http requests
        /// </summary>
        public TimeSpan Timeout { get => RequestConverter.Timeout; set => RequestConverter.Timeout = value; }

        /// <summary>
        /// Adapter for serialization/deserialization of http body data
        /// </summary>
        public ISerializationAdapter SerializationAdapter { get; }

        /// <summary>
        /// Logging abstraction that will trace request/response data and log events
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Specifies whether or not the client will throw an exception when non-successful status codes are returned in the http response. The default is true
        /// </summary>
        public bool ThrowExceptionOnFailure { get; set; } = true;

        /// <summary>
        /// Base Uri for the client. Any resources specified on requests will be relative to this.
        /// </summary>
        public Uri BaseUri { get => RequestConverter.BaseUri; set => RequestConverter.BaseUri = value; }

        /// <summary>
        /// Name of the client
        /// </summary>
        public string Name => RequestConverter.Name;

        /// <summary>
        /// Gets the current IRequestConverter instance responsible for converting rest requests to http requests
        /// </summary>
        public IRequestConverter RequestConverter { get; }
        #endregion

        #region Constructors
        /// <summary>
        /// Construct a client
        /// </summary>
        /// <param name="serializationAdapter">The serialization adapter for serializing/deserializing http content bodies</param>
        public Client(
            ISerializationAdapter serializationAdapter)
        : this(
            serializationAdapter,
            default)
        {
        }

        /// <summary>
        /// Construct a client
        /// </summary>
        /// <param name="serializationAdapter">The serialization adapter for serializing/deserializing http content bodies</param>
        /// <param name="baseUri">The base Url for the client. Specify this if the client will be used for one Url only</param>
        public Client(
            ISerializationAdapter serializationAdapter,
            Uri baseUri)
        : this(
            serializationAdapter,
            null,
            baseUri)
        {
        }

        /// <summary>
        /// Construct a client.
        /// </summary>
        /// <param name="serializationAdapter">The serialization adapter for serializing/deserializing http content bodies</param>
        /// <param name="logger"></param>
        /// <param name="httpClientFactory"></param>
        public Client(
            ISerializationAdapter serializationAdapter,
            ILogger logger,
            IHttpClientFactory httpClientFactory)
        : this(
            serializationAdapter,
            null,
            null,
            logger: logger,
            httpClientFactory: httpClientFactory)
        {
        }

        /// <summary>
        /// Construct a client.
        /// </summary>
        /// <param name="serializationAdapter">The serialization adapter for serializing/deserializing http content bodies</param>
        /// <param name="name">The of the client instance. This is also passed to the HttpClient factory to get or create HttpClient instances</param>
        /// <param name="baseUri">The base Url for the client. Specify this if the client will be used for one Url only</param>
        /// <param name="defaultRequestHeaders">Default headers to be sent with http requests</param>
        /// <param name="logger">Logging abstraction that will trace request/response data and log events</param>
        /// <param name="httpClientFactory">The IHttpClientFactory instance that is used for getting or creating HttpClient instances when the SendAsync call is made</param>
        /// <param name="sendHttpRequestFunc">The Func responsible for performing the SendAsync method on HttpClient. This can replaced in the constructor in order to implement retries and so on.</param>
        /// <param name="requestConverter">IRequestConverter instance responsible for converting rest requests to http requests</param>
        public Client(
            ISerializationAdapter serializationAdapter,
            string name = null,
            Uri baseUri = null,
            IHeadersCollection defaultRequestHeaders = null,
            ILogger logger = null,
            IHttpClientFactory httpClientFactory = null,
            IRequestConverter requestConverter = null)
        {
            var defaultRequestConverter = new DefaultRequestConverter(httpClientFactory ?? new DefaultHttpClientFactory(), name);
            RequestConverter = requestConverter ?? defaultRequestConverter;
            SerializationAdapter = serializationAdapter ?? throw new ArgumentNullException(nameof(serializationAdapter));
            Logger = logger;
            BaseUri = baseUri;
            DefaultRequestHeaders = defaultRequestHeaders ?? new RequestHeadersCollection();
        }

        #endregion

        #region Implementation
        public async Task<Response<TResponseBody>> SendAsync<TResponseBody, TRequestBody>(Request<TRequestBody> request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            byte[] requestBodyData = null;

            if (DefaultRequestConverter.UpdateHttpRequestMethods.Contains(request.HttpRequestMethod))
            {
                requestBodyData = SerializationAdapter.Serialize(request.Body, request.Headers);
            }

            HttpResponseMessage httpResponseMessage;
            try
            {
                httpResponseMessage = await RequestConverter.SendAsync(request, RequestConverter, requestBodyData);
            }
            catch (TaskCanceledException tce)
            {
                Log(LogLevel.Error, null, tce);
                throw;
            }
            catch (OperationCanceledException oce)
            {
                Log(LogLevel.Error, null, oce);
                throw;
            }
            //TODO: Does this need to be handled like this?
            catch (Exception ex)
            {
                var exception = new SendException<TRequestBody>("HttpClient Send Exception", request, ex);
                Log(LogLevel.Error, null, exception);
                throw exception;
            }

            Log(LogLevel.Trace, new Trace
                (
                 request.HttpRequestMethod,
                 httpResponseMessage.RequestMessage.RequestUri,
                 requestBodyData,
                 TraceEvent.Request,
                 null,
                 request.Headers
                ), null);

            return await ProcessResponseAsync<TResponseBody, TRequestBody>(request, httpResponseMessage);
        }

        private async Task<Response<TResponseBody>> ProcessResponseAsync<TResponseBody, TRequestBody>(Request<TRequestBody> request, HttpResponseMessage httpResponseMessage)
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

            var httpResponseHeadersCollection = new HttpResponseHeadersCollection(httpResponseMessage.Headers);

            TResponseBody responseBody;
            try
            {
                responseBody = SerializationAdapter.Deserialize<TResponseBody>(responseData, httpResponseHeadersCollection);
            }
            catch (Exception ex)
            {
                throw new DeserializationException(Messages.ErrorMessageDeserialization, responseData, this, ex);
            }

            var httpResponseMessageResponse = new HttpResponseMessageResponse<TResponseBody>
            (
                httpResponseHeadersCollection,
                (int)httpResponseMessage.StatusCode,
                request.HttpRequestMethod,
                responseData,
                responseBody,
                httpResponseMessage
            );

            Log(LogLevel.Trace, new Trace
            (
             request.HttpRequestMethod,
             httpResponseMessage.RequestMessage.RequestUri,
             responseData,
             TraceEvent.Response,
             (int)httpResponseMessage.StatusCode,
             httpResponseHeadersCollection
            ), null);

            if (httpResponseMessageResponse.IsSuccess || !ThrowExceptionOnFailure)
            {
                return httpResponseMessageResponse;
            }

            throw new HttpStatusException($"Non successful Http Status Code: {httpResponseMessageResponse.StatusCode}.\r\nRequest Uri: {httpResponseMessage.RequestMessage.RequestUri}", httpResponseMessageResponse, this);
        }
        #endregion

        #region Private Methods
        private void Log(LogLevel loglevel, Trace restTrace, Exception exception)
        {
            Logger?.Log(loglevel,
                restTrace != null ?
                new EventId((int)restTrace.RestEvent, restTrace.RestEvent.ToString()) :
                new EventId((int)TraceEvent.Error, TraceEvent.Error.ToString()),
                restTrace, exception, null);
        }
        #endregion
    }
}

#pragma warning restore CA2000