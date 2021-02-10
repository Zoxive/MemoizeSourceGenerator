using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MemoizeSourceGenerator.Attribute;
using MemoizeSourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemoizeSourceGenerator
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
            var calls = new List<MemoizerCall>();

            if (context.SyntaxReceiver is not RecieveExtensionCalls receiver)
            {
                context.AddSource("Memoized_ServiceCollectionExtensions", AddMemoizedExtensionCall.Generate(calls));
                return;
            }

            var compilation = context.Compilation;

            var createMemoizedAttribute = compilation.GetSymbol(nameof(CreateMemoizedImplementationAttribute));
            var partitionAttribute = compilation.GetSymbol(nameof(PartitionCacheAttribute));
            var slidingCacheAttribute = compilation.GetSymbol(nameof(SlidingCacheAttribute));
            var sizeOfResultAttribute = compilation.GetSymbol(nameof(SizeOfResultAttribute));
            var memoizerFactoryInterface = compilation.GetSymbol(nameof(IMemoizerFactory));

            #pragma warning disable RS1024
            var createAttributes = new Dictionary<ITypeSymbol, CreateMemoizeInterfaceContext>();
            #pragma warning restore RS1024

            foreach (var interfaceWithAttribute in receiver.CandidateAttributes)
            {
                var interfaceModel = compilation.GetSemanticModel(interfaceWithAttribute.SyntaxTree);
                var interfaceType = interfaceModel.GetDeclaredSymbol(interfaceWithAttribute);
                if (interfaceType == null) continue;
                var interfaceAttributes = interfaceType.GetAttributes();
                var createMemoizedAttributeData = interfaceAttributes.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, createMemoizedAttribute));
                if (createMemoizedAttributeData == null) continue;

                var create = CreateMemoizeInterfaceContext.CreateFromSyntax(interfaceWithAttribute, interfaceType, interfaceModel, createMemoizedAttributeData);
                createAttributes.Add(interfaceType, create);
            }

            var myContext = new GeneratorContext(context, partitionAttribute, slidingCacheAttribute, memoizerFactoryInterface, createMemoizedAttribute, sizeOfResultAttribute, createAttributes);

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
                        myContext.CreateError("Wrong Type", "Generic Arguments not found", name.GetLocation());
                        continue;
                    }

                    if (!MemoizerCall.TryCreate(myContext, addMemoizedScopeCall, interfaceArg, implArg, out var scopedCall))
                        continue;

                    // Same Proj with multiple AddMemoizer() calls for same types
                    if (calls.Any(x => x.IsSameType(scopedCall)))
                    {
                        continue;
                    }

                    calls.Add(scopedCall);

                    var source = MemoizedClass.Generate(scopedCall);

                    context.AddSource(scopedCall.ClassName, source);
                }
            }

            context.AddSource("Memoized_ServiceCollectionExtensions", AddMemoizedExtensionCall.Generate(calls));
        }
    }

    public static class CompilationExtensions
    {
        public static INamedTypeSymbol GetSymbol(this Compilation compilation, string name)
        {
            var createMemoizedAttribute = compilation.GetTypeByMetadataName($"MemoizeSourceGenerator.Attribute.{name}");
            if (createMemoizedAttribute == null)
                throw new Exception($"Could not locate {name}");
            return createMemoizedAttribute;
        }
    }

    public class GeneratorContext
    {
        private readonly IReadOnlyDictionary<ITypeSymbol, CreateMemoizeInterfaceContext> _createMemoizeAttributeContexts;
        public GeneratorExecutionContext Context { get; }
        public INamedTypeSymbol PartitionCacheAttribute { get; }
        public INamedTypeSymbol SlidingCacheAttribute { get; }
        public INamedTypeSymbol MemoizerFactoryInterface { get; }
        public INamedTypeSymbol CreateMemoizedAttribute { get; }
        public INamedTypeSymbol SizeOfResultAttribute { get; }

        public GeneratorContext
        (
            GeneratorExecutionContext context,
            INamedTypeSymbol partitionCacheAttribute,
            INamedTypeSymbol slidingCacheAttribute,
            INamedTypeSymbol memoizerFactoryInterface,
            INamedTypeSymbol createMemoizedAttribute,
            INamedTypeSymbol sizeOfResultAttribute,
            IReadOnlyDictionary<ITypeSymbol, CreateMemoizeInterfaceContext> createMemoizeAttributeContexts
        )
        {
            _createMemoizeAttributeContexts = createMemoizeAttributeContexts;
            Context = context;
            PartitionCacheAttribute = partitionCacheAttribute;
            SlidingCacheAttribute = slidingCacheAttribute;
            MemoizerFactoryInterface = memoizerFactoryInterface;
            CreateMemoizedAttribute = createMemoizedAttribute;
            SizeOfResultAttribute = sizeOfResultAttribute;
        }

        public void ReportDiagnostic(Diagnostic diag)
        {
            Context.ReportDiagnostic(diag);
        }

        public bool TryGetInterfaceContext(ITypeSymbol interfaceType, out CreateMemoizeInterfaceContext o)
        {
            return _createMemoizeAttributeContexts.TryGetValue(interfaceType, out o);
        }
    }

    public class RecieveExtensionCalls : ISyntaxReceiver
    {
        public List<MemberAccessExpressionSyntax> Candidate { get; } = new();
        public List<InterfaceDeclarationSyntax> CandidateAttributes { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            switch (syntaxNode)
            {
                case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                {
                    var methodName = memberAccessExpressionSyntax.Name.Identifier.ValueText;
                    if (methodName == "AddMemoizedScoped" || methodName == "AddMemoizedSingleton")
                    {
                        Candidate.Add(memberAccessExpressionSyntax);
                    }
                    break;
                }
                case InterfaceDeclarationSyntax interfaceDeclarationSyntax when interfaceDeclarationSyntax.AttributeLists.Count > 0:
                    CandidateAttributes.Add(interfaceDeclarationSyntax);
                    break;
            }
        }
    }
}
