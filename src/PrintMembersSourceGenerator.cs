using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/*
TODO: Analyzers
1. Attribute is used in a non-record type.
2. Attribute is used in non-partial record.
3. Attribute is used in a record that already re-defines PrintMembers.
4. Property isn't readable (set only) (the attribute is completely ignored in this case).
5. Warning for in-effective attributes. (Note: Take positional record parameters in account if supported.)
6. Warning for included override properties (it causes duplicates).
*/

/*
TODO: Tests
1. Is the attribute actually inherited?
*/


/*
CONSIDER: What if the record original user definition of record is defined in 2 different syntax tree or has two partial declarations.
*/

namespace PrintMembersGenerator
{
    [Generator]
    public class PrintMembersSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new PartialRecordsSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            SourceText atttributeSourceText = SourceText.From(Constants.AttributeSourceCode, Encoding.UTF8);

            context.AddSource(Constants.AttributeFileName, atttributeSourceText);

            if (context.SyntaxReceiver is not PartialRecordsSyntaxReceiver receiver)
            {
                return;
            }

            // Workaround source generator issue. Currently, the added source isn't visible to
            // the current compilation. The following lines explicitly add it.
            // If Roslyn fixed this on their side, these will no longer be needed.
            ParseOptions options = ((CSharpCompilation)context.Compilation).SyntaxTrees[0].Options;
            SyntaxTree attributeTree = CSharpSyntaxTree.ParseText(atttributeSourceText, (CSharpParseOptions)options);
            Compilation compilation = context.Compilation.AddSyntaxTrees(attributeTree);

            INamedTypeSymbol? printMembersAttribute = compilation.GetTypeByMetadataName(Constants.AttributeFullyQualifiedName);
            if (printMembersAttribute is null)
            {
                return;
            }

            foreach (RecordDeclarationSyntax node in receiver.PartialRecords)
            {
                SemanticModel model = compilation.GetSemanticModel(node.SyntaxTree);

                var groups = GetSymbolGroupsFromPartialRecord(node, model, printMembersAttribute, context.CancellationToken);

                if (groups[SymbolClassification.ShouldInclude].Count == 0 &&
                    groups[SymbolClassification.ShouldExclude].Count == 0)
                {
                    continue;
                }

                // TODO: Revise null suppression.
                INamedTypeSymbol recordSymbol = model.GetDeclaredSymbol(node, context.CancellationToken)!;
                SourceText recordSourceText = PrintMembersGenerator.GeneratePrintMembersText(node, recordSymbol, groups);
                System.IO.File.WriteAllText(@"C:\Test.txt", recordSourceText.ToString());

                // Can we hit multiple declarations with the same identifier name?? e.g. when the developer have existing two partial declarations.
                context.AddSource($"{node.Identifier.ValueText}_PrintMembers.Generated.cs", recordSourceText);
            }
        }

        private Dictionary<SymbolClassification, List<ISymbol>> GetSymbolGroupsFromPartialRecord(RecordDeclarationSyntax node, SemanticModel model, INamedTypeSymbol printMembersAttribute, CancellationToken cancellationToken)
        {
            var shouldIncludeSymbols = new List<ISymbol>();
            var shouldExcludeSymbols = new List<ISymbol>();
            var useDefaultSymbols = new List<ISymbol>();
            var symbolsDictionary = new Dictionary<SymbolClassification, List<ISymbol>>();

            symbolsDictionary.Add(SymbolClassification.ShouldInclude, new List<ISymbol>());
            symbolsDictionary.Add(SymbolClassification.ShouldExclude, new List<ISymbol>());
            symbolsDictionary.Add(SymbolClassification.UseDefault, new List<ISymbol>());

            // TODO: Should I add support for positional record parameters?
            IEnumerable<SyntaxNode> declarationNodes = node.ChildNodes()
                .Where(node => node is (BasePropertyDeclarationSyntax or FieldDeclarationSyntax));

            foreach (SyntaxNode declarationNode in declarationNodes)
            {
                SymbolsClassifier.AddSymbolsToGroups(
                    declarationNode,
                    model,
                    printMembersAttribute,
                    symbolsDictionary,
                    cancellationToken);
            }

            return symbolsDictionary;
        }

        private class PartialRecordsSyntaxReceiver : ISyntaxReceiver
        {
            public List<RecordDeclarationSyntax> PartialRecords { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is RecordDeclarationSyntax record &&
                    record.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    PartialRecords.Add(record);
                }
            }
        }
    }
}
