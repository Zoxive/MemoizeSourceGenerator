using Microsoft.CodeAnalysis;

namespace MemoizeSourceGenerator
{
    public static class DiagError
    {
        public static DiagnosticDescriptor CreateError(string title, string messageFormat)
        {
            return new DiagnosticDescriptor("Memo0000", title, messageFormat, "MemoizerDISourceGenerator", DiagnosticSeverity.Error, true);
        }
    }
}