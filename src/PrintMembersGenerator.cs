using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PrintMembersGenerator
{
    internal static class PrintMembersGenerator
    {
        private static readonly TypeSyntax s_stringBuilderType = SyntaxFactory.ParseTypeName(Constants.StringBuilderFullyQualifiedName);
        private static readonly SyntaxToken s_builderIdentifier = SyntaxFactory.Identifier(Constants.BuilderIdentifierName);
        private static readonly SyntaxTokenList s_emptyTokenList = SyntaxFactory.TokenList();
        private static readonly SyntaxList<AttributeListSyntax> s_emptyAttributeList = SyntaxFactory.List<AttributeListSyntax>();
        private static readonly SyntaxList<TypeParameterConstraintClauseSyntax> s_emptyConstraintList = SyntaxFactory.List<TypeParameterConstraintClauseSyntax>();
        private static readonly SyntaxToken s_printMembersIdentifier = SyntaxFactory.Identifier(Constants.PrintMembers);

        public static SourceText GeneratePrintMembersText(
            RecordDeclarationSyntax recordDeclaration,
            INamedTypeSymbol recordSymbol,
            Dictionary<SymbolClassification, List<ISymbol>> groups)
        {
            MemberDeclarationSyntax printMembersMethod = SyntaxFactory.MethodDeclaration(
                attributeLists: s_emptyAttributeList,
                modifiers: GetPrintMembersModifiers(recordSymbol),
                returnType: SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                explicitInterfaceSpecifier: null,
                identifier: s_printMembersIdentifier,
                typeParameterList: null,
                parameterList: SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList<ParameterSyntax>(
                        new[] { SyntaxFactory.Parameter(s_emptyAttributeList, s_emptyTokenList, s_stringBuilderType, s_builderIdentifier, null) }
                    )
                ),
                constraintClauses: s_emptyConstraintList,
                body: GenerateBody(recordDeclaration, recordSymbol, groups),
                expressionBody: null
            );

            string namespaceFullyQualifiedName = GetFullyQualifiedNameOfNamespace(recordSymbol.ContainingNamespace);
            recordDeclaration = recordDeclaration.WithMembers(new SyntaxList<MemberDeclarationSyntax>(printMembersMethod));

            if (namespaceFullyQualifiedName.Length == 0)
            {
                return recordDeclaration.NormalizeWhitespace().GetText(Encoding.UTF8);
            }

            NamespaceDeclarationSyntax namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(namespaceFullyQualifiedName));
            return namespaceDeclaration.WithMembers(new SyntaxList<MemberDeclarationSyntax>(recordDeclaration)).NormalizeWhitespace().GetText(Encoding.UTF8);
        }

        private static SyntaxTokenList GetPrintMembersModifiers(INamedTypeSymbol recordSymbol)
        {
            // TODO: Any chance of hitting IndexOutOfRange here?
            ISymbol printMembersSymbol = recordSymbol.GetMembers(Constants.PrintMembers)[0];

            // TODO: This assertion can/may currently fail. Find a way to not process this from the beginning.
            // Or should I throw an exception and completely fail the generator with an appropriate exception message?
            Debug.Assert(printMembersSymbol.IsImplicitlyDeclared, $"You already re-define PrintMembers in {recordSymbol.Name}.");

            var result = SyntaxFactory.TokenList();
            if (printMembersSymbol.DeclaredAccessibility == Accessibility.Private)
            {
                result = result.Add(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
            }
            else if (printMembersSymbol.DeclaredAccessibility == Accessibility.Protected)
            {
                result = result.Add(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword));
            }
            else
            {
                Debug.Assert(false, $"PrintMember accessibility is expected to be either private or protected. Found {printMembersSymbol.DeclaredAccessibility}");
            }

            if (printMembersSymbol.IsOverride)
            {
                result = result.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
            }
            else if (printMembersSymbol.IsVirtual)
            {
                result = result.Add(SyntaxFactory.Token(SyntaxKind.VirtualKeyword));
            }

            return result;
        }

        // When implementing this, consider records that are derived from object, and records that are derived from another record.
        private static BlockSyntax GenerateBody(
            RecordDeclarationSyntax recordDeclaration,
            INamedTypeSymbol recordSymbol,
            Dictionary<SymbolClassification, List<ISymbol>> groups)
        {
            List<ISymbol> forceIncluded = groups[SymbolClassification.ShouldInclude];
            List<ISymbol> includedByDefaultAndNotForceExcluded = GetIncludedByDefault(groups[SymbolClassification.UseDefault]);
            List<ISymbol> printableMembers = forceIncluded.Concat(includedByDefaultAndNotForceExcluded).ToList();

            if (printableMembers.Count == 0)
            {
                return GenerateBodyForZeroPrintableMembers(recordSymbol);
            }

            var statements = new SyntaxList<StatementSyntax>();

            if (recordSymbol.BaseType!.SpecialType != SpecialType.System_Object)
            {
                // if (base.PrintMembers(builder))
                // {
                //     builder.Append(", ");
                // }
                statements = statements.Add(SyntaxFactory.IfStatement(
                    condition: GetBasePrintMembersBuilderStatement(),
                    statement: SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(GetBuilderAppendExpression(GetStringLiteralExpressionSyntax(", "))))
                ));
            }

            // TODO: The order I'm printing may/will be different than the compiler's order.
            // Do I need to address this? and how?
            for (int i = 0; i < printableMembers.Count; i++)
            {
                // builder.Append("<MemberName>");
                statements = statements.Add(SyntaxFactory.ExpressionStatement(GetBuilderAppendExpression(GetStringLiteralExpressionSyntax(printableMembers[i].Name))));
                // builder.Append(" = ");
                statements = statements.Add(SyntaxFactory.ExpressionStatement(GetBuilderAppendExpression(GetStringLiteralExpressionSyntax(" = "))));
                // TODO: builder.Append((object)MemberName); or builder.Append(MemberName.ToString());

            }
            statements = statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)));
            return SyntaxFactory.Block(statements);
        }

        private static LiteralExpressionSyntax GetStringLiteralExpressionSyntax(string stringContent)
            => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(stringContent));

        private static BlockSyntax GenerateBodyForZeroPrintableMembers(INamedTypeSymbol recordSymbol)
        {
            StatementSyntax statement;
            // TODO: Revise null suppression.
            if (recordSymbol.BaseType!.SpecialType == SpecialType.System_Object)
            {
                // return false;
                statement = SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression));
            }
            else
            {
                // return base.PrintMembers(builder);
                statement = SyntaxFactory.ReturnStatement(GetBasePrintMembersBuilderStatement());
            }
            return SyntaxFactory.Block(new SyntaxList<StatementSyntax>(statement));
        }

        private static InvocationExpressionSyntax GetBasePrintMembersBuilderStatement()
        {
            return SyntaxFactory.InvocationExpression(
                expression: SyntaxFactory.MemberAccessExpression(
                    kind: SyntaxKind.SimpleMemberAccessExpression,
                    expression: SyntaxFactory.BaseExpression(),
                    name: SyntaxFactory.IdentifierName(Constants.PrintMembers)
                ),
                argumentList: SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.IdentifierName(Constants.BuilderIdentifierName)
                        )
                    )
                )
            );
        }

        private static InvocationExpressionSyntax GetBuilderAppendExpression(ExpressionSyntax argumentExpression)
        {
            return SyntaxFactory.InvocationExpression(
                expression: SyntaxFactory.MemberAccessExpression(
                    kind: SyntaxKind.SimpleMemberAccessExpression,
                    expression: SyntaxFactory.IdentifierName(Constants.BuilderIdentifierName),
                    name: SyntaxFactory.IdentifierName(nameof(System.Text.StringBuilder.Append))
                ),
                argumentList: SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            expression: argumentExpression
                        )
                    )
                )
            );
        }

        private static List<ISymbol> GetIncludedByDefault(List<ISymbol> symbols)
            => symbols.Where(symbol => IsIncludedByDefault(symbol)).ToList();


        private static bool IsIncludedByDefault(ISymbol symbol)
        {
            // This method is an exact copy from Roslyn's SynthesizedRecordPrintMembers code:
            // https://github.com/dotnet/roslyn/blob/585e5693bdb3cf99fa276e31e10e232d1bbe5ee5/src/Compilers/CSharp/Portable/Symbols/Synthesized/Records/SynthesizedRecordPrintMembers.cs#L210-L230
            // If for some reason the logic from Roslyn changed (which should be very very rare), this should be updated to match it.
            if (symbol.DeclaredAccessibility != Accessibility.Public || symbol.IsStatic)
            {
                return false;
            }

            if (symbol.Kind is SymbolKind.Field)
            {
                return true;
            }

            if (symbol.Kind is SymbolKind.Property)
            {
                var property = (IPropertySymbol)symbol;
                return !property.IsIndexer && !property.IsOverride && property.GetMethod is not null;
            }

            return false;
        }

        private static string GetFullyQualifiedNameOfNamespace(INamespaceSymbol @namespace)
            => @namespace.IsGlobalNamespace ? string.Empty : @namespace.ToDisplayString();
    }
}
