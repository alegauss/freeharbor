// DD350 — NOTICE, written from the code that renders it.
//
// NOTICE is generated (Attribution.Notice) and a test refuses a committed copy that differs, but
// until DD350 nothing in the repository wrote it: every version bump regenerated it by hand. The
// weekly pin workflow needs it unattended, so it is a file-based app here, runnable the same way
// from a shell:
//
//   dotnet run build/write-notice.cs -- NOTICE
//
// Not AOT and with reflection-based JSON on, because the manifest it reads is deserialised that way
// and a file-based app defaults to neither.
#:property TargetFramework=net10.0-windows
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:project ../src/FreeHarbor.Core/FreeHarbor.Core.csproj

using FreeHarbor.Core.Engine;
using FreeHarbor.Core.Licensing;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run build/write-notice.cs -- <path to NOTICE>");
    return 2;
}

File.WriteAllText(args[0], Attribution.Notice(EngineManifest.Current));
return 0;
