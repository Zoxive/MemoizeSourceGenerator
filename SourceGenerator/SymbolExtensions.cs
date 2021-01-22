using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace SourceGenerator
{
    public static class SymbolExtensions
    {
        public static bool IsObject(this ITypeSymbol symbol)
        {
            return symbol.SpecialType == SpecialType.System_Object;
        }

        public static bool IsObjectOrObjectArray(this ITypeSymbol symbol)
        {
            return IsObject(symbol) || IsObjectArray(symbol);
        }

        public static bool IsObjectArray(this ITypeSymbol symbol)
        {
            if (symbol is IArrayTypeSymbol arrayType)
            {
                return arrayType.ElementType.SpecialType == SpecialType.System_Object;
            }

            return false;
        }

        public static bool IsStringArray(this ITypeSymbol symbol)
        {
            if (symbol is IArrayTypeSymbol arrayType)
            {
                return arrayType.ElementType.SpecialType == SpecialType.System_String;
            }

            return false;
        }

        public static bool IsString(this ITypeSymbol symbol)
        {
            return symbol.SpecialType == SpecialType.System_String;
        }

        public static bool IsStringOrStringArray(this ITypeSymbol symbol)
        {
            return IsString(symbol) || IsStringArray(symbol);
        }

        public static bool IsGeneric(this ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol namedTypeSymbol)
            {
                return namedTypeSymbol.IsGenericType;
            }

            return false;
        }

        public static IEnumerable<ISymbol> GetAllBaseMembers(this INamedTypeSymbol symbol)
        {
            var parent = symbol.BaseType;

            while (parent != null)
            {
                foreach (var member in parent.GetMembers())
                {
                    if (member.IsOverride)
                        continue;

                    yield return member;
                }

                parent = parent.BaseType;
            }

            foreach (var member in symbol.GetMembers())
            {
                if (member.IsOverride)
                    continue;

                yield return member;
            }
        }
    }
}