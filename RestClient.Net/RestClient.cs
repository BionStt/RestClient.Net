﻿using RestClientDotNet.Abstractions;
using System;

namespace RestClientDotNet
{
    public class RestClient : RestClientBase, IRestClient
    {
        public RestClient(ISerializationAdapter serializationAdapter) : this(serializationAdapter, null)
        {
        }

        public RestClient(ISerializationAdapter serializationAdapter, Uri baseUri) : this(serializationAdapter, baseUri, default(ITracer))
        {
        }

        public RestClient(ISerializationAdapter serializationAdapter, Uri baseUri, ITracer tracer) : this(serializationAdapter, baseUri, default, tracer, new SingletonHttpClientFactory(default, baseUri), null)
        {
        }

        public RestClient(ISerializationAdapter serializationAdapter, Uri baseUri, TimeSpan timeout) : this(serializationAdapter, baseUri, timeout, null)
        {
        }

        public RestClient(ISerializationAdapter serializationAdapter, Uri baseUri, TimeSpan timeout, IHttpClientFactory httpClientFactory) : this(serializationAdapter, baseUri, timeout, null, httpClientFactory, null)
        {
        }

        public RestClient(ISerializationAdapter serializationAdapter, Uri baseUri, TimeSpan timeout, IHttpClientFactory httpClientFactory, ITracer tracer) : this(serializationAdapter, baseUri, timeout, tracer, httpClientFactory, null)
        {
        }

        public RestClient(ISerializationAdapter serializationAdapter, Uri baseUri, TimeSpan timeout, ITracer tracer, IHttpClientFactory httpClientFactory, IZip zip) : base(serializationAdapter, baseUri, timeout, new ResponseProcessorFactory(serializationAdapter, tracer, httpClientFactory, zip), tracer)
        {
        }
    }
}
