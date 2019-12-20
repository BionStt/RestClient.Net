﻿using RestClientApiSamples;
using RestClientDotNet;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace RESTClient.NET.CoreSample
{
    internal class Program
    {
        #region Main Method
        private static void Main(string[] args)
        {
            Go();
            Console.ReadLine();
        }
        #endregion

        #region Methods
        private static async Task Go()
        {
            //var asdasd = await new HttpClient().GetByteArrayAsync("http://localhost:42908/person");

            var restClient = new RestClient(new ProtobufSerializationAdapter(), new Uri("http://localhost:42908/person"));
            var person = await restClient.GetAsync<Person>();

            Console.WriteLine($"Got person {person.FirstName}");
            Console.ReadLine();
        }

        #endregion
    }
}
