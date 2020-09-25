using Microsoft.CodeAnalysis;

namespace PrintMembersGenerator
{
    internal static class ISymbolExtensions
    {
        /// <summary>
        /// Determines whether the compiler includes the symbol in PrintMembers or not.
        /// </summary>
        /// <remarks>
        /// The compiler includes the symbol if all the following is true:
        /// 1. Symbol is public.
        /// 2. Symbol is not static or constant.
        /// 3. Symbol is either a field or a property.
        /// 3. Symbol is not an indexer.
        /// 4. Symbol is not an override.
        /// 5. Symbol is not a set-only/init-only property.
        /// </remarks>
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