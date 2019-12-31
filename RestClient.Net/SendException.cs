﻿using RestClientDotNet.Abstractions;
using System;

namespace RestClientDotNet
{
    [Serializable]
    public class SendException<TRequestBody> : Exception
    {
        public Request<TRequestBody> RestRequest { get; }

        public SendException(string message, Request<TRequestBody> restRequest, Exception ex) : base(message, ex)
        {
            RestRequest = restRequest;
        }
    }
}