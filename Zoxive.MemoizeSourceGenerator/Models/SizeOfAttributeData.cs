using System.Linq;
using Microsoft.CodeAnalysis;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace Zoxive.MemoizeSourceGenerator.Models
{
    public class SizeOfAttributeData
    {
        public static readonly SizeOfAttributeData Empty = new SizeOfAttributeData(null, null);

        public string? GlobalSizeOfMethod { get; }
        public string? SelfSizeOfMethod { get; }

        private SizeOfAttributeData(string? globalSizeOfMethod, string? selfSizeOfMethod)
        {
            GlobalSizeOfMethod = globalSizeOfMethod;
            SelfSizeOfMethod = selfSizeOfMethod;
        }

        public void Deconstruct(out string? globalSizeOfMethod, out string? selfSizeOfMethod)
        {
            globalSizeOfMethod = GlobalSizeOfMethod;
            selfSizeOfMethod = SelfSizeOfMethod;
        }

        public static SizeOfAttributeData Parse(AttributeData? sizeOfAttributeData)
        {
            if (sizeOfAttributeData == null)
                return Empty;

            string? globalSizeOfMethod = null;
            string? selfSizeOfMethod = null;

            var selfSizeOfMethodName = sizeOfAttributeData.NamedArguments.FirstOrDefault(x => x.Key == nameof(SizeOfResultAttribute.SizeOfMethodName)).Value;
                if (!selfSizeOfMethodName.IsNull)
            {
                selfSizeOfMethod = selfSizeOfMethodName.Value?.ToString();
            }
            var globalStaticMethodName = sizeOfAttributeData.NamedArguments.FirstOrDefault(x => x.Key == nameof(SizeOfResultAttribute.GlobalStaticMethod)).Value;
                if (!globalStaticMethodName.IsNull)
            {
                globalSizeOfMethod = globalStaticMethodName.Value?.ToString();
            }

            if (globalSizeOfMethod == null && selfSizeOfMethod == null) return Empty;

            return new SizeOfAttributeData(globalSizeOfMethod, selfSizeOfMethod);
        }
    }
}