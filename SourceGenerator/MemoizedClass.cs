using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SourceGenerator
{
    internal static class MemoizedClass
    {
        public static string Generate(ScopedMemoizerCall scopedCall)
        {
            var fullInterfaceName = scopedCall.InterfaceType.ToDisplayString();

            var sb = new StringBuilder(@$"using Microsoft.Extensions.Caching.Memory;

namespace {scopedCall.Namespace}
{{
    public class {scopedCall.ClassName} : {fullInterfaceName}
    {{
");
            sb.AppendLine($"\t\tprivate readonly {fullInterfaceName} _impl;");
            sb.AppendLine($"\t\tprivate readonly IMemoryCache _cache;");
            sb.AppendLine($"\t\tpublic {scopedCall.ClassName}(IMemoryCache cache, {fullInterfaceName} impl)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t_cache = cache;");
            sb.AppendLine("\t\t\t_impl = impl;");
            sb.AppendLine("\t\t}");

            var methods= scopedCall.InterfaceType.GetMembers().OfType<IMethodSymbol>();
            foreach (var method in methods)
            {
                var returnType = method.ReturnsVoid? "void" : method.ReturnType.ToDisplayString();
                var methodName = method.Name;

                sb.Append($"\t\tpublic {returnType} {methodName}(");

                var lastArg = method.Parameters.LastOrDefault();
                foreach (var arg in method.Parameters)
                {
                    sb.Append(arg.ToDisplayString());
                    sb.Append(" ");
                    sb.Append(arg.Name);

                    if (!ReferenceEquals(arg, lastArg))
                        sb.Append(", ");
                }
                sb.AppendLine(")");
                sb.AppendLine("\t\t{");

                //
                sb.Append("\t\t\treturn _impl.");
                sb.Append(methodName);
                sb.Append("(");
                foreach (var arg in method.Parameters)
                {
                    sb.Append(arg.Name);

                    if (!ReferenceEquals(arg, lastArg))
                        sb.Append(", ");
                }
                sb.AppendLine(");");
                //

                sb.AppendLine("\t\t}");
            }

            sb.AppendLine("\t}");

            // TODO generate ArgStructs

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}