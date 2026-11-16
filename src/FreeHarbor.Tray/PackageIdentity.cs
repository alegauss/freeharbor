using System.Runtime.InteropServices;

namespace FreeHarbor.Tray;

/// <summary>
/// Whether this process runs inside an MSIX package, which is the Microsoft Store build (DD311).
/// </summary>
/// <remarks>
/// <para>Ported from claude-tray's, which passed Store certification. Asked of the process at
/// runtime and never set by a build flag, so the exe the tests check is the exe both channels
/// ship.</para>
///
/// <para>Two things change under a package. The self-updater stands down: the Store owns updates,
/// and an Inno setup run over a <c>WindowsApps</c> folder would land beside the package rather than
/// replace it. And a launch reads how the package was activated, because the startup task and the
/// Start menu tile start it the same way and only one of them is meant to be quiet.</para>
/// </remarks>
internal static class PackageIdentity
{
    /// <summary>What <c>GetCurrentPackageFullName</c> answers a zero-length buffer with when there is a package.</summary>
    private const int ErrorInsufficientBuffer = 122;

    /// <summary><c>ActivationKind.StartupTask</c>: the manifest's startup task launched this at sign-in.</summary>
    internal const int StartupTaskKind = 1020;

    /// <summary>Whether this process belongs to a package.</summary>
    internal static bool IsPackaged { get; } = Detect();

    /// <summary>Whether this copy updates itself.</summary>
    /// <param name="packaged">Whether it runs in a package.</param>
    /// <returns><see langword="true"/> for the installer's copy, never for the Store's.</returns>
    internal static bool SelfUpdates(bool packaged) => !packaged;

    /// <summary>Whether this launch opens the window.</summary>
    /// <param name="asked">What the command line asked for: a bare launch asks, <c>--tray</c> does not (DD80).</param>
    /// <param name="packaged">Whether it runs in a package.</param>
    /// <param name="activationKind">How the package was activated, or <see langword="null"/> where that cannot be read.</param>
    /// <returns>Whether to show the window straight away.</returns>
    /// <remarks>
    /// The inverse of claude-tray's rule, because the default is: a bare launch of FreeHarbor already
    /// opens the window, and the package's startup task launches it bare. So under a package the
    /// startup task is what has to be told apart, and a kind that cannot be read counts as the
    /// startup task, so a failure never puts a window in somebody's face at sign-in. The Start menu
    /// tile and the Store's Open button keep the window a bare launch has always opened.
    /// </remarks>
    internal static bool OpensWindow(bool asked, bool packaged, int? activationKind) =>
        asked && (!packaged || activationKind is { } kind && kind != StartupTaskKind);

    /// <summary>How the package was activated.</summary>
    /// <returns>The <c>ActivationKind</c>, or <see langword="null"/> when unpackaged or unreadable.</returns>
    internal static int? ActivationKind()
    {
        if (!IsPackaged)
        {
            return null;
        }

        var name = IntPtr.Zero;
        try
        {
            const string AppInstance = "Windows.ApplicationModel.AppInstance";
            if (WindowsCreateString(AppInstance, AppInstance.Length, out name) != 0)
            {
                return null;
            }

            var iid = typeof(IAppInstanceStatics).GUID;
            if (RoGetActivationFactory(name, ref iid, out var statics) != 0)
            {
                return null;
            }

            if (statics.GetActivatedEventArgs(out var args) != 0 || args is null)
            {
                return null;
            }

            return args.GetKind(out var kind) == 0 ? kind : null;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException
            or EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }
        finally
        {
            if (name != IntPtr.Zero)
            {
                WindowsDeleteString(name);
            }
        }
    }

    private static bool Detect()
    {
        try
        {
            // A zero-length buffer answers ERROR_INSUFFICIENT_BUFFER when there is a package to name,
            // and APPMODEL_ERROR_NO_PACKAGE when there is none. Anything else reads as unpackaged, so
            // a failure here can never switch the updater off for a copy that still needs it.
            var length = 0;
            return GetCurrentPackageFullName(ref length, null) == ErrorInsufficientBuffer;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IAppInstanceStatics factory);

    // The two WinRT interfaces this needs, declared by hand rather than taking on the Windows SDK
    // projection for one call. IInspectable's three methods are placeholders that keep the slots aligned.
    [ComImport]
    [Guid("9d11e77f-9ea6-47af-a6ec-46784c5ba254")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAppInstanceStatics
    {
        void GetIids();

        void GetRuntimeClassName();

        void GetTrustLevel();

        void GetRecommendedInstance();

        [PreserveSig]
        int GetActivatedEventArgs([MarshalAs(UnmanagedType.Interface)] out IActivatedEventArgs? args);
    }

    [ComImport]
    [Guid("cf651713-cd08-4fd8-b697-a281b6544e2e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivatedEventArgs
    {
        void GetIids();

        void GetRuntimeClassName();

        void GetTrustLevel();

        [PreserveSig]
        int GetKind(out int kind);
    }
}
