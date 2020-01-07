﻿
#if (NETCOREAPP3_1)

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RestClient.Net.Samples.Model;
using RestClient.Net.UnitTests.Model;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RestClient.Net.UnitTests
{
    [TestClass]
    public class PerformanceTests
    {
        [TestMethod]
        public async Task GetPerformanceTest()
        {
            var countryCodeClient = new Client(new NewtonsoftSerializationAdapter(), new Uri("https://restcountries.eu/rest/v2/"));

            List<RestCountry> countryData = null;

            var startTime = DateTime.Now;

            for (var i = 0; i < 15; i++)
                countryData = await countryCodeClient.GetAsync<List<RestCountry>>();

            var restClientTotalMilliseconds = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"RestClient Get : Total Milliseconds:{ restClientTotalMilliseconds}");


            startTime = DateTime.Now;
            var restSharpClient = new RestSharp.RestClient("https://restcountries.eu/rest/v2/");

            var request = new RestRequest(Method.GET)
            {
                Resource = "/country/get/all"
            };

            for (var i = 0; i < 15; i++)
            {
                var taskCompletionSource = new TaskCompletionSource<List<RestCountry>>();
                var response = restSharpClient.ExecuteAsync<List<RestCountry>>(request, (a) => { taskCompletionSource.SetResult(a.Data); });
                countryData = await taskCompletionSource.Task;
            }

            var restSharpTotalMilliseconds = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"RestSharp Get : Total Milliseconds:{ restSharpTotalMilliseconds}");

            Assert.IsTrue(restClientTotalMilliseconds < restSharpTotalMilliseconds, "😞 RestSharp wins.");

            Console.WriteLine("🏆 RestClient Wins!!!");
        }

        [TestMethod]
        public async Task PatchPerformanceTest()
        {

            var restClient = new Client(new NewtonsoftSerializationAdapter(), new Uri("https://jsonplaceholder.typicode.com"));

            UserPost userPost = null;

            var startTime = DateTime.Now;

            for (var i = 0; i < 15; i++)
                userPost = await restClient.PatchAsync<UserPost, UserPost>(new UserPost { title = "Moops" }, "/posts/1");

            var restClientTotalMilliseconds = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"RestClient Get : Total Milliseconds:{ restClientTotalMilliseconds}");


            startTime = DateTime.Now;
            var restSharpClient = new RestSharp.RestClient("https://jsonplaceholder.typicode.com");

            var request = new RestRequest(RestSharp.Method.PATCH)
            {
                Resource = "/posts/1"
            };

            for (var i = 0; i < 15; i++)
            {
                var taskCompletionSource = new TaskCompletionSource<UserPost>();
                var response = restSharpClient.ExecuteAsync<UserPost>(request, (a) => { taskCompletionSource.SetResult(a.Data); });
                userPost = await taskCompletionSource.Task;
            }

            var restSharpTotalMilliseconds = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"RestSharp Get : Total Milliseconds:{ restSharpTotalMilliseconds}");

            Assert.IsTrue(restClientTotalMilliseconds < restSharpTotalMilliseconds, "😞 RestSharp wins.");

            Console.WriteLine("🏆 RestClient Wins!!!");
        }

    }
}

#endif