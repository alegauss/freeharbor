# DD309: WSL registration from inside an MSIX package

The gate for the Store path (DD292). A full-trust app in an MSIX package has its writes to a
new folder under AppData redirected to the package's LocalCache, and its HKCU writes kept in a
private hive. FreeHarbor's root is `%LOCALAPPDATA%\FreeHarbor`, and `distro` under it is the
BasePath WSL registers the disk at. WslService runs outside the package and sees the real
filesystem, so the question was whether the app and the service agree on where the disk is.

## How it was measured

On 16 November 2026, on Windows 11 build 26300, with `build\msix-probe\`: a self-signed
package holding the Release `FreeHarbor.exe` of that day, installed by sideload, and driven
through an execution alias so every verb ran with the package's identity. The host had no
FreeHarbor installed and no `freeharbor` distribution during the run. Each mode was installed,
exercised, removed and cleaned up before the other.

- **Virtualized**: the default for a full-trust package.
- **Unvirtualized**: `desktop6:FileSystemWriteVirtualization` and
  `desktop6:RegistryWriteVirtualization` set to `disabled`, under the
  `rescap:unvirtualizedResources` capability.

## What happened

| | Virtualized | Unvirtualized |
|---|---|---|
| `--provision` | Fails at ImportDistribution: `Wsl/ERROR_PATH_NOT_FOUND` | Succeeds |
| Where the root lands | `Packages\<family>\LocalCache\Local\FreeHarbor`, while the app reports `%LOCALAPPDATA%\FreeHarbor` | `%LOCALAPPDATA%\FreeHarbor` |
| Lxss BasePath | none: the distribution is never registered | `%LOCALAPPDATA%\FreeHarbor\distro`, where the vhdx really is |
| `--run`, `docker_engine` | "not registered" | serves the pipe; `docker version` from outside the package answers |
| `--status`, `--stop` | "not registered" | work |
| `--fsck` | "not registered" | finds the disk, brings up the rescue, reports it clean |
| `--compact` | "not registered" | every step but the last; Windows has sparse VHDs turned off, which refuses the same step outside a package |
| `--autostart on` | written to the private hive; the real HKCU Run key never sees it | written to the real HKCU Run key, naming the exe under `C:\Program Files\WindowsApps` |
| After `Remove-AppxPackage` | nothing is left: LocalCache and the hive go with the package | the distribution, its disk, the root and the Run value all stay; the Run value now names an exe that no longer exists |

The Virtualized failure is the split the design predicted: the app hands `wsl --import` a path
that exists only in its redirected view, and the service, which reads the real disk, finds
nothing there.

## The decision

**The package uses Unvirtualized**, `desktop6:FileSystemWriteVirtualization` and
`desktop6:RegistryWriteVirtualization` disabled under `rescap:unvirtualizedResources`. It is the
only mode in which `wsl --import` works, and with it every verb behaves as it does in the Inno
install. The capability is restricted, so the Store submission has to justify it: the app hands
paths to a Windows service outside the package, and that service has to find the files.

What the measurement settles for the tasks after it:

- DD310 builds the manifest with these two elements and the capability.
- DD313 is confirmed: a Run value written from the package outlives the package, so startup has
  to be a manifest startup task, which goes with it.
- DD317 is confirmed in its unvirtualized case: removing the package leaves the distribution,
  its disk and the whole root, so a removal verb run first is the only clean uninstall.
