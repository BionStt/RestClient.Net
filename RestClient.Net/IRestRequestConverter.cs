﻿using RestClientDotNet.Abstractions;
using System.Net.Http;

namespace RestClientDotNet
{
    public interface IRestRequestConverter
    {
        HttpRequestMessage GetHttpRequestMessage<TRequestBody>(Request<TRequestBody> restRequest, byte[] requestBodyData);
    }
}
