using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PrintMembersGenerator
{
    internal static class SymbolsClassifier
    {
        /// <remarks>
        /// The passed dictionary should already have all the possible keys for SymbolClassification with an initialized non-null list.
        /// </remarks>
        public static void AddSymbolsToGroups(SyntaxNode declaration, SemanticModel model, INamedTypeSymbol printMembersAttribute, Dictionary<SymbolClassification, List<ISymbol>> groups, CancellationToken cancellationToken)
        {
            // BasePropertyDeclarationSyntax is a property, indexer, or event.
            if (declaration is BasePropertyDeclarationSyntax propertyDeclaration)
            {
                // TODO: Revise if the argument can actually be null?
                ISymbol symbol = model.GetDeclaredSymbol(propertyDeclaration, cancellationToken)!;

                if (symbol is IPropertySymbol { GetMethod: null })
                {
                    // Write-only symbol.
                    return;
                }

                AddSymbolToAppropriateGroup(symbol, groups, printMembersAttribute, cancellationToken);
            }
            else if (declaration is FieldDeclarationSyntax fieldDeclarationSyntax)
            {
                // TODO:(For personal interest only) Curious to test what will happen for the following line if
                // the fieldDeclarationSyntax contains multiple declarators.
                // e.g. private string _x, _y, _z;
                //model.GetDeclaredSymbol(fieldDeclarationSyntax);

                // TODO: Revise the null suppression.
                IEnumerable<ISymbol> symbols = fieldDeclarationSyntax.Declaration.Variables
                    .Select(variable => model.GetDeclaredSymbol(variable, cancellationToken)!);

                foreach (ISymbol symbol in symbols)
                {
                    AddSymbolToAppropriateGroup(symbol, groups, printMembersAttribute, cancellationToken);
                }
            }
            else
            {
                throw new ArgumentException($@"Expected the given node to be of type {nameof(BasePropertyDeclarationSyntax)} or {nameof(FieldDeclarationSyntax)}.
Actual type: {declaration.GetType()}", nameof(declaration));
            }
        }

        /// <remarks>
        /// The passed dictionary should already have all the possible keys for SymbolClassification with an initialized non-null list.
        /// </remarks>
        private static void AddSymbolToAppropriateGroup(ISymbol symbol, Dictionary<SymbolClassification, List<ISymbol>> groups, INamedTypeSymbol printMembersAttribute, CancellationToken cancellationToken)
        {
            SymbolClassification classification = ClassifySymbol(symbol, printMembersAttribute, cancellationToken);
            Debug.Assert(groups.ContainsKey(classification), $"The dictionary was expected to have '{classification}'.");
            groups[classification].Add(symbol);
        }

        /// <summary>
        /// Classifies the given symbol.
        /// </summary>
        /// <remarks>
        /// If the user defines the compiler's default, symbol is classified as UseDefault rather than the user's choice.
        /// The reason for this is to avoid re-defining PrintMembers when it's not needed.
        /// i.e. If all defined attributes are ineffective.
        /// </remarks>
        private static SymbolClassification ClassifySymbol(ISymbol symbol, INamedTypeSymbol printMembersAttribute, CancellationToken cancellationToken)
        {
            AttributeData? data = symbol.GetAttributes().FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(printMembersAttribute, attr.AttributeClass));
            if (data is null)
            {
                return SymbolClassification.UseDefault;
            }

            bool shouldIncludeValue = getShouldInclude(data);

            if (shouldIncludeValue == symbol.IsIncludedByDefault())
            {
                return SymbolClassification.UseDefault;
            }

            return shouldIncludeValue
                ? SymbolClassification.ShouldInclude
                : SymbolClassification.ShouldExclude;

            static bool getShouldInclude(AttributeData data)
            {
                KeyValuePair<string, TypedConstant> shouldIncludeArgument = data.NamedArguments.FirstOrDefault(arg => arg.Key == Constants.ShouldInclude);
                if (shouldIncludeArgument.Key != Constants.ShouldInclude)
                {
                    // If the argument isn't found, then it's not specified and the default value for it (true) is used.
                    // TODO: I don't like this approach. See how this can be improved.
                    return true;
                }

                if (shouldIncludeArgument.Value.Value is not bool booleanShouldIncludeValue)
                {
                    throw new InvalidOperationException($"The value of {shouldIncludeArgument} was expected to be a non-null boolean value.");
                }

                return booleanShouldIncludeValue;
            }
        }
    }
}