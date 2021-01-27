using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Attribute;

namespace SourceGenerator.Models
{
    public class ScopedMemoizerCall
    {
        public static bool TryCreate
        (
            GeneratorContext context,
            MemberAccessExpressionSyntax expressionSyntax,
            ITypeSymbol interfaceType,
            ITypeSymbol implementationType,
            [NotNullWhen(true)] out ScopedMemoizerCall? call)
        {
            var interfaceAttributes = interfaceType.GetAttributes();
            var errorLocation = expressionSyntax.Name.GetLocation();

            if (!TryGetClassName(context, interfaceType, interfaceAttributes, errorLocation, out var className))
            {
                call = null;
                return false;
            }

            var slidingCache = SlidingCache.MaybeCreate(context, interfaceAttributes);

            var members = interfaceType.GetMembers();
            var methods = new List<MemoizedMethodMember>(members.Length);

            var methodSymbols = members.OfType<IMethodSymbol>().ToList();

            // TODO Create different ArgKey class name implementations
            // then Check names to see what ArgKey_ class name algo we can use

            foreach (var member in methodSymbols)
            {
                if (MemoizedMethodMember.TryCreate(context, errorLocation, member, out var methodMember))
                {
                    methods.Add(methodMember);
                }
                else
                {
                    call = null;
                    return false;
                }
            }

            call = new ScopedMemoizerCall(interfaceType, implementationType, className, methods, slidingCache);

            return true;
        }

        private static bool TryGetClassName
        (
            GeneratorContext context,
            ITypeSymbol interfaceType,
            ImmutableArray<AttributeData> interfaceAttributes,
            Location errorLocation,
            [NotNullWhen(true)] out string? className
        )
        {
            var memoizeAttribute = interfaceAttributes.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, context.CreateMemoizedAttribute));

            if (memoizeAttribute == null)
            {
                var label = DiagError.CreateError("Missing Memoized Attribute", "Interface must have the [CreateMemoizedImplementation] attribute attached");
                context.ReportDiagnostic(Diagnostic.Create(label, errorLocation));
                className = null;
                return false;
            }

            var name = memoizeAttribute.NamedArguments.FirstOrDefault(x => x.Key == nameof(CreateMemoizedImplementationAttribute.Name)).Value;

            string? classNameFromAttribute = null;
            if (!name.IsNull)
            {
                classNameFromAttribute = name.Value as string;
            }

            className = classNameFromAttribute ??
                        $"{(interfaceType.Name.StartsWith("I", StringComparison.OrdinalIgnoreCase) ? interfaceType.Name.Substring(1) : interfaceType.Name)}_Memoized";
            return true;
        }

        private ScopedMemoizerCall
        (
            ITypeSymbol interfaceType,
            ITypeSymbol implementationType,
            string className,
            IReadOnlyList<MemoizedMethodMember> methods,
            SlidingCache? slidingCache
        )
        {
            InterfaceType = interfaceType;
            ImplementationsType = implementationType;
            ClassName = className;
            Methods = methods;
            SlidingCache = slidingCache;
        }

        public ITypeSymbol ImplementationsType { get; }
        public ITypeSymbol InterfaceType { get; }
        public string ClassName { get; }
        public string Namespace => InterfaceType.ContainingNamespace.ToDisplayString();
        public IReadOnlyList<MemoizedMethodMember> Methods { get; }
        public SlidingCache? SlidingCache { get; }
    }
}