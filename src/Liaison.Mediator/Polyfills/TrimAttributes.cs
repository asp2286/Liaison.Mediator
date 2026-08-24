// Trim/AOT annotation attributes do not exist on netstandard2.0, so internal
// copies are compiled into that target only. Trim tooling matches these
// attributes by full type name regardless of the defining assembly, so a
// net6.0/net7.0 consumer that resolves the netstandard2.0 asset gets real
// diagnostics from them - the shapes below must stay exactly in sync with the
// runtime's definitions.
#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
internal sealed class RequiresDynamicCodeAttribute : Attribute
{
    public RequiresDynamicCodeAttribute(string message)
    {
        Message = message;
    }

    public string Message { get; }

    public string? Url { get; set; }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
internal sealed class RequiresUnreferencedCodeAttribute : Attribute
{
    public RequiresUnreferencedCodeAttribute(string message)
    {
        Message = message;
    }

    public string Message { get; }

    public string? Url { get; set; }
}

[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter |
    AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method |
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
    Inherited = false)]
internal sealed class DynamicallyAccessedMembersAttribute : Attribute
{
    public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
    {
        MemberTypes = memberTypes;
    }

    public DynamicallyAccessedMemberTypes MemberTypes { get; }
}

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
internal sealed class UnconditionalSuppressMessageAttribute : Attribute
{
    public UnconditionalSuppressMessageAttribute(string category, string checkId)
    {
        Category = category;
        CheckId = checkId;
    }

    public string Category { get; }

    public string CheckId { get; }

    public string? Scope { get; set; }

    public string? Target { get; set; }

    public string? MessageId { get; set; }

    public string? Justification { get; set; }
}

[Flags]
internal enum DynamicallyAccessedMemberTypes
{
    None = 0,
    PublicParameterlessConstructor = 0x0001,
    PublicConstructors = 0x0001 | 0x0002,
    NonPublicConstructors = 0x0004,
    PublicMethods = 0x0008,
    NonPublicMethods = 0x0010,
    PublicFields = 0x0020,
    NonPublicFields = 0x0040,
    PublicNestedTypes = 0x0080,
    NonPublicNestedTypes = 0x0100,
    PublicProperties = 0x0200,
    NonPublicProperties = 0x0400,
    PublicEvents = 0x0800,
    NonPublicEvents = 0x1000,
    Interfaces = 0x2000,
    All = ~None,
}
#endif
