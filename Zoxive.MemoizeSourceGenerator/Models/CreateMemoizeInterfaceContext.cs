using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zoxive.MemoizeSourceGenerator.Models
{
    public class CreateMemoizeInterfaceContext
    {
        public InterfaceDeclarationSyntax? InterfaceWithAttribute { get; }
        public ITypeSymbol InterfaceType { get; }
        public SemanticModel? InterfaceModel { get; }
        public AttributeData CreateMemoizedAttributeData { get; }

        public Location ErrorLocation { get; }

        public static CreateMemoizeInterfaceContext CreateFromSyntax(InterfaceDeclarationSyntax interfaceWithAttribute, ITypeSymbol interfaceType, SemanticModel interfaceModel, AttributeData createMemoizedAttributeData)
        {
            return new CreateMemoizeInterfaceContext(interfaceWithAttribute, interfaceType, interfaceModel, createMemoizedAttributeData, interfaceWithAttribute.GetLocation());
        }

        public static CreateMemoizeInterfaceContext CreateFromType(ITypeSymbol interfaceType, AttributeData createMemoizedAttributeData, Location errorLocation)
        {
            return new CreateMemoizeInterfaceContext(null, interfaceType, null, createMemoizedAttributeData, errorLocation);
        }

        private CreateMemoizeInterfaceContext
        (
            InterfaceDeclarationSyntax? interfaceWithAttribute,
            ITypeSymbol interfaceType,
            SemanticModel? interfaceModel,
            AttributeData createMemoizedAttributeData,
            Location errorLocation
        )
        {
            InterfaceWithAttribute = interfaceWithAttribute;
            InterfaceType = interfaceType;
            InterfaceModel = interfaceModel;
            CreateMemoizedAttributeData = createMemoizedAttributeData;

            ErrorLocation = errorLocation;
        }
    }
}