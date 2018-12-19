using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;

namespace AnalisadorLogCatch
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AnalisadorLogCatchCodeFixProvider)), Shared]
    public class AnalisadorLogCatchCodeFixProvider : CodeFixProvider
    {
        private const string title = "Incluir Logs";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(AnalisadorLogCatchAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            //encontrando o catch que estava com erro
            var @catch = (CatchClauseSyntax)root.FindNode(diagnosticSpan);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => FixWrongCode(context.Document, @catch, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> FixWrongCode(Document document, CatchClauseSyntax @catch, CancellationToken cancellationToken)
        {
            //procurando o bloco do catch
            var blocoCatch = @catch.DescendantNodes().OfType<BlockSyntax>().First();

            //criando a seguinte expressão : logger.Error(ex, "Erro na classe")
            var chamadaLog = SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("logger"),
                                        SyntaxFactory.IdentifierName("Error")))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                        new SyntaxNodeOrToken[]{
                                            SyntaxFactory.Argument(
                                                SyntaxFactory.IdentifierName("ex")),
                                            SyntaxFactory.Token(
                                                SyntaxFactory.TriviaList(),
                                                SyntaxKind.CommaToken,
                                                SyntaxFactory.TriviaList(
                                                    SyntaxFactory.Space)),
                                            SyntaxFactory.Argument(
                                                SyntaxFactory.LiteralExpression(
                                                    SyntaxKind.StringLiteralExpression,
                                                    SyntaxFactory.Literal("Erro na classe")))}))))
                            .WithSemicolonToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.SemicolonToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.LineFeed)));

            //inserindo o novo código antes do primeiro nó do catch
            var novoBlocoCatch = blocoCatch.InsertNodesBefore(blocoCatch.ChildNodes().First(), new SyntaxList<StatementSyntax>(new StatementSyntax[] { chamadaLog }));

            //formatando (como se fosse CTRL + K + D)
            var novoBlocoCatchFormatado = novoBlocoCatch.WithAdditionalAnnotations(Formatter.Annotation);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(blocoCatch, novoBlocoCatchFormatado);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
