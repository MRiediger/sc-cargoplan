using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CargoPlan.App.Tests;

/// <summary>The network and the clock of the hologram tests (D63): nothing leaves the machine.</summary>
internal static class FakeHttp
{
    /// <summary>A manifest.json of holo-v1 listing these files, with their real length and hash.</summary>
    public static byte[] Manifest(params (string Slug, byte[] Data)[] ships) => Encoding.UTF8.GetBytes($$"""
        { "dataVersion": "holo-v1", "formatVersion": 2, "ships": [ {{string.Join(", ", ships.Select(s =>
            $$"""{ "slug": "{{s.Slug}}", "file": "{{s.Slug}}.holo", "bytes": {{s.Data.Length}}, "sha256": "{{Convert.ToHexStringLower(SHA256.HashData(s.Data))}}", "budget": 20000 }"""))}} ] }
        """);

    public sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

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
