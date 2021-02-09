using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MemoizeSourceGenerator.Attribute;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemoizeSourceGenerator.Models
{
    public class MemoizerCall
    {
        public static bool TryCreate
        (
            GeneratorContext context,
            MemberAccessExpressionSyntax expressionSyntax,
            ITypeSymbol interfaceType,
            ITypeSymbol implementationType,
            [NotNullWhen(true)] out MemoizerCall? call)
        {
            var interfaceAttributes = interfaceType.GetAttributes();

            if (!context.TryGetInterfaceContext(interfaceType, out var interfaceContext))
            {
                // The Interface could exist in a referenced project which we dont have the interfacesyntax for.. lets check that
                var memoizeAttribute2 = interfaceAttributes.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, context.CreateMemoizedAttribute));

                if (memoizeAttribute2 == null)
                {
                    context.CreateError("Missing Memoized Attribute", "Interface must have the [CreateMemoizedImplementation] attribute attached", expressionSyntax.Name.GetLocation());
                    call = null;
                    return false;
                }

                interfaceContext = CreateMemoizeInterfaceContext.CreateFromType(interfaceType, memoizeAttribute2, expressionSyntax.GetLocation());
            }

            var memoizeAttribute = interfaceContext.CreateMemoizedAttributeData;

            if (!TryGetClassName(memoizeAttribute, interfaceType, out var className, out var humanId))
            {
                call = null;
                return false;
            }

            var memoizerFactory = memoizeAttribute.NamedArguments.FirstOrDefault(x => x.Key == nameof(CreateMemoizedImplementationAttribute.MemoizerFactory)).Value;
            INamedTypeSymbol? memoizerFactoryTypeSymbol = null;
            if (!memoizerFactory.IsNull)
            {
                memoizerFactoryTypeSymbol = memoizerFactory.Value as INamedTypeSymbol;
            }

            if (memoizerFactoryTypeSymbol != null)
            {
                if (!memoizerFactoryTypeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, context.MemoizerFactoryInterface)))
                {
                    context.CreateError($"Wrong {nameof(CreateMemoizedImplementationAttribute.MemoizerFactory)} type", $"MemoizerFactory type must implement {nameof(IMemoizerFactory)}", interfaceContext.InterfaceWithAttribute?.GetLocation() ?? expressionSyntax.Name.GetLocation());
                    call = null;
                    return false;
                }
            }

            var slidingCache = SlidingCache.MaybeCreate(context, interfaceAttributes);

            var members = interfaceType.GetMembers();
            var methods = new List<MemoizedMethodMember>(members.Length);
            var methodSymbols = members.OfType<IMethodSymbol>().ToList();

            // TODO Create different ArgKey class name implementations
            // then Check names to see what ArgKey_ class name algo we can use

            foreach (var interfaceMethod in methodSymbols)
            {
                if (MemoizedMethodMember.TryCreate(context, interfaceContext, interfaceMethod, out var methodMember))
                {
                    methods.Add(methodMember);
                }
                else
                {
                    call = null;
                    return false;
                }
            }

            call = new MemoizerCall(interfaceType, implementationType, className, methods, slidingCache, memoizerFactoryTypeSymbol, humanId);

            return true;
        }

        private static bool TryGetClassName(AttributeData memoizeAttribute, ITypeSymbol interfaceType, out string className, out string humanId)
        {
            var name = memoizeAttribute.NamedArguments.FirstOrDefault(x => x.Key == nameof(CreateMemoizedImplementationAttribute.Name)).Value;
            string? classNameFromAttribute = null;
            if (!name.IsNull)
            {
                classNameFromAttribute = name.Value as string;
            }

            var interfaceNameWithoutI = interfaceType.Name.StartsWith("I", StringComparison.OrdinalIgnoreCase) ? interfaceType.Name.Substring(1) : interfaceType.Name;

            className = classNameFromAttribute ?? "Memoized_" + interfaceNameWithoutI;
            humanId = classNameFromAttribute ?? interfaceNameWithoutI;

            return true;
        }

        private MemoizerCall
        (ITypeSymbol interfaceType,
            ITypeSymbol implementationType,
            string className,
            IReadOnlyList<MemoizedMethodMember> methods,
            SlidingCache? slidingCache,
            INamedTypeSymbol? memoizerFactoryType,
            string humanId)
        {
            InterfaceType = interfaceType;
            ImplementationsType = implementationType;
            ClassName = className;
            Methods = methods;
            SlidingCache = slidingCache;
            MemoizerFactoryType = memoizerFactoryType;
            HumanId = humanId;
        }

        public ITypeSymbol ImplementationsType { get; }
        public ITypeSymbol InterfaceType { get; }
        public string HumanId { get; }
        public string ClassName { get; }
        //public string Namespace => $"{InterfaceType.ContainingNamespace.ToDisplayString()}.Memoized";
        public string Namespace => "Memoized";
        public IReadOnlyList<MemoizedMethodMember> Methods { get; }
        public SlidingCache? SlidingCache { get; }
        public INamedTypeSymbol? MemoizerFactoryType { get; }

        public bool IsSameType(MemoizerCall scopedCall)
        {
            if (!SymbolEqualityComparer.Default.Equals(InterfaceType, scopedCall.InterfaceType))
                return false;

            if (!SymbolEqualityComparer.Default.Equals(ImplementationsType, scopedCall.ImplementationsType))
                return false;

            return true;
        }
    }
}