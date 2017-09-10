// Copyright (c) Just Eat, 2017. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace JustEat.HttpClientInterception
{
    /// <summary>
    /// This class contains tests which provide example scenarios for using the library.
    /// </summary>
    public static class Examples
    {
        [Fact]
        public static async Task Intercept_Http_Get_For_Json_Object()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForGet()
                .ForHttps()
                .ForHost("public.je-apis.com")
                .ForPath("terms")
                .WithJsonContent(new { Id = 1, Link = "https://www.just-eat.co.uk/privacy-policy" });

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            string json;

            using (var client = options.CreateHttpClient())
            {
                // Act
                json = await client.GetStringAsync("https://public.je-apis.com/terms");
            }

            // Assert
            var content = JObject.Parse(json);
            content.Value<int>("Id").ShouldBe(1);
            content.Value<string>("Link").ShouldBe("https://www.just-eat.co.uk/privacy-policy");
        }

        [Fact]
        public static async Task Intercept_Http_Get_For_Html_String()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForHost("www.google.co.uk")
                .ForPath("search")
                .ForQuery("q=Just+Eat")
                .WithMediaType("text/html")
                .WithContent(@"<!DOCTYPE html><html dir=""ltr"" lang=""en""><head><title>Just Eat</title></head></html>");

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            string html;

            using (var client = options.CreateHttpClient())
            {
                // Act
                html = await client.GetStringAsync("http://www.google.co.uk/search?q=Just+Eat");
            }

            // Assert
            html.ShouldContain("Just Eat");
        }

        [Fact]
        public static async Task Intercept_Http_Get_For_Raw_Bytes()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForHttps()
                .ForHost("files.domain.com")
                .ForPath("setup.exe")
                .WithMediaType("application/octet-stream")
                .WithContent(() => new byte[] { 0, 1, 2, 3, 4 });

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            byte[] content;

            using (var client = options.CreateHttpClient())
            {
                // Act
                content = await client.GetByteArrayAsync("https://files.domain.com/setup.exe");
            }

            // Assert
            content.ShouldBe(new byte[] { 0, 1, 2, 3, 4 });
        }

        [Fact]
        public static async Task Intercept_Http_Post_For_Json_String()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForPost()
                .ForHttps()
                .ForHost("public.je-apis.com")
                .ForPath("consumer")
                .WithStatus(HttpStatusCode.Created)
                .WithContent(@"{ ""id"": 123 }");

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            HttpStatusCode status;
            string json;

            using (var client = options.CreateHttpClient())
            {
                using (var body = new StringContent(@"{ ""FirstName"": ""John"" }"))
                {
                    // Act
                    using (var response = await client.PostAsync("https://public.je-apis.com/consumer", body))
                    {
                        status = response.StatusCode;
                        json = await response.Content.ReadAsStringAsync();
                    }
                }
            }

            // Assert
            status.ShouldBe(HttpStatusCode.Created);

            var content = JObject.Parse(json);
            content.Value<int>("id").ShouldBe(123);
        }

        [Fact]
        public static async Task Intercept_Custom_Http_Method()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForMethod(new HttpMethod("custom"))
                .ForHost("custom.domain.com")
                .ForQuery("length=2")
                .WithContent(() => new byte[] { 0, 1 });

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            byte[] content;

            using (var client = options.CreateHttpClient())
            {
                using (var message = new HttpRequestMessage(new HttpMethod("custom"), "http://custom.domain.com?length=2"))
                {
                    // Act
                    using (var response = await client.SendAsync(message))
                    {
                        content = await response.Content.ReadAsByteArrayAsync();
                    }
                }
            }

            // Assert
            content.ShouldBe(new byte[] { 0, 1 });
        }

        [Fact]
        public static async Task Inject_Fault_For_Http_Get()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForHost("www.google.co.uk")
                .WithStatus(HttpStatusCode.InternalServerError);

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            using (var client = options.CreateHttpClient())
            {
                // Act and Assert
                await Should.ThrowAsync<HttpRequestException>(() => client.GetStringAsync("http://www.google.co.uk"));
            }
        }

        [Fact]
        public static async Task Inject_Latency_For_Http_Get()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForHost("www.google.co.uk");

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            options.OnSend = (_) => Task.Delay(TimeSpan.FromMilliseconds(50));

            var stopwatch = new Stopwatch();

            using (var client = options.CreateHttpClient())
            {
                stopwatch.Start();

                // Act
                await client.GetStringAsync("http://www.google.co.uk");

                stopwatch.Stop();
            }

            // Assert
            stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public static async Task Intercept_Http_Get_To_Stream_Content_From_Disk()
        {
            // Arrange
            var builder = new HttpRequestInterceptionBuilder()
                .ForHost("xunit.github.io")
                .ForPath("settings.json")
                .WithContent(() => File.ReadAllBytesAsync("xunit.runner.json"));

            var options = new HttpClientInterceptorOptions()
                .Register(builder);

            string json;

            using (var client = options.CreateHttpClient())
            {
                // Act
                json = await client.GetStringAsync("http://xunit.github.io/settings.json");
            }

            // Assert
            json.ShouldNotBeNullOrWhiteSpace();

            var config = JObject.Parse(json);
            config.Value<string>("methodDisplay").ShouldBe("method");
        }
    }
}
