using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace InjectCodeGen
{
    /// <summary>
    /// Source Generator that creates lazy getter properties for fields marked with [Inject] attribute.
    /// </summary>
    [Generator]
    public class InjectSourceGenerator : IIncrementalGenerator
    {
        /// <summary>
        /// Checks if the attribute is our InjectAttribute (in global namespace).
        /// Excludes attributes from other libraries like AdvancedSceneManager.
        /// </summary>
        internal static bool IsOurInjectAttribute(INamedTypeSymbol? attributeClass)
        {
            if (attributeClass == null) return false;

            var name = attributeClass.Name;
            if (name != "InjectAttribute" && name != "Inject") return false;

            // Exclude AdvancedSceneManager namespace
            var ns = attributeClass.ContainingNamespace?.ToDisplayString() ?? "";
            if (ns.StartsWith("AdvancedSceneManager"))
                return false;

            return true;
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find classes with fields that have [Inject] attribute
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsCandidateClass(s),
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m is not null);

            // Combine with compilation
            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            // Generate source
            context.RegisterSourceOutput(compilationAndClasses,
                static (spc, source) => Execute(source.Left, source.Right!, spc));
        }

        /// <summary>
        /// Quick check if a class might have [Inject] fields.
        /// </summary>
        private static bool IsCandidateClass(SyntaxNode node)
        {
            if (node is not ClassDeclarationSyntax classDeclaration)
                return false;

            // Check if class is partial
            if (!classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                return false;

            // Check if any field has [Inject] attribute
            foreach (var member in classDeclaration.Members)
            {
                if (member is FieldDeclarationSyntax field)
                {
                    foreach (var attributeList in field.AttributeLists)
                    {
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var name = attribute.Name.ToString();
                            if (name == "Inject" || name == "InjectAttribute")
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Semantic analysis to confirm the [Inject] attribute.
        /// </summary>
        private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            foreach (var member in classDeclaration.Members)
            {
                if (member is FieldDeclarationSyntax field)
                {
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (fieldSymbol == null) continue;

                        foreach (var attribute in fieldSymbol.GetAttributes())
                        {
                            if (IsOurInjectAttribute(attribute.AttributeClass))
                            {
                                return classDeclaration;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Execute source generation.
        /// </summary>
        private static void Execute(Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax?> classes,
            SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
                return;

            // Remove duplicates
            var distinctClasses = classes.Where(c => c != null).Cast<ClassDeclarationSyntax>().Distinct();

            foreach (var classDeclaration in distinctClasses)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                if (classSymbol == null)
                    continue;

                var source = GeneratePartialClass(classSymbol, classDeclaration, semanticModel);

                if (!string.IsNullOrEmpty(source))
                {
                    // Use "Global" for global namespace, otherwise use the namespace
                    var ns = classSymbol.ContainingNamespace;
                    string namespacePrefix = (ns == null || ns.IsGlobalNamespace)
                        ? "Global"
                        : ns.ToDisplayString().Replace(".", "_");
                    var fileName = $"{namespacePrefix}_{classSymbol.Name}.g.cs";
                    context.AddSource(fileName, SourceText.From(source!, Encoding.UTF8));
                }
            }
        }

        /// <summary>
        /// Generate partial class source.
        /// </summary>
        private static string? GeneratePartialClass(INamedTypeSymbol classSymbol,
            ClassDeclarationSyntax classDeclaration,
            SemanticModel semanticModel)
        {
            var injectFields = new List<InjectFieldInfo>();

            // Collect fields with [Inject] attribute
            foreach (var member in classDeclaration.Members)
            {
                if (member is not FieldDeclarationSyntax field)
                    continue;

                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                    if (fieldSymbol == null) continue;

                    var injectAttribute = fieldSymbol.GetAttributes()
                        .FirstOrDefault(a => IsOurInjectAttribute(a.AttributeClass));

                    if (injectAttribute == null)
                        continue;

                    // Extract key value
                    string key = "";

                    // Extract key from constructor argument
                    if (injectAttribute.ConstructorArguments.Length > 0)
                    {
                        var keyArg = injectAttribute.ConstructorArguments[0];
                        if (keyArg.Value is string keyValue)
                            key = keyValue;
                    }

                    // Extract Key from named argument
                    foreach (var namedArg in injectAttribute.NamedArguments)
                    {
                        if (namedArg.Key == "Key" && namedArg.Value.Value is string keyNamedValue)
                            key = keyNamedValue;
                    }

                    // Extract field accessibility
                    var fieldAccessibility = fieldSymbol.DeclaredAccessibility switch
                    {
                        Accessibility.Public => "public",
                        Accessibility.Internal => "internal",
                        Accessibility.Private => "private",
                        Accessibility.Protected => "protected",
                        Accessibility.ProtectedOrInternal => "protected internal",
                        Accessibility.ProtectedAndInternal => "private protected",
                        _ => "private"
                    };

                    injectFields.Add(new InjectFieldInfo
                    {
                        FieldName = fieldSymbol.Name,
                        FieldType = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        Key = key,
                        Accessibility = fieldAccessibility
                    });
                }
            }

            if (injectFields.Count == 0)
                return null;

            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable disable");
            sb.AppendLine();

            // namespace
            var namespaceName = classSymbol.ContainingNamespace?.ToDisplayString();
            bool hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";

            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            // Class declaration (including access modifier and inheritance info)
            var accessibility = classSymbol.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => "internal"
            };

            var indent = hasNamespace ? "    " : "";
            sb.AppendLine($"{indent}{accessibility} partial class {classSymbol.Name}");
            sb.AppendLine($"{indent}{{");

            // Generate lazy getter property for each [Inject] field
            foreach (var field in injectFields)
            {
                // Generate property name (remove leading _ and convert to PascalCase with 'd' prefix)
                var propertyName = GetPropertyName(field.FieldName);

                sb.AppendLine();
                sb.AppendLine($"{indent}    // Auto-generated lazy injection property for {field.FieldName}");
                sb.AppendLine($"{indent}    {field.Accessibility} {field.FieldType} {propertyName}");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        get");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            if ({field.FieldName} == null)");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                {field.FieldName} = global::DIContainer.GetValue(typeof({field.FieldType}), \"{field.Key}\") as {field.FieldType};");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}            return {field.FieldName};");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}    }}");
            }

            // Generate ClearInjectCache method
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// Clears cached dependency injection values.");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected void ClearInjectCache()");
            sb.AppendLine($"{indent}    {{");

            foreach (var field in injectFields)
            {
                sb.AppendLine($"{indent}        {field.FieldName} = null;");
            }

            sb.AppendLine($"{indent}    }}");

            sb.AppendLine($"{indent}}}");

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate property name from field name.
        /// _fieldName -> dFieldName
        /// fieldName -> dFieldName
        /// </summary>
        internal static string GetPropertyName(string fieldName)
        {
            // Remove leading underscores
            var trimmed = fieldName.TrimStart('_');

            if (string.IsNullOrEmpty(trimmed))
                return fieldName;

            // Add 'd' prefix and capitalize first letter
            return "d" + char.ToUpper(trimmed[0]) + trimmed.Substring(1);
        }

        /// <summary>
        /// Check if property name has the recommended 'd' prefix.
        /// </summary>
        internal static bool HasRecommendedPrefix(string propertyName)
        {
            return propertyName.StartsWith("d") && propertyName.Length > 1 && char.IsUpper(propertyName[1]);
        }

        private class InjectFieldInfo
        {
            public string FieldName { get; set; } = "";
            public string FieldType { get; set; } = "";
            public string Key { get; set; } = "";
            public string Accessibility { get; set; } = "private";
        }
    }

    /// <summary>
    /// Analyzer that reports errors and warnings for [Inject] field usage.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InjectFieldAccessAnalyzer : DiagnosticAnalyzer
    {
        public const string DirectAccessDiagnosticId = "LDI001";
        public const string TypeNameCollisionDiagnosticId = "LDI002";

        private static readonly LocalizableString DirectAccessTitle = "Direct access to [Inject] field is not allowed";
        private static readonly LocalizableString DirectAccessMessageFormat = "Direct access to field '{0}' is not allowed. Use the generated property '{1}' instead.";
        private static readonly LocalizableString DirectAccessDescription = "Fields with [Inject] attribute must be accessed through auto-generated properties only.";

        private static readonly LocalizableString TypeNameCollisionTitle = "Generated property name matches type name";
        private static readonly LocalizableString TypeNameCollisionMessageFormat = "Generated property '{0}' has the same name as its type '{1}'. Consider using a prefix like 'd' (e.g., 'd{0}') to avoid confusion.";
        private static readonly LocalizableString TypeNameCollisionDescription = "When the generated property name matches the type name, it can cause confusion. Using a prefix like 'd' is recommended.";

        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor DirectAccessRule = new DiagnosticDescriptor(
            DirectAccessDiagnosticId,
            DirectAccessTitle,
            DirectAccessMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: DirectAccessDescription);

        private static readonly DiagnosticDescriptor TypeNameCollisionRule = new DiagnosticDescriptor(
            TypeNameCollisionDiagnosticId,
            TypeNameCollisionTitle,
            TypeNameCollisionMessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: TypeNameCollisionDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DirectAccessRule, TypeNameCollisionRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        }

        private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                if (fieldSymbol == null) continue;

                // Check if field has [Inject] attribute
                var hasInjectAttribute = fieldSymbol.GetAttributes()
                    .Any(a => InjectSourceGenerator.IsOurInjectAttribute(a.AttributeClass));

                if (!hasInjectAttribute)
                    continue;

                // Get the generated property name
                var propertyName = InjectSourceGenerator.GetPropertyName(fieldSymbol.Name);

                // Get the type name (without namespace, without generic parameters)
                var typeName = fieldSymbol.Type.Name;

                // Check if property name matches type name (case-insensitive comparison)
                if (string.Equals(propertyName, typeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Property name matches type name - this can cause confusion
                    var diagnostic = Diagnostic.Create(
                        TypeNameCollisionRule,
                        variable.GetLocation(),
                        propertyName,
                        typeName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
        {
            var identifierName = (IdentifierNameSyntax)context.Node;

            // Get symbol info
            var symbolInfo = context.SemanticModel.GetSymbolInfo(identifierName);
            if (symbolInfo.Symbol is not IFieldSymbol fieldSymbol)
                return;

            // Check if field has [Inject] attribute
            var hasInjectAttribute = fieldSymbol.GetAttributes()
                .Any(a => InjectSourceGenerator.IsOurInjectAttribute(a.AttributeClass));

            if (!hasInjectAttribute)
                return;

            // Allow access from within generated property getter
            var containingMethod = identifierName.Ancestors()
                .OfType<AccessorDeclarationSyntax>()
                .FirstOrDefault();

            if (containingMethod != null)
            {
                // Check if inside getter
                var propertyDecl = containingMethod.Ancestors()
                    .OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault();

                if (propertyDecl != null)
                {
                    var propertyName = propertyDecl.Identifier.Text;
                    var expectedPropertyName = InjectSourceGenerator.GetPropertyName(fieldSymbol.Name);

                    // Allow access from generated property getter
                    if (propertyName == expectedPropertyName)
                        return;
                }
            }

            // Allow access from ClearInjectCache method
            var containingMethodDecl = identifierName.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (containingMethodDecl?.Identifier.Text == "ClearInjectCache")
                return;

            // Allow access in field declaration (initialization etc.)
            var fieldDecl = identifierName.Ancestors()
                .OfType<FieldDeclarationSyntax>()
                .FirstOrDefault();

            if (fieldDecl != null)
            {
                // Check if this is the same field's declaration
                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    if (variable.Identifier.Text == fieldSymbol.Name)
                        return;
                }
            }

            // Report error for other cases
            var propertyNameSuggestion = InjectSourceGenerator.GetPropertyName(fieldSymbol.Name);
            var diagnostic = Diagnostic.Create(DirectAccessRule, identifierName.GetLocation(), fieldSymbol.Name, propertyNameSuggestion);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
