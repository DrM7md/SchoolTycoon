#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    // Enables C# 9 records and init accessors on netstandard2.1 (Unity).
    internal static class IsExternalInit { }
}
#endif
