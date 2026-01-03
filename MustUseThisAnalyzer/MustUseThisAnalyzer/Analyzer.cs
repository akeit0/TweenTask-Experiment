using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MustUseThisAnalyzer
{
    internal static class DiagnosticDescriptors
    {
        internal static readonly DiagnosticDescriptor MustUseReturnValue =
            new(
                id: "MUSTUSE001",
                title: "Return value must be used",
                messageFormat:
                "The return value of '{0}' must be used because its return type is annotated with [MustUseThis]",
                category: "Usage",
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor InternalError =
            new(
                id: "MUSTUSE999",
                title: "Analyzer internal error",
                messageFormat: "Analyzer threw an exception: {0}",
                category: "MustUseReturnValue.Analyzer",
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MustUseReturnValueAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DiagnosticDescriptors.MustUseReturnValue,
                DiagnosticDescriptors.InternalError);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // ExpressionStatement (= "Foo();" のような捨て呼び) を解析する
            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ExpressionStatement);
        }

        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            try
            {
                DoAnalyze(context);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InternalError,
                        context.Node.GetLocation(),
                        ex.ToString()));
            }
        }

        private void DoAnalyze(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not ExpressionStatementSyntax ess)
                return;

            // 例: Foo(); / new X(); / obj.Method(); など
            var symbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, ess.Expression, context.CancellationToken)
                .Symbol;
            if (symbol is not IMethodSymbol methodSymbol)
                return;

            if (!IsSupported(methodSymbol))
                return;

            if (!MustUseReturnValueOf(methodSymbol))
                return;

            var display = $"{methodSymbol.ContainingType.Name}.{methodSymbol.Name}";

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.MustUseReturnValue,
                    ess.GetLocation(),
                    display));
        }

        private static bool IsSupported(IMethodSymbol methodSymbol)
        {
            return methodSymbol.MethodKind switch
            {
                MethodKind.Ordinary => true,
                MethodKind.LocalFunction => true,
                MethodKind.ReducedExtension => true,
                _ => false
            };
        }

        private bool MustUseReturnValueOf(IMethodSymbol methodSymbol)
        {
            if (methodSymbol.ReturnsVoid)
                return false;

            // 1) 通常: 戻り値の型に MustUse が付いているか
            var returnType = methodSymbol.ReturnType;
            return HasMustUseAttribute(returnType);
            
        }

        private static bool HasMustUseAttribute(ISymbol symbol)
        {
            foreach (var attributeData in symbol.GetAttributes())
            {
                var attr = attributeData.AttributeClass;
                if (attr?.Name is "MustUseThisAttribute" or "MustUseThis") return true;
            }

            return false;
        }
    }
}