using System;
using System.Collections.Generic;
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
            GeneratorExecutionContext context,
            MemberAccessExpressionSyntax expressionSyntax,
            ITypeSymbol interfaceType,
            ITypeSymbol implementationType,
            INamedTypeSymbol createMemoizedAttribute,
            [NotNullWhen(true)] out ScopedMemoizerCall? call)
        {
            var interfaceAttribute = interfaceType.GetAttributes().FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, createMemoizedAttribute));

            var errorLocation = expressionSyntax.Name.GetLocation();

            if (interfaceAttribute == null)
            {
                var label = DiagError.CreateError("Missing Memoized Attribute", "Interface must have the [CreateMemoizedImplementation] attribute attached");
                context.ReportDiagnostic(Diagnostic.Create(label, errorLocation));
                call = null;
                return false;
            }

            var name = interfaceAttribute.NamedArguments.FirstOrDefault(x => x.Key == nameof(CreateMemoizedImplementationAttribute.Name)).Value;

            string? classNameFromAttribute = null;
            if (!name.IsNull)
            {
                classNameFromAttribute = name.Value?.ToString();
            }

            var className = classNameFromAttribute ?? $"{(interfaceType.Name.StartsWith("I", StringComparison.OrdinalIgnoreCase)? interfaceType.Name.Substring(1) : interfaceType.Name)}_Memoized";

            var members = interfaceType.GetMembers();
            var methods = new List<MemoizedMethodMember>(members.Length);

            var methodSymbols = members.OfType<IMethodSymbol>().ToList();

            // TODO Check names to see what ArgKey_ class name impl we can use
            // Create different ArgKey class name implementations

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
        public static bool TryCreate(GeneratorExecutionContext context, Location errorLocation, IMethodSymbol methodSymbol,
            [NotNullWhen(true)] out MemoizedMethodMember? method)
        {
            var @params = methodSymbol.Parameters;
            var args = new List<MemoizedMethodMemberArgument>(@params.Length);

            foreach (var param in @params)
            {
                if (MemoizedMethodMemberArgument.TryCreate(context, errorLocation, param, out var arg))
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
            ReturnType = methodSymbol.ReturnType.ToDisplayString();

            Name = methodSymbol.Name;

            var argNames = parameters.Select(x => x.ArgType.Replace('.', '_')).ToArray();
            ClassName = $"ArgKey_{ReturnType}_{methodSymbol.Name}_{(string.Join('_', argNames))}";

            ReturnsVoid = methodSymbol.ReturnsVoid;

            _lastArg = parameters.LastOrDefault();
            Parameters = parameters;
        }

        public string ClassName { get; }

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
        public static bool TryCreate
        (
            GeneratorExecutionContext context,
            Location errorLocation,
            IParameterSymbol parameterSymbol,
            [NotNullWhen(true)] out MemoizedMethodMemberArgument? arg
        )
        {
            var type = parameterSymbol.Type;

            // TODO structs

            if (!parameterSymbol.DeclaringSyntaxReferences.IsEmpty)
            {
                var syntaxReference = parameterSymbol.DeclaringSyntaxReferences.First();
                errorLocation = syntaxReference.SyntaxTree.GetLocation(syntaxReference.Span);
            }

            if (TypeRequiresEquatable(type))
            {
                if (!type.AllInterfaces.Any(x => x.MetadataName == "IEquatable`1"))
                {
                    var label = DiagError.CreateError("Must implement IEquatable<>", $"Type {parameterSymbol.ToDisplayString()} must implement IEquatable<>");
                    context.ReportDiagnostic(Diagnostic.Create(label, errorLocation));
                    arg = null;
                    return false;
                }
            }

            arg = new MemoizedMethodMemberArgument(parameterSymbol);
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

        private MemoizedMethodMemberArgument(IParameterSymbol parameterSymbol)
        {
            Name = parameterSymbol.Name;
            ArgType = parameterSymbol.ToDisplayString();
        }

        public string Name { get; }
        public string ArgType { get; }
    }
}