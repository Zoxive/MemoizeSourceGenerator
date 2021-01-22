using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            if (!Debugger.IsAttached) Debugger.Launch();

            context.RegisterForSyntaxNotifications(() => new RecieveExtensionCalls());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not RecieveExtensionCalls receiver)
                return;

            var compilation = context.Compilation;

            var serviceCollectionSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");

            var calls = new List<ScopedMemoizerCall>();

            foreach (var addMemoizedScopeCall in receiver.Candidate)
            {
                var model = compilation.GetSemanticModel(addMemoizedScopeCall.SyntaxTree);

                if (model.GetSymbolInfo(addMemoizedScopeCall.Name).Symbol is IMethodSymbol {IsGenericMethod: true} s && SymbolEqualityComparer.Default.Equals(s.ReturnType, serviceCollectionSymbol) && s.TypeArguments.Length == 2)
                {
                    calls.Add(new ScopedMemoizerCall(s, addMemoizedScopeCall));
                }
            }
        }

        private static bool HasAttribute(INamedTypeSymbol classSymbol, INamedTypeSymbol attributeSymbol)
        {
            return classSymbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attributeSymbol));
        }
    }

    public class ScopedMemoizerCall
    {
        public IMethodSymbol MethodSymbol { get; }
        public MemberAccessExpressionSyntax ExpressionSyntax { get; }

        public ScopedMemoizerCall(IMethodSymbol methodSymbol, MemberAccessExpressionSyntax expressionSyntax)
        {
            MethodSymbol = methodSymbol;
            ExpressionSyntax = expressionSyntax;
            throw new NotImplementedException();
        }
    }

    public class RecieveExtensionCalls : ISyntaxReceiver
    {
        public List<MemberAccessExpressionSyntax> Candidate { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is MemberAccessExpressionSyntax memberAccessExpressionSyntax && memberAccessExpressionSyntax.Name.Identifier.ValueText == "AddMemoizedScoped")
            {
                Candidate.Add(memberAccessExpressionSyntax);
            }
        }
    }
}
