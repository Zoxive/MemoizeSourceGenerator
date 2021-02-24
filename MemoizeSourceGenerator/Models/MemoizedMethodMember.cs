using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using MemoizeSourceGenerator.Attribute;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemoizeSourceGenerator.Models
{
    public class MemoizedMethodSizeOfFunction
    {
        private readonly string? _selfMethodName;
        private readonly string? _globalStaticMethodName;

        public MemoizedMethodSizeOfFunction(string? selfMethodName, string? globalStaticMethodName)
        {
            _selfMethodName = selfMethodName;
            _globalStaticMethodName = globalStaticMethodName;
        }

        public void Write(StringBuilder sb, string result)
        {
            if (_selfMethodName != null)
            {
                sb.AppendLine($"{result}.{_selfMethodName}();");
            }
            else if (_globalStaticMethodName != null)
            {
                sb.AppendLine($"{_globalStaticMethodName}({result});");
            }
        }
    }

    public class MemoizedMethodMember
    {
        public static bool TryCreate(GeneratorContext context, CreateMemoizeInterfaceContext interfaceContext, IMethodSymbol methodSymbol, /*[NotNullWhen(true)]*/ out MemoizedMethodMember? method)
        {
            var @params = methodSymbol.Parameters;
            var args = new List<MemoizedMethodMemberArgument>(@params.Length);

            var attributes = methodSymbol.GetAttributes();

            var slidingCache = SlidingCache.MaybeCreate(context, attributes);

            foreach (var param in @params)
            {
                if (MemoizedMethodMemberArgument.TryCreate(context, interfaceContext.ErrorLocation, param, out var arg))
                {
                    args.Add(arg);
                }
                else
                {
                    method = null;
                    return false;
                }
            }

            var returnType = methodSymbol.ReturnType;
            var isAsync = methodSymbol.IsTaskOfTOrValueTaskOfT();

            bool typeIsNullable;
            bool typeIsReferenceType;
            ITypeSymbol typeInCache;

            if (isAsync)
            {
                if (returnType is not INamedTypeSymbol {IsGenericType: true} namedTypeSymbol || namedTypeSymbol.TypeArguments.Length != 1)
                {
                    context.CreateError("Async return types must return something", $"Expected 1 generic type argument for {methodSymbol.Name}", interfaceContext.ErrorLocation);
                    method = null;
                    return false;
                }

                var taskReturnObj = namedTypeSymbol.TypeArguments[0];

                // TODO check it has SizeOf()
                // we dont care if they are IEquatable, but we do care they implement .SizeOf() at somepoint
                /*
                if (!taskReturnObj.AllInterfaces.Any(x => x.MetadataName == "IEquatable`1"))
                {
                    context.CreateError("Return types must implement IEquatable", $"Async return type Task<{taskReturnObj.Name}> does not implement IEquatable", interfaceContext.ErrorLocation);
                    method = null;
                    return false;
                }
                */

                typeInCache = taskReturnObj;
                typeIsNullable = taskReturnObj.NullableAnnotation == NullableAnnotation.Annotated;

                typeIsReferenceType = taskReturnObj.IsReferenceType;
            }
            else
            {
                typeInCache = methodSymbol.ReturnType;
                typeIsNullable = methodSymbol.ReturnType.NullableAnnotation == NullableAnnotation.Annotated;

                typeIsReferenceType = methodSymbol.ReturnType.IsReferenceType;
            }

            var returnTypeAttributes = methodSymbol.GetReturnTypeAttributes();

            string? globalSizeOfMethod = null;
            string? selfSizeOfMethod = null;

            // TODO [Attribute] set on the Interface that allows you to specify a class that can calculate this for you
            // Check TypeInCache has .SizeOfInBytes() method
            // Check for Attribute
            var sizeOfAttributeData = returnTypeAttributes.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, context.SizeOfResultAttribute));

            if (sizeOfAttributeData == null && context.GlobalSizeOfAttribute == SizeOfAttributeData.Empty)
            {
                var sizeOfInBytesMethod = typeInCache.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(x => x.Name == "SizeOfInBytes" && x.ReturnType.IsLong());
                if (sizeOfInBytesMethod == null)
                {
                    context.CreateError("Missing SizeOfInBytes function", $"Return type '{typeInCache.ToDisplayString()}' must have a 'public long SizeOfInBytes()' function or use SizeOfResultAttribute.", interfaceContext.ErrorLocation);
                    method = null;
                    return false;
                }
                else
                {
                    selfSizeOfMethod = "SizeOfInBytes";
                }
            }
            else
            {
                if (sizeOfAttributeData == null)
                {
                    if (context.GlobalSizeOfAttribute == SizeOfAttributeData.Empty)
                    {
                        context.CreateError("Missing SizeOfInBytes function", $"Return type '{typeInCache.ToDisplayString()}' must have a 'public long SizeOfInBytes()' function or use SizeOfResultAttribute.", interfaceContext.ErrorLocation);
                        method = null;
                        return false;
                    }

                    (globalSizeOfMethod, selfSizeOfMethod) = context.GlobalSizeOfAttribute;
                }
                else
                {
                    (globalSizeOfMethod, selfSizeOfMethod) = SizeOfAttributeData.Parse(sizeOfAttributeData);
                }
            }

            var returnTypeSizeOfMethod = new MemoizedMethodSizeOfFunction(selfSizeOfMethod, globalSizeOfMethod);

            method = new MemoizedMethodMember(methodSymbol, args, slidingCache, isAsync, returnType.ToDisplayString(), typeInCache, typeIsNullable, typeIsReferenceType, returnTypeSizeOfMethod);

            return true;
        }

        private MemoizedMethodMember(IMethodSymbol methodSymbol, IReadOnlyList<MemoizedMethodMemberArgument> parameters, SlidingCache? slidingCache,
            bool isAsync, string returnType, ITypeSymbol typeInCache, bool typeCanBeNull, bool typeIsReferenceType, MemoizedMethodSizeOfFunction memoizedMethodSizeOfFunction)
        {
            Name = methodSymbol.Name;
            IsAsync = isAsync;
            ReturnType = returnType;
            TypeInCache = typeInCache;
            TypeCanBeNull = typeCanBeNull;
            TypeIsReferenceType = typeIsReferenceType;
            MemoizedMethodSizeOfFunction = memoizedMethodSizeOfFunction;

            PartitionedParameter = parameters.FirstOrDefault(x => x.PartitionsCache);

            // TODO better way to generate a class name.s
            static string Fix(string sr)
            {
                return sr.Replace('.', '_')
                .Replace("<", "")
                .Replace(">", "")
                .Replace("?", "")
                .Replace(",", "")
                .Replace(" ", "");
            }

            var argNames = parameters.Select(x => Fix(x.ArgType)).ToArray();

            var methodsClassName = Fix(methodSymbol.ContainingType.Name);

            var simpleReturnName = Fix(ReturnType);
            ClassName = $"ArgKey_{methodsClassName}_{simpleReturnName}_{Name}_{(string.Join("_", argNames))}";

            SimpleName = $"{methodsClassName}.{Name}";

            ReturnsVoid = methodSymbol.ReturnsVoid;

            _lastArg = parameters.LastOrDefault();
            Parameters = parameters;
            SlidingCache = slidingCache;
        }

        public string SimpleName { get; }

        public MemoizedMethodMemberArgument? PartitionedParameter { get; }

        public string ClassName { get; }

        private readonly MemoizedMethodMemberArgument? _lastArg;

        public IReadOnlyList<MemoizedMethodMemberArgument> Parameters { get; }
        public SlidingCache? SlidingCache { get; }
        public string Name { get; }
        public bool ReturnsVoid { get; }
        public bool IsAsync { get; }
        public string ReturnType { get; }

        /// <summary>
        /// This will be the same as ReturnType, unless its a Task<> method then its the value inside the Task.
        /// </summary>
        public ITypeSymbol TypeInCache { get; }

        public bool TypeCanBeNull { get; }
        public bool TypeIsReferenceType { get; }
        public MemoizedMethodSizeOfFunction MemoizedMethodSizeOfFunction { get; }

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