using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace MemoizeSourceGenerator.Models
{
    public class MemoizedMethodMember
    {
        public static bool TryCreate(GeneratorContext context, Location errorLocation, IMethodSymbol methodSymbol,
            [NotNullWhen(true)] out MemoizedMethodMember? method)
        {
            var @params = methodSymbol.Parameters;
            var args = new List<MemoizedMethodMemberArgument>(@params.Length);

            var attributes = methodSymbol.GetAttributes();

            var slidingCache = SlidingCache.MaybeCreate(context, attributes);

            foreach (var param in @params)
            {
                if (MemoizedMethodMemberArgument.TryCreate(context, errorLocation, param, out var arg))
                {
                    args.Add(arg);
                }
                else
                {
                    method = null;
                    return false;
                }
            }

            method = new MemoizedMethodMember(methodSymbol, args, slidingCache);

            return true;
        }

        private MemoizedMethodMember(IMethodSymbol methodSymbol, IReadOnlyList<MemoizedMethodMemberArgument> parameters, SlidingCache? slidingCache)
        {
            ReturnType = methodSymbol.ReturnType.ToDisplayString();

            Name = methodSymbol.Name;

            PartitionedParameter = parameters.FirstOrDefault(x => x.PartitionsCache);

            var argNames = parameters.Select(x => x.ArgType.Replace('.', '_')).ToArray();

            // TODO better way to generate a class name.s
            var simpleReturnName = ReturnType
                .Replace('.', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('?', '_');
            ClassName = $"ArgKey_{simpleReturnName}_{Name}_{(string.Join("_", argNames))}";

            ReturnsVoid = methodSymbol.ReturnsVoid;

            _lastArg = parameters.LastOrDefault();
            Parameters = parameters;
            SlidingCache = slidingCache;
        }

        public MemoizedMethodMemberArgument? PartitionedParameter { get; }

        public string ClassName { get; }

        private readonly MemoizedMethodMemberArgument? _lastArg;
        public IReadOnlyList<MemoizedMethodMemberArgument> Parameters { get; }
        public SlidingCache? SlidingCache { get; }
        public string Name { get; }
        public bool ReturnsVoid { get; }
        public string ReturnType { get; }

        public void WriteParameters(StringBuilder sb, bool writeType = false, string? prefix = null)
        {
            foreach (var arg in Parameters)
            {
                if (writeType)
                {
                    sb.Append(arg.ArgType);
                    sb.Append(" ");
                }
                if (prefix != null)
                    sb.Append(prefix);
                sb.Append(arg.Name);
                if (!ReferenceEquals(arg, _lastArg))
                    sb.Append(", ");
            }
        }
    }
}