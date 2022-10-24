﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GammaLibrary.Enhancements;
using GammaLibrary.Extensions;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using WFBot.Features.Resource;
using WFBot.TextCommandCore;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace WFBot.Utils
{
    public class WebStatus
    {
        public WebStatus(bool isOnline, long latency)
        {
            IsOnline = isOnline;
            Latency = latency;
        }

        public Boolean IsOnline { get; set; }
        public long Latency { get; set; }
    }

    public class Container<T>
    {
        public Container(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
    }

    public static class WebHelper
    {
        public static async Task<Image<Rgba32>> LoadImageFromWeb(string url)
        {
            var sw = Stopwatch.StartNew();
            var client = SharedHttpClient.Value;
            Directory.CreateDirectory("WFCaches/WebImageCache");
            var filePath = "WFCaches/WebImageCache/" + url.SHA256().ToHexString();
            if (File.Exists(filePath))
            {
                return Image.Load(filePath, new PngDecoder()) as Image<Rgba32>;
            }

            await using var bytes = await client.GetStreamAsync(url);
            // Remember to dispose of this image once you are finished.
            var fs = File.OpenWrite(filePath);
            bytes.CopyTo(fs);
            fs.Close();
            var image = Image.Load(filePath, new PngDecoder());
            Trace.WriteLine($"图片下载完成: URL [{url}], 用时 {sw.Elapsed.TotalSeconds:F1}s.");
            return image as Image<Rgba32>;
        }
        public static async Task<WebStatus> TryGet(string url)
        {
            try
            {
                var client = new HttpClient(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestVersion = new Version(2, 0);
                client.Timeout = TimeSpan.FromSeconds(0xF);
                var sw = Stopwatch.StartNew();
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return new WebStatus(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized, sw.ElapsedMilliseconds);
            }
            catch (Exception)
            {
                //
            }

            return new WebStatus(false, int.MaxValue);
        }
        
        static readonly Lazy<HttpClient> SharedHttpClient = new Lazy<HttpClient>(() =>
        {
            var dHandler =
                new RetryHandler(new HttpClientHandler {AutomaticDecompression = DecompressionMethods.Brotli});
            
            var hc = new HttpClient(dHandler);
            hc.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            hc.Timeout = TimeSpan.FromSeconds(30);
            return hc;
        });

        public static async Task<string> DownloadStringAsync(string url, List<KeyValuePair<string, string>> header = null)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var hc = SharedHttpClient.Value;
                var msg = new HttpRequestMessage(HttpMethod.Get, url);

                if (header != null)
                {
                    foreach (var (key, value) in header)
                    {
                        msg.Headers.Add(key, value);
                    }
                }

                try
                {
                    var data = await hc.SendAsync(msg, AsyncContext.GetCancellationToken());
                    data.EnsureSuccessStatusCode();

                    return await data.Content.ReadAsStringAsync();
                }
                catch (WebException e)
                {
                    if (e.Status == WebExceptionStatus.Timeout)
                    {
                        throw new CommandException($"请求超时.");
                    }

                    throw new CommandException($"数据下载出错: {e.Message}.");
                }
            }
            finally
            {
                Trace.WriteLine($"数据下载完成: URL [{url}], 用时 {sw.Elapsed.TotalSeconds:F1}s.");
            }
        } 
        public static async Task<T> DownloadJsonAsync<T>(string url, List<KeyValuePair<string, string>> header = null)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var hc = SharedHttpClient.Value;
                var msg = new HttpRequestMessage(HttpMethod.Get, url);

                if (header != null)
                {
                    foreach (var (key, value) in header)
                    {
                        msg.Headers.Add(key, value);
                    }
                }

                try
                {
                    var data = await hc.SendAsync(msg, AsyncContext.GetCancellationToken());
                    data.EnsureSuccessStatusCode();

                    return await ResourceLoaders<T>.JsonDotNetLoader(await data.Content.ReadAsStreamAsync());
                }
                catch (WebException e)
                {
                    if (e.Status == WebExceptionStatus.Timeout)
                    {
                        throw new CommandException($"请求超时.");
                    }

                    throw new CommandException($"数据下载出错: {e.Message}.");
                }
            }
            finally
            {
                Trace.WriteLine($"数据下载完成: URL [{url}], 用时 {sw.Elapsed.TotalSeconds:F1}s.");
            }
        }
    }
}
