// ReSharper disable once CheckNamespace
// This polyfill enables `init` and `record` on .NET Framework 4.6.1+
// Automatically included only when targeting net461
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for init-only setters on .NET Framework.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
