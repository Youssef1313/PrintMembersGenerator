using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PrintMembersGenerator;
using Xunit;

namespace PrintMemberGeneratorTests
{
    public class GeneratorTests
    {
        [Fact]
        public void SimpleGeneratorTest()
        {
            string source = @"
namespace MyCode
{
    public class Program
    {
        public static void Main(string[] args)
        {
        }
    }
}
";
            Compilation comp = CreateCompilation(source);
            Compilation newComp = RunGenerators(comp, out ImmutableArray<Diagnostic> generatorDiags, new PrintMembersSourceGenerator());
            IEnumerable<SyntaxTree> generatedTrees = newComp.RemoveSyntaxTrees(comp.SyntaxTrees).SyntaxTrees;

            Assert.Single(generatedTrees);
            Assert.Empty(generatorDiags);
            Assert.Empty(newComp.GetDiagnostics());
        }

        [Fact]
        public void NonPartialRecord_ShouldBeIgnored()
        {
            string source = @"
using SourceGenerator;

namespace MyCode
{
    public record R
    {
        [PrintMembers]
        public const int I = 13;

        [PrintMembers(ShouldInclude = true)]
        public const string  S = ""Youssef"";

        [PrintMembers(ShouldInclude = false)]
        public string X { get; set; }
    }
}
";
            Compilation comp = CreateCompilation(source, OutputKind.DynamicallyLinkedLibrary);
            Compilation newComp = RunGenerators(comp, out ImmutableArray<Diagnostic> generatorDiags, new PrintMembersSourceGenerator());
            IEnumerable<SyntaxTree> generatedTrees = newComp.RemoveSyntaxTrees(comp.SyntaxTrees).SyntaxTrees;

            Assert.Single(generatedTrees);
            Assert.Empty(generatorDiags);
            Assert.Empty(newComp.GetDiagnostics());
        }

        [Fact]
        public void PartialRecord_01()
        {
            string source = @"
using SourceGenerator;

namespace MyCode
{
    public partial record R
    {
        [PrintMembers]
        public const int I = 13;

        [PrintMembers(ShouldInclude = true)]
        public const string  S = ""Youssef"";

        [PrintMembers(ShouldInclude = false)]
        public string X { get; set; }
    }
}
";

            string expectedGenerated =
@"namespace MyCode
{
    public partial record R
    {
        protected virtual bool PrintMembers(System.Text.StringBuilder builder)
        {
            builder.Append(""I"");
            builder.Append("" = "");
            builder.Append(I.ToString());
            builder.Append("", "");
            builder.Append(""S"");
            builder.Append("" = "");
            builder.Append((object)S);
            return true;
        }
    }
}";
            SyntaxTree expectedTree = CSharpSyntaxTree.ParseText(expectedGenerated);
            Compilation comp = CreateCompilation(source, OutputKind.DynamicallyLinkedLibrary);
            Compilation newComp = RunGenerators(comp, out ImmutableArray<Diagnostic> generatorDiags, new PrintMembersSourceGenerator());
            IEnumerable<SyntaxTree> generatedTrees = newComp.RemoveSyntaxTrees(comp.SyntaxTrees).SyntaxTrees;

            Assert.Equal(generatedTrees.Count(), 2); // Attribute tree and PrintMembers tree.
            Assert.True(expectedTree.IsEquivalentTo(generatedTrees.Last()), "Trees doesn't match"); // TODO: Enhance this.
            Assert.Empty(generatorDiags);
            Assert.Empty(newComp.GetDiagnostics());
        }

        private static Compilation CreateCompilation(string source, OutputKind outputKind = OutputKind.ConsoleApplication)
            => CSharpCompilation.Create("compilation",
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)) },
                new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
                new CSharpCompilationOptions(outputKind));

        private static GeneratorDriver CreateDriver(Compilation c, params ISourceGenerator[] generators)
            => CSharpGeneratorDriver.Create(generators, parseOptions: (CSharpParseOptions)c.SyntaxTrees.First().Options);

        private static Compilation RunGenerators(Compilation c, out ImmutableArray<Diagnostic> diagnostics, params ISourceGenerator[] generators)
        {
            CreateDriver(c, generators).RunGeneratorsAndUpdateCompilation(c, out var d, out diagnostics);
            return d;
        }
    }
}