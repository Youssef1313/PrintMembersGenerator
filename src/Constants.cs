namespace PrintMembersGenerator
{
    internal static class Constants
    {
        private const string AttributeNamespace = "SourceGenerator";
        private const string AttributeClassName = "PrintMembersAttribute";

        public static readonly string StringBuilderFullyQualifiedName = typeof(System.Text.StringBuilder).FullName;
        public static readonly string BuilderIdentifierName = "builder";
        public static readonly string ShouldInclude = nameof(ShouldInclude);
        public static readonly string PrintMembers = nameof(PrintMembers);
        public static readonly string AttributeFileName = $"{AttributeClassName}.Generated.cs";
        public static readonly string AttributeFullyQualifiedName = $"{AttributeNamespace}.{AttributeClassName}";
        public static readonly string AttributeSourceCode = $@"using System;

namespace {AttributeNamespace}
{{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class {AttributeClassName} : Attribute
    {{
        public bool {ShouldInclude} {{ get; set; }} = true;
    }}
}}
";
    }
}
