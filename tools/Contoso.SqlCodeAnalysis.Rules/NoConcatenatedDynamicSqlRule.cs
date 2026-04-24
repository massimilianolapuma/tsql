using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Contoso.SqlCodeAnalysis.Rules;

/// <summary>
/// CT0001: vieta EXEC / EXECUTE su stringhe costruite per concatenazione
/// (es. EXEC(@sql + @userInput) o EXEC('SELECT ... ' + @x)).
/// È il pattern tipico di SQL injection di secondo livello che le SR* non coprono.
///
/// Allow-list: EXEC sp_executesql con parametri tipizzati, oppure EXEC su variabile
/// la cui unica assegnazione è un literal costante.
/// </summary>
[ExportCodeAnalysisRule(
    RuleId,
    "Vieta dynamic SQL costruito per concatenazione",
    Description = "EXEC/EXECUTE su stringhe concatenate (es. EXEC(@sql + @userInput)) è il pattern tipico di SQL injection di secondo livello. Usare sp_executesql con parametri tipizzati.",
    Category = "Contoso.Security",
    RuleScope = SqlRuleScope.Element)]
public sealed class NoConcatenatedDynamicSqlRule : SqlCodeAnalysisRule
{
    /// <summary>Identificativo univoco della regola CT0001.</summary>
    public const string RuleId = "Contoso.Security.CT0001";

    /// <summary>Crea l'istanza della regola e dichiara gli element type analizzati.</summary>
    public NoConcatenatedDynamicSqlRule()
    {
        SupportedElementTypes = new[]
        {
            ModelSchema.Procedure,
            ModelSchema.TableValuedFunction,
            ModelSchema.ScalarFunction,
            ModelSchema.DmlTrigger,
        };
    }

    /// <summary>Analizza il frammento T-SQL del modello e restituisce le violazioni rilevate.</summary>
    public override IList<SqlRuleProblem> Analyze(SqlRuleExecutionContext ctx)
    {
        var problems = new List<SqlRuleProblem>();

        if (ctx.ScriptFragment is not TSqlFragment fragment)
            return problems;

        var visitor = new ExecVisitor();
        fragment.Accept(visitor);

        foreach (var offending in visitor.Findings)
        {
            problems.Add(new SqlRuleProblem(
                description: "Dynamic SQL costruito per concatenazione: usare sp_executesql con parametri tipizzati.",
                modelElement: ctx.ModelElement,
                fragment: offending));
        }

        return problems;
    }

    private sealed class ExecVisitor : TSqlFragmentVisitor
    {
        public List<TSqlFragment> Findings { get; } = new();

        public override void ExplicitVisit(ExecuteStatement node)
        {
            // Caso 1: EXEC(<expr>)  -> ExecuteSpecification.ExecutableEntity è ExecutableStringList.
            // Il parser appiattisce concat di stringhe e variabili in più voci della collection
            // Strings, quindi consideriamo "concatenazione pericolosa" qualsiasi voce che
            // contenga una VariableReference (direttamente o annidata).
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableStringList strList)
            {
                if (strList.Strings.Any(ContainsVariable))
                    Findings.Add(node);
            }

            // Caso 2: EXEC sp_executesql @sql, ... con @sql proveniente da concatenazione
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableProcedureReference procRef &&
                IsSpExecuteSql(procRef) &&
                FirstParamIsConcatenated(procRef))
            {
                Findings.Add(node);
            }

            base.ExplicitVisit(node);
        }

        private static bool IsConcatenation(ScalarExpression expr) =>
            expr is BinaryExpression { BinaryExpressionType: BinaryExpressionType.Add } b &&
            (ContainsVariable(b.FirstExpression) || ContainsVariable(b.SecondExpression));

        private static bool ContainsVariable(ScalarExpression expr) => expr switch
        {
            VariableReference => true,
            BinaryExpression b => ContainsVariable(b.FirstExpression) || ContainsVariable(b.SecondExpression),
            ParenthesisExpression p => ContainsVariable(p.Expression),
            _ => false,
        };

        private static bool IsSpExecuteSql(ExecutableProcedureReference procRef)
        {
            var name = procRef.ProcedureReference?.ProcedureReference?.Name?.BaseIdentifier?.Value;
            return string.Equals(name, "sp_executesql", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool FirstParamIsConcatenated(ExecutableProcedureReference procRef)
        {
            var firstParam = procRef.Parameters?.FirstOrDefault()?.ParameterValue;
            return firstParam is not null && IsConcatenation(firstParam);
        }
    }
}
