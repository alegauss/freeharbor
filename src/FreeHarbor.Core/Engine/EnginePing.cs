using System.IO.Pipes;
using System.Text;
using FreeHarbor.Core.Preflight.Windows;

namespace FreeHarbor.Core.Engine;

/// <summary>What asking the pipe produced.</summary>
/// <param name="Answered">Whether the daemon replied with an HTTP success.</param>
/// <param name="ApiVersion">The Engine API version it reported, when it replied.</param>
/// <param name="Detail">What happened, for a report.</param>
public sealed record PingResult(bool Answered, string? ApiVersion, string Detail);

/// <summary>
/// Asks the engine whether it is there, by talking to it.
/// </summary>
/// <remarks>
/// Deliberately not "does the pipe exist". A pipe exists the moment the relay creates its listener,
/// which is before the daemon has finished booting — so existence is exactly the lie this project
/// is trying not to tell. What counts is a reply.
/// </remarks>
public static class EnginePing
{
    /// <summary>The smallest request the Engine API answers.</summary>
    private const string Request = "GET /_ping HTTP/1.1\r\nHost: docker\r\nConnection: close\r\n\r\n";

    /// <summary>
    /// What a ping says when it never got a handle on the pipe at all (DD173, DD180).
    /// </summary>
    /// <remarks>
    /// A constant rather than a literal because two places now depend on the wording: this class
    /// writes it, and <see cref="EngineWatch.WhenItWentQuiet"/> reads it to decide whether the
    /// relay's own figures belong in the sentence. That is the coupling
    /// <see cref="EngineRevival.RestartMark"/> exists for and is spelled the same way — nothing fails
    /// to compile when a sentence is reworded, the second reader simply stops matching, and the
    /// journal quietly loses the half of the line that made it worth reading.
    /// </remarks>
    public const string NoConnection = "no connection";

    /// <summary>Ask the engine on <paramref name="pipeName"/> whether it is answering.</summary>
    /// <param name="pipeName">The pipe to ask.</param>
    /// <param name="timeout">How long to wait for the connection and the reply.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>What it said.</returns>
    public static async Task<PingResult> AskAsync(
        string pipeName = EnginePipeRelay.DefaultPipeName,
        TimeSpan? timeout = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        var budget = timeout ?? TimeSpan.FromSeconds(3);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(budget);

        // What ran out of budget, in the words the caller will read (DD173). One catch covers a
        // connect, a write and a read, and every one of them used to come back saying "no answer" —
        // which is true of the read and a guess about the other two. They are not the same failure:
        // a connection nothing accepted is the relay and its wsl.exe hop, the load story DD133 is
        // about, while a connection that opened and then went quiet is the daemon behind it. The
        // two send a reader to opposite ends of the machine, so the sentence has to pick one.
        var whatRanOut = NoConnection;

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            whatRanOut = "the request did not go";

            // ASCII and no encoder preamble. A UTF-8 writer puts a byte-order mark in front of the
            // request line, and the daemon answers that with 400 — measured, and confusing enough
            // to be worth spelling out here.
            var bytes = Encoding.ASCII.GetBytes(Request);
            await pipe.WriteAsync(bytes, deadline.Token).ConfigureAwait(false);
            await pipe.FlushAsync(deadline.Token).ConfigureAwait(false);
            whatRanOut = "no answer";

            var buffer = new byte[ReplyCap];
            var read = await pipe.ReadAsync(buffer, deadline.Token).ConfigureAwait(false);
            if (read == 0)
            {
                return new PingResult(false, null, "the pipe accepted a connection and said nothing");
            }

            if (!IsHttp(buffer.AsSpan(0, read)))
            {
                // Not the daemon, so whoever it is gets to finish its sentence (DD343). The one in
                // practice is the relay's wsl.exe writing its own refusal, and a first read can hold
                // a single character of that; it exits once it has said it, so this ends at EOF.
                read += await ReadRestAsync(pipe, buffer, read, deadline.Token).ConfigureAwait(false);
            }

            return Interpret(buffer.AsSpan(0, read).ToArray());
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return new PingResult(false, null,
                $"{whatRanOut} within {budget.TotalSeconds:0.#}s");
        }
        catch (Exception exception) when (exception is IOException
            or TimeoutException or UnauthorizedAccessException or ObjectDisposedException)
        {
            return new PingResult(false, null, exception.Message);
        }
    }

    /// <summary>The most of a reply a ping reads. A status line and its headers fit many times over.</summary>
    private const int ReplyCap = 4096;

    /// <summary>The most of a non-HTTP answer a report carries.</summary>
    private const int QuoteCap = 300;

    /// <summary>Whether a reply begins the way every HTTP response does.</summary>
    /// <param name="head">The first bytes read.</param>
    /// <returns>Whether it could be the daemon.</returns>
    /// <remarks>A prefix of "HTTP/" counts, because a first read may stop anywhere.</remarks>
    private static bool IsHttp(ReadOnlySpan<byte> head)
    {
        ReadOnlySpan<byte> http = "HTTP/"u8;
        var length = Math.Min(head.Length, http.Length);
        return head[..length].SequenceEqual(http[..length]);
    }

    /// <summary>Read into the rest of <paramref name="buffer"/> until EOF, a full buffer or the deadline.</summary>
    /// <returns>How many more bytes arrived.</returns>
    private static async Task<int> ReadRestAsync(
        Stream pipe, byte[] buffer, int offset, CancellationToken deadline)
    {
        var more = 0;
        try
        {
            while (offset + more < buffer.Length)
            {
                var read = await pipe.ReadAsync(buffer.AsMemory(offset + more), deadline)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                more += read;
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            // What already arrived is the report. Running out of budget here must not turn a
            // sentence that has started into "no answer".
        }

        return more;
    }

    /// <summary>Read the raw bytes the pipe answered with.</summary>
    /// <param name="reply">Everything the ping read.</param>
    /// <returns>The verdict.</returns>
    /// <remarks>
    /// Bytes rather than text, because the encoding is part of the finding (DD343). The daemon
    /// speaks ASCII HTTP. Anything else is a voice in its place, and the one this project has met is
    /// wsl.exe, which writes its own refusal in UTF-16LE: read as ASCII that kept every other byte a
    /// NUL, and the journal printed the first letter and lost the rest at the first zero.
    /// </remarks>
    internal static PingResult Interpret(byte[] reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        if (IsHttp(reply))
        {
            return Interpret(Encoding.ASCII.GetString(reply));
        }

        var fromWsl = ConsoleTool.EncodingOf(reply) == Encoding.Unicode;
        var said = Quote(ConsoleTool.Decode(reply));
        return new PingResult(false, null, fromWsl
            ? $"wsl.exe answered in the daemon's place: {said}"
            : $"the pipe answered with something that is not HTTP: {said}");
    }

    /// <summary>Read a status line and the API version out of a reply.</summary>
    /// <param name="reply">What the daemon wrote.</param>
    /// <returns>The verdict.</returns>
    internal static PingResult Interpret(string reply) => InterpretHttp(reply);

    /// <summary>One line of printable text, short enough for a journal line.</summary>
    private static string Quote(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        if (builder.Length == 0)
        {
            return "(nothing printable)";
        }

        return builder.Length <= QuoteCap ? builder.ToString() : builder.ToString(0, QuoteCap) + "…";
    }

    /// <summary>The status line and API version of an HTTP reply.</summary>
    private static PingResult InterpretHttp(string reply)
    {
        var lines = reply.Split('\n');
        var status = lines.Length > 0 ? lines[0].Trim() : "";
        var version = lines
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("Api-Version:", StringComparison.OrdinalIgnoreCase))
            ?.Split(':', 2)[1].Trim();

        // Any 2xx is the daemon answering. A 400 is too — it proves something is there — but it
        // means this code sent it something wrong, and calling that "running" would hide a defect.
        var answered = status.Contains(" 200", StringComparison.Ordinal)
            || status.Contains(" 204", StringComparison.Ordinal);

        return new PingResult(answered, version,
            answered ? status : $"the daemon replied: {status}");
    }
}
