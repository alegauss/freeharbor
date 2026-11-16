using FreeHarbor.Core.Preflight;

namespace FreeHarbor.Core.Fixtures;

/// <summary>
/// A machine that has never had WSL2, for a picture of the first-run page (DD316, L6).
/// </summary>
/// <remarks>
/// The state that page exists for, and the one a development machine cannot produce: everything
/// else is green, so the WSL2 row is the one blocker and its remedy is the command the page offers.
/// </remarks>
public sealed class SampleMachineFacts : IMachineFacts
{
    /// <inheritdoc/>
    public Version OperatingSystem { get; } = new(10, 0, 26100, 0);

    /// <inheritdoc/>
    public bool? VirtualizationFirmwareEnabled => true;

    /// <inheritdoc/>
    public bool? HypervisorPresent => false;

    /// <inheritdoc/>
    public bool? IsVirtualMachine => false;

    /// <inheritdoc/>
    public WslInstallation Wsl { get; } = new() { CommandPresent = true, FeatureInstalled = false };

    /// <inheritdoc/>
    public IReadOnlyList<RivalEngine> RivalEngines { get; } = [];

    /// <inheritdoc/>
    public DockerClientTarget DockerClient { get; } = new()
    {
        ContextName = "default",
        Host = "npipe:////./pipe/docker_engine",
    };

    /// <inheritdoc/>
    public EngineRegistration? EngineDistribution => null;
}
