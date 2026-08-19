using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Solace.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImmediateHandlerCancellationTokenSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor IDE0060Suppression = new(
        id: "IMH001",
        suppressedDiagnosticId: "IDE0060",
        justification: "CancellationToken is required by the Immediate.Handlers HandleAsync method signature."
    );

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [IDE0060Suppression];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Id is not "IDE0060")
            {
                continue;
            }

            var location = diagnostic.Location;
            var tree = location.SourceTree;
            if (tree is null)
            {
                continue;
            }

            var root = tree.GetRoot(context.CancellationToken);
            var node = root.FindNode(location.SourceSpan);

            var parameterNode = node as ParameterSyntax ?? node.FirstAncestorOrSelf<ParameterSyntax>();
            if (parameterNode is null)
            {
                continue;
            }

            if (parameterNode.Parent?.Parent is not MethodDeclarationSyntax methodNode)
            {
                continue;
            }

            if (methodNode.Identifier.Text is not "HandleAsync")
            {
                continue;
            }

            var semanticModel = context.GetSemanticModel(tree);

            if (semanticModel.GetDeclaredSymbol(parameterNode, context.CancellationToken) is not IParameterSymbol parameterSymbol)
            {
                continue;
            }

            if (parameterSymbol.Type is not { Name: "CancellationToken", ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } })
            {
                continue;
            }

            var methodSymbol = parameterSymbol.ContainingSymbol as IMethodSymbol;
            var containingType = methodSymbol?.ContainingType;
            if (containingType == null)
            {
                continue;
            }

            var hasHandlerAttribute = containingType.GetAttributes().Any(attribute => attribute.AttributeClass is { Name: "HandlerAttribute", ContainingNamespace: { Name: "Shared", ContainingNamespace: { Name: "Handlers", ContainingNamespace: { Name: "Immediate", ContainingNamespace.IsGlobalNamespace: true } } } });

            if (!hasHandlerAttribute)
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(IDE0060Suppression, diagnostic));
        }
    }
}
