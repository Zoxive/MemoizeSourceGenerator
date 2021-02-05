using Microsoft.CodeAnalysis;

namespace MemoizeSourceGenerator
{
    public static class DiagError
    {
        public static Diagnostic CreateError(string title, string messageFormat, Location location)
        {
            var descriptor = new DiagnosticDescriptor("Memo0000", title, messageFormat, "MemoizerDISourceGenerator", DiagnosticSeverity.Error, true);

            return Diagnostic.Create(descriptor, location, DiagnosticSeverity.Error);
        }

        public static void CreateError(this GeneratorContext context, string title, string messageFormat, Location location)
        {
            context.ReportDiagnostic(CreateError(title, messageFormat, location));
        }
    }
}