using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed class BrowserImageLoader
{
    private static readonly HttpClient Shared = CreateClient();
    private static readonly SemaphoreSlim Workers = new(2);
    private static int _pending;
    private readonly HttpClient _client;
    public BrowserImageLoader(HttpClient? client = null) => _client = client ?? Shared;
    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false,
            MaxConnectionsPerServer = 2, PooledConnectionLifetime = TimeSpan.FromMinutes(1) };
        handler.ConnectCallback = async (context, cancellation) => {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellation);
            if (addresses.Length == 0 || addresses.Any(a => !ImageSourceHints.PublicAddress(a))) throw new HttpRequestException("Image address is not public.");
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellation); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); if (cancellation.IsCancellationRequested) throw; }
            }
            throw new HttpRequestException("Image server is unavailable.");
        };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
    public async Task<(byte[] Bytes, string Extension)> Load(Uri source, CancellationToken cancellation)
    {
        if (Interlocked.Increment(ref _pending) > 8) { Interlocked.Decrement(ref _pending); throw new InvalidOperationException("Image queue is full; link kept."); }
        try
        {
            return await Task.Run(async () => {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var token = timeout.Token;
                await Workers.WaitAsync(token);
                try
                {
                    var uri = source;
                    for (var redirects = 0; redirects <= 3; redirects++)
                    {
                        if (!ImageSourceHints.Allowed(uri) || IPAddress.TryParse(uri.DnsSafeHost, out var literal) && !ImageSourceHints.PublicAddress(literal))
                            throw new HttpRequestException("Only public image URLs are supported.");
                        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                        request.Headers.UserAgent.ParseAdd("ClipDrop/" + Diagnostics.Version);
                        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                        if ((int)response.StatusCode is >= 300 and <= 399 && response.Headers.Location is { } location)
                        { uri = location.IsAbsoluteUri ? location : new Uri(uri, location); continue; }
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentType?.MediaType is "text/html" or "application/xhtml+xml" or "image/svg+xml")
                            throw new InvalidDataException("The image address returned a page or unsupported vector content.");
                        if (response.Content.Headers.ContentLength > ImageFiles.MaximumBytes) throw new InvalidDataException("Image exceeds the 64 MB limit.");
                        using var input = await response.Content.ReadAsStreamAsync(token); using var output = new MemoryStream();
                        var buffer = new byte[81920]; int count;
                        while ((count = await input.ReadAsync(buffer, token)) != 0)
                        { if (output.Length + count > ImageFiles.MaximumBytes) throw new InvalidDataException("Image exceeds the 64 MB limit."); await output.WriteAsync(buffer.AsMemory(0, count), token); }
                        var bytes = output.ToArray(); ImageFiles.Load(bytes, 160); token.ThrowIfCancellationRequested();
                        return (bytes, ImageFiles.Extension(bytes));
                    }
                    throw new HttpRequestException("Too many image redirects.");
                }
                finally { Workers.Release(); }
            }, cancellation);
        }
        finally { Interlocked.Decrement(ref _pending); }
    }
}
