using FreeHarbor.Core.Releases;
using FreeHarbor.Tray;
using FreeHarbor.Tray.Ui.Pages;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>How a copy behaves when it runs inside an MSIX package (DD311).</summary>
public sealed class PackageIdentityTests
{
    /// <summary><c>ActivationKind.Launch</c>: the Start menu tile and the Store's Open button.</summary>
    private const int LaunchKind = 0;

    [Fact]
    public void The_test_host_is_not_packaged() =>
        // Asked of the process, so this is the answer the installer's copy gets too.
        Assert.False(PackageIdentity.IsPackaged);

    [Fact]
    public void Only_the_installer_copy_updates_itself()
    {
        Assert.True(PackageIdentity.SelfUpdates(packaged: false));
        Assert.False(PackageIdentity.SelfUpdates(packaged: true));
    }

    [Theory]
    [InlineData(true, false, null, true)]                                    // a bare launch of the installer's copy
    [InlineData(false, false, null, false)]                                  // --tray, as its Run key launches it
    [InlineData(true, true, LaunchKind, true)]                               // the Start tile or the Store's Open
    [InlineData(true, true, PackageIdentity.StartupTaskKind, false)]         // the startup task at sign-in
    [InlineData(true, true, null, false)]                                    // a kind nobody could read
    [InlineData(false, true, LaunchKind, false)]                             // --tray asked for no window
    public void A_window_opens_unless_the_package_says_it_was_the_startup_task(
        bool asked, bool packaged, int? kind, bool opens) =>
        Assert.Equal(opens, PackageIdentity.OpensWindow(asked, packaged, kind));

    [Fact]
    public void The_about_page_names_the_Store_and_no_release_request_under_a_package()
    {
        var packaged = AboutPage.WhatItReaches(packaged: true);

        Assert.Contains("Microsoft Store", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain(ReleaseCheck.Host, packaged, StringComparison.Ordinal);
        Assert.Contains(ReleaseCheck.Host, AboutPage.WhatItReaches(packaged: false), StringComparison.Ordinal);
    }

    [Fact]
    public void The_build_line_says_where_updates_come_from_only_under_a_package()
    {
        Assert.Equal("Build 2.2.0", AboutPage.BuildLineFor("2.2.0", packaged: false));
        Assert.Equal(
            "Build 2.2.0 · updates come from the Microsoft Store",
            AboutPage.BuildLineFor("2.2.0", packaged: true));
    }
}
