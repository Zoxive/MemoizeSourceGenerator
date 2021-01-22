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
            //if (!Debugger.IsAttached) Debugger.Launch();

            context.RegisterForSyntaxNotifications(() => new RecieveExtensionCalls());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var calls = new List<ScopedMemoizerCall>();

            if (context.SyntaxReceiver is RecieveExtensionCalls receiver)
            {
                var compilation = context.Compilation;

                var serviceCollectionSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");

                foreach (var addMemoizedScopeCall in receiver.Candidate)
                {
                    var model = compilation.GetSemanticModel(addMemoizedScopeCall.SyntaxTree);

                    var name = addMemoizedScopeCall.Name as GenericNameSyntax;
                    if (name != null && name.TypeArgumentList.Arguments.Count == 2)
                    {
                        var interfaceArg = model.GetSymbolInfo(name.TypeArgumentList.Arguments[0]).Symbol as ITypeSymbol;
                        var implArg = model.GetSymbolInfo(name.TypeArgumentList.Arguments[1]).Symbol as ITypeSymbol;

                        if (interfaceArg == null || implArg == null)
                        {
                            var label = new DiagnosticDescriptor("MemoService001", "Wrong Type", "Generic Arguments not found", "MemoizerDISourceGenerator", DiagnosticSeverity.Error, true);
                            context.ReportDiagnostic(Diagnostic.Create(label, name.GetLocation()));
                            continue;
                        }

                        calls.Add(new ScopedMemoizerCall(addMemoizedScopeCall, interfaceArg, implArg));
                    }
                }
            }

            context.AddSource("Memoized_ServiceCollectionExtensions", AddMemoizedExtensionCall.Generate(calls));
        }
    }

    public class ScopedMemoizerCall
    {
        public MemberAccessExpressionSyntax ExpressionSyntax { get; }
        public ITypeSymbol ImplementationsType { get; }
        public ITypeSymbol InterfaceType { get; }

        public ScopedMemoizerCall(MemberAccessExpressionSyntax expressionSyntax, ITypeSymbol interfaceType, ITypeSymbol implementationType)
        {
            ExpressionSyntax = expressionSyntax;
            InterfaceType = interfaceType;
            ImplementationsType = implementationType;
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
