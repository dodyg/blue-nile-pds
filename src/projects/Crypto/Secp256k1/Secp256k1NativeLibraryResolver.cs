using System.Reflection;
using System.Runtime.InteropServices;

namespace Crypto.Secp256k1;

internal static class Secp256k1NativeLibraryResolver
{
    private const string LibraryName = "secp256k1";
    private static readonly object Sync = new();
    private static bool _resolverRegistered;
    private static IntPtr _libraryHandle;

    public static void EnsureLoaded()
    {
        if (_libraryHandle != IntPtr.Zero)
        {
            return;
        }

        lock (Sync)
        {
            if (_libraryHandle != IntPtr.Zero)
            {
                return;
            }

            if (!_resolverRegistered)
            {
                try
                {
                    NativeLibrary.SetDllImportResolver(typeof(Secp256k1Net.Secp256k1).Assembly, Resolve);
                }
                catch (InvalidOperationException ex)
                {
                    _libraryHandle = LoadBundledLibrary();
                    if (_libraryHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(
                            "A DllImport resolver was already registered for Secp256k1.Net and the bundled secp256k1 library could not be loaded directly.",
                            ex);
                    }
                }

                _resolverRegistered = true;
            }

            if (_libraryHandle == IntPtr.Zero)
            {
                _libraryHandle = LoadBundledLibrary();
            }
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!IsSecp256k1Library(libraryName))
        {
            return IntPtr.Zero;
        }

        lock (Sync)
        {
            if (_libraryHandle != IntPtr.Zero)
            {
                return _libraryHandle;
            }

            _libraryHandle = LoadBundledLibrary();
            return _libraryHandle;
        }
    }

    private static IntPtr LoadBundledLibrary()
    {
        Exception? lastLoadException = null;

        foreach (var candidate in GetCandidatePaths())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return NativeLibrary.Load(candidate);
            }
            catch (DllNotFoundException ex)
            {
                lastLoadException = ex;
            }
            catch (BadImageFormatException ex)
            {
                lastLoadException = ex;
            }
        }

        if (lastLoadException != null)
        {
            throw new InvalidOperationException("Unable to load the bundled secp256k1 native library.", lastLoadException);
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        var fileName = GetLibraryFileName();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var directory in GetCandidateDirectories())
        {
            var directPath = Path.Combine(directory, fileName);
            if (seen.Add(directPath))
            {
                yield return directPath;
            }
        }

        foreach (var runtimeIdentifier in GetRuntimeIdentifiers())
        {
            var runtimePath = Path.Combine(AppContext.BaseDirectory, "runtimes", runtimeIdentifier, "native", fileName);
            if (seen.Add(runtimePath))
            {
                yield return runtimePath;
            }
        }
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string nativeSearchDirectories)
        {
            foreach (var directory in nativeSearchDirectories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return directory;
            }
        }

        yield return AppContext.BaseDirectory;

        var assemblyDirectory = Path.GetDirectoryName(typeof(Secp256k1Net.Secp256k1).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            yield return assemblyDirectory;
        }
    }

    private static IEnumerable<string> GetRuntimeIdentifiers()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return $"win-{GetArchitectureSuffix()}";
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return $"osx-{GetArchitectureSuffix()}";
            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            var architectureSuffix = GetArchitectureSuffix();
            yield return $"linux-{architectureSuffix}";

            if (architectureSuffix is "x64" or "arm64")
            {
                yield return $"linux-musl-{architectureSuffix}";
            }
        }
    }

    private static string GetArchitectureSuffix()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Unsupported process architecture for secp256k1: {RuntimeInformation.ProcessArchitecture}")
        };
    }

    private static string GetLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{LibraryName}.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"lib{LibraryName}.dylib";
        }

        if (OperatingSystem.IsLinux())
        {
            return $"lib{LibraryName}.so";
        }

        throw new PlatformNotSupportedException($"Unsupported platform for secp256k1: {RuntimeInformation.OSDescription}");
    }

    private static bool IsSecp256k1Library(string libraryName)
    {
        return libraryName is LibraryName or "libsecp256k1" or "libsecp256k1.so" or "libsecp256k1.dylib" or "secp256k1.dll";
    }
}
