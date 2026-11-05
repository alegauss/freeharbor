using System.Globalization;
using System.Text.RegularExpressions;

namespace FreeHarbor.Core.Engine;

/// <summary>
/// The oldest Engine API version the daemon answers, and the one setting that lowers it (DD288).
/// </summary>
/// <remarks>
/// <b>Moby's floor, moved by Moby's own switch.</b> Docker 29 refuses every client below API 1.40,
/// and Testcontainers' bundled docker-java opens at 1.32, so each of its suites reads the engine as
/// absent and skips. dockerd takes its minimum from <see cref="Variable"/>, which the shell that
/// starts it can export without touching the engine, so the non-goal against forking it holds.
///
/// <para><b>Lowering only, from 1.24.</b> Measured on 5 November 2026 with
/// <c>dockerd --validate</c> on 29.7.2: 1.24 to 1.55 is accepted, 1.10 and 1.60 are refused, and a
/// refused value is a daemon that does not start. The upper bound here is <see cref="MobyDefault"/>
/// and not the engine's own maximum, because that maximum moves with every engine update and a value
/// valid today would stop the engine after one. Raising the floor is not what anybody asks for.</para>
///
/// <para>Checked again where it is used. The file is a user's, and a hand edit that wrote 1.10 must
/// cost the setting, not the engine.</para>
/// </remarks>
public static partial class EngineApiFloor
{
    /// <summary>The environment variable dockerd reads its minimum from.</summary>
    public const string Variable = "DOCKER_MIN_API_VERSION";

    /// <summary>The lowest minimum dockerd will start with.</summary>
    public const string Lowest = "1.24";

    /// <summary>The minimum the pinned engine applies when nothing is set.</summary>
    public const string MobyDefault = "1.40";

    private const int LowestMinor = 24;
    private const int DefaultMinor = 40;

    /// <summary>Read a version a user typed.</summary>
    /// <param name="text">What they typed, such as <c>1.24</c>.</param>
    /// <param name="version">The version as dockerd should be given it.</param>
    /// <returns>
    /// Whether it is a version this setting accepts: <c>1.N</c>, from <see cref="Lowest"/> to
    /// <see cref="MobyDefault"/>.
    /// </returns>
    public static bool TryRead(string? text, out string version)
    {
        version = "";
        var match = Shape().Match(text?.Trim() ?? "");
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out var minor)
            || minor is < LowestMinor or > DefaultMinor)
        {
            return false;
        }

        version = $"1.{minor}";
        return true;
    }

    /// <summary>What the launch command says before it execs the daemon.</summary>
    /// <param name="minimum">The stored setting, or <see langword="null"/> for Moby's own.</param>
    /// <returns>
    /// An <c>export</c> and a separator, or the empty string where the setting is absent or is not
    /// one <see cref="TryRead"/> accepts.
    /// </returns>
    /// <remarks>
    /// An <c>export</c> of its own and not an assignment in front of <c>exec</c>: POSIX leaves it to
    /// the shell whether an assignment before a special builtin is exported, and an export reaches
    /// the daemon whichever shell <c>/bin/sh</c> is.
    /// </remarks>
    public static string ShellPrefix(string? minimum) =>
        TryRead(minimum, out var version) ? $"export {Variable}={version}; " : "";

    [GeneratedRegex(@"^1\.(\d{1,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
