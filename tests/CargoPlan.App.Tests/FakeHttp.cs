using System.Net;
using System.Security.Cryptography;
using CargoPlan.Core.Holo;
using CargoPlan.Core.ShipData;

namespace CargoPlan.App.Tests;

/// <summary>The network and the ship data of the hologram tests (D63, D114): nothing leaves the machine.</summary>
internal static class FakeHttp
{
    /// <summary>The ship data in use in the hologram tests; its release holds the holograms.</summary>
    public static readonly ShipDataVersion DataVersion = new(2026, 11, 1);

    /// <summary>The holo section of <see cref="DataVersion"/>'s manifest listing these files, with their real length and hash.</summary>
    public static HoloOffer Holo(params (string Slug, byte[] Data)[] ships) => new HoloOffer.Offered(new HoloManifest(
        DataVersion.ToString(),
        [.. ships.OrderBy(s => s.Slug, StringComparer.Ordinal)
            .Select(s => new HoloManifestEntry(s.Slug, s.Slug + ".holo", s.Data.Length, Convert.ToHexStringLower(SHA256.HashData(s.Data))))]));

    /// <summary>The data in use with <paramref name="holo"/>; the folders are never read by the hologram code.</summary>
    public static ActiveShipData Data(HoloOffer holo) => new(
        DataVersion, ShipDataOrigin.Baseline, @"C:\app\ship-data", @"C:\app\ship-data\ships", @"C:\app\ship-data\outlines.json", holo);

    /// <summary>Answers per file name; logs every request. Unknown files get a 404.</summary>
    public sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _answers = [];

        public List<(string Uri, string Method, string UserAgent)> Requests { get; } = [];

        /// <summary>Requests that <see cref="Hang"/> held until the caller cancelled them.</summary>
        public int Cancelled { get; private set; }

        public void Serve(string file, byte[] data) => Answer(file, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });

        public void Answer(string file, Func<HttpResponseMessage> answer) => _answers[file] = _ => Task.FromResult(answer());

        public void Hang(string file) => _answers[file] = async token =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                Cancelled++;
                throw;
            }

            throw new InvalidOperationException("unreachable");
        };

        public void Fail(string file) => _answers[file] = _ => throw new HttpRequestException("No such host is known.");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((uri, request.Method.Method, request.Headers.UserAgent.ToString()));
            return _answers.TryGetValue(uri[(uri.LastIndexOf('/') + 1)..], out var answer)
                ? answer(cancellationToken)
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
