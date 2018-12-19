using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AnalisadorLogCatch
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AnalisadorLogCatchAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "AnalisadorLogCatch";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Boas práticas";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            //registrando para analisar TODOS os blocos de código
            context.RegisterCodeBlockAction(AnalyseCodeBlockAction);
        }

        private static void AnalyseCodeBlockAction(CodeBlockAnalysisContext context)
        {

            //Procurando diretivas try no código
            foreach (var @try in context.CodeBlock.DescendantNodes().OfType<TryStatementSyntax>())
            {
                //Procurando todos os catchs do try
                foreach (var @catch in @try.Catches)
                {
                    var declaracaoCatch = @catch.DescendantNodes().OfType<CatchDeclarationSyntax>().FirstOrDefault();

                    string nomeVariavelException = null;

                    //se não há nada no catch ja reporta erro no código
                    if (declaracaoCatch == null)
                    {
                        var diagnostic = Diagnostic.Create(Rule, @catch.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                        continue;
                    }
                    else
                    {
                        //procurando por variáveis no catch (se estiver somente com o catch sem a váriavel já reporta erro)
                        // Ex: catch(Exception){     =====> ERRADO
                        var declaracaoVariavel = declaracaoCatch.ChildTokens().FirstOrDefault(x => x.Kind() == SyntaxKind.IdentifierToken);
                        if (declaracaoVariavel == null || string.IsNullOrEmpty(declaracaoVariavel.Text))
                        {
                            var diagnostic = Diagnostic.Create(Rule, @catch.GetLocation());
                            context.ReportDiagnostic(diagnostic);

                            continue;
                        }
                        else
                        {
                            //separo o nome da váriavel para posteriormente verificar se ela foi utilizada no log
                            nomeVariavelException = declaracaoVariavel.Text;
                        }
                    }

                    var chamadasMetodos = @catch.DescendantNodes().OfType<ExpressionStatementSyntax>();

                    bool encontrouTratamentoEsperado = false;

                    //procurando todas as chamadas de métodos dentro do catch
                    foreach (var chamadaMetodo in chamadasMetodos)
                    {
                        var acessoMembro = chamadaMetodo.Expression as InvocationExpressionSyntax;

                        if(acessoMembro != null)
                        {
                            //encontrado uma chamada para algum método
                            var identificadores = acessoMembro.DescendantNodes().OfType<IdentifierNameSyntax>();

                            if(identificadores.Count() > 1)
                            {
                                //Obtendo o nome do método chamado (dos identificadores é o que fica na posição abaixo)
                                var identificadorChamadaMetodo = identificadores.ElementAt(1);

                                if (identificadorChamadaMetodo.Identifier.ToFullString().Contains("Error") ||
                                    identificadorChamadaMetodo.Identifier.ToFullString().Contains("Warn") ||
                                    identificadorChamadaMetodo.Identifier.ToFullString().Contains("Fatal"))
                                {

                                    //procurando pelos parâmetros passados no métodos (um deles TEM que ser a variavel do catch)
                                    var parametrosMetodo = acessoMembro.DescendantNodes().OfType<ArgumentSyntax>()
                                        .SelectMany(x => x.DescendantTokens()).Where(x => x.Kind() == SyntaxKind.IdentifierToken);

                                    if (parametrosMetodo.Any(x => x.Text == nomeVariavelException))
                                    {
                                        encontrouTratamentoEsperado = true;
                                    }
                                }
                            }
                        }
                    }

                    //se não encontrou o tratamento reporta o erro no catch
                    if (!encontrouTratamentoEsperado)
                    {
                        var diagnostic = Diagnostic.Create(Rule, @catch.GetLocation());

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
