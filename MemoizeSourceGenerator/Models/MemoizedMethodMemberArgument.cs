using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MemoizeSourceGenerator.Models
{
    public class MemoizedMethodMemberArgument
    {
        public static bool TryCreate
        (
            GeneratorContext context,
            Location errorLocation,
            IParameterSymbol parameterSymbol,
            /*[NotNullWhen(true)]*/ out MemoizedMethodMemberArgument? arg
        )
        {
            var type = parameterSymbol.Type;

            if (!parameterSymbol.DeclaringSyntaxReferences.IsEmpty)
            {
                var syntaxReference = parameterSymbol.DeclaringSyntaxReferences.First();
                errorLocation = syntaxReference.SyntaxTree.GetLocation(syntaxReference.Span);
            }

            var attributes = parameterSymbol.GetAttributes();

            var partitionsCache = attributes.Any(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, context.PartitionCacheAttribute));

            if (TypeRequiresEquatable(type))
            {
                if (!type.AllInterfaces.Any(x => x.MetadataName == "IEquatable`1"))
                {
                    context.CreateError("Must implement IEquatable<>", $"Type {parameterSymbol.ToDisplayString()} must implement IEquatable<>", errorLocation);
                    arg = null;
                    return false;
                }
            }

            arg = new MemoizedMethodMemberArgument(parameterSymbol, partitionsCache);
            return true;
        }

        private static bool TypeRequiresEquatable(ITypeSymbol type)
        {
            switch (type.TypeKind)
            {
                case TypeKind.Class:
                case TypeKind.Struct:
                case TypeKind.Interface:
                    return true;
            }

            return false;
        }

        private MemoizedMethodMemberArgument(IParameterSymbol parameterSymbol, bool partitionsCache)
        {
            PartitionsCache = partitionsCache;
            Name = parameterSymbol.Name;
            ArgType = parameterSymbol.ToDisplayString();
            IsNullable = parameterSymbol.NullableAnnotation == NullableAnnotation.Annotated;
        }

        public bool IsNullable { get; }
        public bool PartitionsCache { get; }
        public string Name { get; }
        public string ArgType { get; }
    }
}