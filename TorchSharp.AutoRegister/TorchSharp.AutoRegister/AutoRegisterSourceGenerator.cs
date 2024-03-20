using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TorchSharp.AutoRegister;

[Generator(LanguageNames.CSharp)]
public class AutoRegisterSourceGenerator : IIncrementalGenerator
{
    private record struct Model(
        string Namespace, string ClassName, 
        string FieldName, string FieldType,
        string PropertyName);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context =>
            context.AddSource("AutoRegisteredAttribute.g.cs", SourceText.From(
                """
                using System;
                namespace TorchSharp.AutoRegister
                {
                    [AttributeUsage(AttributeTargets.Field)]
                    public sealed partial class AutoRegisteredAttribute : Attribute
                    {
                    }
                }
                """, Encoding.UTF8)));

        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "TorchSharp.AutoRegister.AutoRegisteredAttribute",
            predicate: (_, _) => true,
            transform: (context, _) =>
            {
                var format = SymbolDisplayFormat.FullyQualifiedFormat
                    .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);
                var symbol = (IFieldSymbol)context.TargetSymbol;
                return new Model(
                    Namespace: symbol.ContainingType.ContainingNamespace?.ToDisplayString(
                        format.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                    ClassName: symbol.ContainingType.Name,
                    FieldName: symbol.Name,
                    FieldType: symbol.Type.ToDisplayString(format),
                    PropertyName: $"{char.ToUpperInvariant(symbol.Name[0])}{symbol.Name.Substring(1)}");
            }
        );

        context.RegisterSourceOutput(pipeline, (context, model) =>
        {
            using var text = new StringWriter();
            using var writer = new IndentedTextWriter(text);

            writer.WriteLine($"namespace {model.Namespace};");
            writer.WriteLine($"partial class {model.ClassName}");
            writer.WriteLine($"{{");
            writer.Indent++;
            {
                writer.WriteLine($"public {model.FieldType} {model.PropertyName}");
                writer.WriteLine($"{{");
                writer.Indent++;
                {
                    writer.WriteLine($"get => this.{model.FieldName};");
                    writer.WriteLine($"init");
                    writer.WriteLine($"{{");
                    writer.Indent++;
                    {
                        writer.WriteLine($"this.{model.FieldName} = value;");
                        writer.WriteLine($"if (this._internal_submodules.ContainsKey(\"{model.FieldName}\"))");
                        writer.Indent++;
                        writer.WriteLine($"_ = this._internal_submodules.Remove(\"{model.FieldName}\");");
                        writer.Indent--;
                        writer.WriteLine($"this.register_module(\"{model.FieldName}\", this.{model.FieldName});");
                    }
                    writer.Indent--;
                    writer.WriteLine($"}}");
                }
                writer.Indent--;
                writer.WriteLine($"}}");
            }
            writer.Indent--;
            writer.WriteLine($"}}");

            var sourceText = SourceText.From(text.ToString(), Encoding.UTF8);

            context.AddSource($"{model.ClassName}_{model.FieldName}.g.cs", sourceText);
        });
    }
}
