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
            try
            {
                Generate(context);
            }
            catch (Exception e)
            {
                //This is temporary till https://github.com/dotnet/roslyn/issues/46084 is fixed
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SI0000",
                        "An exception was thrown by the JsonSrcGen generator",
                        "An exception was thrown by the MockDI generator: '{0}'",
                        "MockDISrcGen",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    e.ToString() + e.StackTrace));
            }
        }

        private void Generate(GeneratorExecutionContext context)
        {
            var calls = new List<ScopedMemoizerCall>();

            if (context.SyntaxReceiver is RecieveExtensionCalls receiver)
            {
                var compilation = context.Compilation;

                var createMemoizedAttribute = compilation.GetTypeByMetadataName("SourceGenerator.Attribute.CreateMemoizedImplementationAttribute");

                foreach (var possibleAttribute in receiver.CandidateAttributes)
                {
                    var model = compilation.GetSemanticModel(possibleAttribute.SyntaxTree);
                    var symbol = model.GetSymbolInfo(possibleAttribute).Symbol;

                    if (SymbolEqualityComparer.Default.Equals(symbol, createMemoizedAttribute))
                    {

                    }
                }

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

                        var interfaceAttribute = interfaceArg.GetAttributes().FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, createMemoizedAttribute));

                        if (interfaceAttribute == null)
                        {
                            var label = new DiagnosticDescriptor("MemoService002", "Missing Memoized Attribute", "Interface must have the [CreateMemoizedImplementation] attribute attached", "MemoizerDISourceGenerator", DiagnosticSeverity.Error, true);
                            context.ReportDiagnostic(Diagnostic.Create(label, name.GetLocation()));
                            continue;
                        }

                        if (!ScopedMemoizerCall.TryCreate(addMemoizedScopeCall, interfaceArg, implArg, interfaceAttribute, out var scopedCall))
                            continue;

                        calls.Add(scopedCall);

                        context.AddSource(scopedCall.ClassName, MemoizedClass.Generate(scopedCall));
                    }
                }
            }

            context.AddSource("Memoized_ServiceCollectionExtensions", AddMemoizedExtensionCall.Generate(calls));
        }
    }

    public class RecieveExtensionCalls : ISyntaxReceiver
    {
        public List<MemberAccessExpressionSyntax> Candidate { get; } = new();
        public List<InterfaceDeclarationSyntax> CandidateAttributes { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is MemberAccessExpressionSyntax memberAccessExpressionSyntax && memberAccessExpressionSyntax.Name.Identifier.ValueText == "AddMemoizedScoped")
            {
                Candidate.Add(memberAccessExpressionSyntax);
            }

            if (syntaxNode is InterfaceDeclarationSyntax interfaceDeclarationSyntax && interfaceDeclarationSyntax.AttributeLists.Count > 0)
            {
                CandidateAttributes.Add(interfaceDeclarationSyntax);
            }
        }
    }
}
