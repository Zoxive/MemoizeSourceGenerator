using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MemoizeSourceGenerator.Models
{
    public class SlidingCache
    {
        public double InMinutes { get; }

        private SlidingCache(double inMinutes)
        {
            InMinutes = inMinutes;
        }

        public static SlidingCache? MaybeCreate(GeneratorContext context, ImmutableArray<AttributeData> interfaceAttributes)
        {
            var slidingCacheAttribute = interfaceAttributes.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, context.SlidingCacheAttribute));

            if (slidingCacheAttribute?.ConstructorArguments.Length == 1 && slidingCacheAttribute?.ConstructorArguments[0].Value is double inMinutes)
            {
                return new SlidingCache(inMinutes);
            }

            return null;
        }
    }
}