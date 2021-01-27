using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Models;

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
                        "An exception was thrown by the MockDI generator",
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

                var createMemoizedAttribute = compilation.GetAttribute("CreateMemoizedImplementationAttribute");
                var partitionAttribute = compilation.GetAttribute("PartitionCacheAttribute");
                var slidingCacheAttribute = compilation.GetAttribute("SlidingCacheAttribute");

                var myContext = new GeneratorContext(context, createMemoizedAttribute, partitionAttribute, slidingCacheAttribute);

                /*
                foreach (var possibleAttribute in receiver.CandidateAttributes)
                {
                    var model = compilation.GetSemanticModel(possibleAttribute.SyntaxTree);
                    var symbol = model.GetSymbolInfo(possibleAttribute).Symbol;

                    if (SymbolEqualityComparer.Default.Equals(symbol, createMemoizedAttribute))
                    {

                    }
                }
                */

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
                            var label = DiagError.CreateError("Wrong Type", "Generic Arguments not found");
                            context.ReportDiagnostic(Diagnostic.Create(label, name.GetLocation()));
                            continue;
                        }

                        if (!ScopedMemoizerCall.TryCreate(myContext, addMemoizedScopeCall, interfaceArg, implArg, out var scopedCall))
                            continue;

                        calls.Add(scopedCall);

                        var source = MemoizedClass.Generate(scopedCall);

                        context.AddSource(scopedCall.ClassName, source);
                    }
                }
            }

            context.AddSource("Memoized_ServiceCollectionExtensions", AddMemoizedExtensionCall.Generate(calls));
        }


    }

    public static class CompilationExtensions
    {
        public static INamedTypeSymbol GetAttribute(this Compilation compilation, string name)
        {
            var createMemoizedAttribute = compilation.GetTypeByMetadataName($"SourceGenerator.Attribute.{name}");
            if (createMemoizedAttribute == null)
                throw new Exception($"Could not locate {name}");
            return createMemoizedAttribute;
        }
    }

    public class GeneratorContext
    {
        public GeneratorExecutionContext Context { get; }
        public INamedTypeSymbol CreateMemoizedAttribute { get; }
        public INamedTypeSymbol PartitionCacheAttribute { get; }
        public INamedTypeSymbol SlidingCacheAttribute { get; }

        public GeneratorContext
        (
            GeneratorExecutionContext context,
            INamedTypeSymbol createMemoizedAttribute,
            INamedTypeSymbol partitionCacheAttribute,
            INamedTypeSymbol slidingCacheAttribute
        )
        {
            Context = context;
            CreateMemoizedAttribute = createMemoizedAttribute;
            PartitionCacheAttribute = partitionCacheAttribute;
            SlidingCacheAttribute = slidingCacheAttribute;
        }

        public void ReportDiagnostic(Diagnostic diag)
        {
            Context.ReportDiagnostic(diag);
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
