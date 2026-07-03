using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Utilities;

[Flags]
public enum OsPlatforms : ushort
{
    Windows = 1,
    Linux = 2,
    MacOS = 4
}

/// <summary>
///     An xUnit [Fact] that is skipped unless the current operating system is one
///     of the supplied platforms. Replaces the FFMpegCore MSTest
///     OsSpecificTestMethod attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OsSpecificFactAttribute : FactAttribute
{
    public OsSpecificFactAttribute(OsPlatforms supportedOsPlatforms,
        [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        var supported = new List<OSPlatform>();
        if (supportedOsPlatforms.HasFlag(OsPlatforms.Windows))
        {
            supported.Add(OSPlatform.Windows);
        }

        if (supportedOsPlatforms.HasFlag(OsPlatforms.Linux))
        {
            supported.Add(OSPlatform.Linux);
        }

        if (supportedOsPlatforms.HasFlag(OsPlatforms.MacOS))
        {
            supported.Add(OSPlatform.OSX);
        }

        if (!supported.Any(RuntimeInformation.IsOSPlatform))
        {
            Skip = $"Test only runs on: {string.Join(", ", supported.Select(p => p.ToString()))}";
        }
    }
}
