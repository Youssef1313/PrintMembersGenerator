using Microsoft.CodeAnalysis;

namespace PrintMembersGenerator
{
    internal static class ISymbolExtensions
    {
        public static bool IsIncludedByDefault(this ISymbol symbol)
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


    }
}