using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Attribute;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SourceGenerator
{
    public class ScopedMemoizerCall
    {
        public MemberAccessExpressionSyntax ExpressionSyntax { get; }
        public ITypeSymbol ImplementationsType { get; }
        public AttributeData InterfaceAttribute { get; }
        public ITypeSymbol InterfaceType { get; }

        public string ClassName { get; }
        public string Namespace => InterfaceType.ContainingNamespace.ToDisplayString();

        public IReadOnlyList<MemoizedMethodMember> Methods { get; }

        public static bool TryCreate
        (
            MemberAccessExpressionSyntax expressionSyntax,
            ITypeSymbol interfaceType,
            ITypeSymbol implementationType,
            AttributeData interfaceAttribute,
            [NotNullWhen(true)] out ScopedMemoizerCall? call
        )
        {
            var name = interfaceAttribute.NamedArguments.FirstOrDefault(x => x.Key == nameof(CreateMemoizedImplementationAttribute.Name)).Value;

            string? classNameFromAttribute = null;
            if (!name.IsNull)
            {
                classNameFromAttribute = name.Value?.ToString();
            }

            var className = classNameFromAttribute ?? $"{(interfaceType.Name.StartsWith("I", StringComparison.OrdinalIgnoreCase)? interfaceType.Name.Substring(1) : interfaceType.Name)}_Memoized";

            var members = interfaceType.GetMembers();
            var methods = new List<MemoizedMethodMember>(members.Length);

            foreach (var member in members.OfType<IMethodSymbol>())
            {
                if (MemoizedMethodMember.TryCreate(member, out var methodMember))
                {
                    methods.Add(methodMember);
                }
                else
                {
                    call = null;
                    return false;
                }
            }

            call = new ScopedMemoizerCall(expressionSyntax, interfaceType, implementationType, interfaceAttribute, className, methods);

            return true;
        }

        private ScopedMemoizerCall
        (
            MemberAccessExpressionSyntax expressionSyntax,
            ITypeSymbol interfaceType,
            ITypeSymbol implementationType,
            AttributeData interfaceAttribute,
            string className,
            IReadOnlyList<MemoizedMethodMember> methods
        )
        {
            ExpressionSyntax = expressionSyntax;
            InterfaceType = interfaceType;
            ImplementationsType = implementationType;
            InterfaceAttribute = interfaceAttribute;
            ClassName = className;
            Methods = methods;
        }
    }

    public class MemoizedMethodMember
    {
        public static bool TryCreate(IMethodSymbol methodSymbol, [NotNullWhen(true)] out MemoizedMethodMember? method)
        {
            var @params = methodSymbol.Parameters;
            var args = new List<MemoizedMethodMemberArgument>(@params.Length);

            foreach (var param in @params)
            {
                if (MemoizedMethodMemberArgument.TryCreate(param, out var arg))
                {
                    args.Add(arg);
                }
                else
                {
                    method = null;
                    return false;
                }
            }

            method = new MemoizedMethodMember(methodSymbol, args);

            return true;
        }

        private MemoizedMethodMember(IMethodSymbol methodSymbol, IReadOnlyList<MemoizedMethodMemberArgument> parameters)
        {
            Name = methodSymbol.Name;
            ReturnsVoid = methodSymbol.ReturnsVoid;
            ReturnType = methodSymbol.ReturnType.ToDisplayString();

            _lastArg = parameters.LastOrDefault();
            Parameters = parameters;
        }
        private readonly MemoizedMethodMemberArgument? _lastArg;
        public IReadOnlyList<MemoizedMethodMemberArgument> Parameters { get; }
        public string Name { get; }
        public bool ReturnsVoid { get; }
        public string ReturnType { get; }

        public void WriteParameters(StringBuilder sb, bool writeType = false)
        {
            foreach (var arg in Parameters)
            {
                if (writeType)
                {
                    sb.Append(arg.ArgType);
                    sb.Append(" ");
                }
                sb.Append(arg.Name);
                if (!ReferenceEquals(arg, _lastArg))
                    sb.Append(", ");
            }
        }
    }

    public class MemoizedMethodMemberArgument
    {
        public static bool TryCreate(IParameterSymbol parameterSymbol, [NotNullWhen(true)] out MemoizedMethodMemberArgument? arg)
        {
            // TODO validate method args are IEquatable<>
            arg = new MemoizedMethodMemberArgument(parameterSymbol);
            return true;
        }

        private MemoizedMethodMemberArgument(IParameterSymbol parameterSymbol)
        {
            Name = parameterSymbol.Name;
            ArgType = parameterSymbol.ToDisplayString();
        }

        public string Name { get; }
        public string ArgType { get; }
    }
}