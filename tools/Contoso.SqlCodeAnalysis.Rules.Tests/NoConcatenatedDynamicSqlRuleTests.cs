using System;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;
using Xunit;

namespace Contoso.SqlCodeAnalysis.Rules.Tests;

/// <summary>
/// Test per <see cref="NoConcatenatedDynamicSqlRule"/> (CT0001).
///
/// Strategia: costruiamo un TSqlModel in-memory, ci aggiungiamo lo script T-SQL
/// di volta in volta, eseguiamo il CodeAnalysisService e verifichiamo che la regola
/// emetta il numero atteso di problemi solo per i pattern vulnerabili.
/// </summary>
public sealed class NoConcatenatedDynamicSqlRuleTests
{
    private static CodeAnalysisResult Analyze(string tsql)
    {
        using var model = new TSqlModel(SqlServerVersion.Sql160, new TSqlModelOptions());
        model.AddObjects(tsql);

        var factory = new CodeAnalysisServiceFactory();
        var settings = new CodeAnalysisServiceSettings
        {
            // Eseguiamo solo la nostra regola per isolare l'asserzione.
            RuleSettings = new CodeAnalysisRuleSettings
            {
                DisableRulesNotInSettings = true,
            },
            AssemblyLookupPath = AppContext.BaseDirectory,
        };
        settings.RuleSettings.Add(new RuleConfiguration(NoConcatenatedDynamicSqlRule.RuleId));

        var service = factory.CreateAnalysisService(model, settings);
        return service.Analyze(model);
    }

    [Fact]
    public void Flags_Exec_With_Concatenated_String_And_Variable()
    {
        const string sql = """
            CREATE PROCEDURE dbo.GetUser @name NVARCHAR(100)
            AS
            BEGIN
                EXEC (N'SELECT * FROM dbo.Users WHERE Name = ' + @name);
            END
            """;

        var result = Analyze(sql);

        Assert.Contains(result.Problems, p => p.RuleId == NoConcatenatedDynamicSqlRule.RuleId);
    }

    [Fact]
    public void Flags_Exec_With_MultiVariable_Concatenation()
    {
        // sp_executesql in T-SQL non accetta espressioni concatenate inline come @stmt
        // (il parser le rifiuta), quindi qui copriamo una variante valida ed equivalente
        // dal punto di vista del rischio: EXEC(...) con concatenazione su più variabili.
        const string sql = """
            CREATE PROCEDURE dbo.SearchOrders @prefix NVARCHAR(50), @term NVARCHAR(50)
            AS
            BEGIN
                EXEC (@prefix + N'SELECT * FROM dbo.Orders WHERE Notes = ' + @term);
            END
            """;

        var result = Analyze(sql);

        Assert.Contains(result.Problems, p => p.RuleId == NoConcatenatedDynamicSqlRule.RuleId);
    }

    [Fact]
    public void Does_Not_Flag_Parameterized_SpExecuteSql()
    {
        const string sql = """
            CREATE PROCEDURE dbo.SearchOrdersSafe @term NVARCHAR(50)
            AS
            BEGIN
                EXEC sp_executesql
                    N'SELECT * FROM dbo.Orders WHERE Notes LIKE @t',
                    N'@t NVARCHAR(50)',
                    @t = @term;
            END
            """;

        var result = Analyze(sql);

        Assert.DoesNotContain(result.Problems, p => p.RuleId == NoConcatenatedDynamicSqlRule.RuleId);
    }

    [Fact]
    public void Does_Not_Flag_Static_Exec_With_Literal_Only()
    {
        const string sql = """
            CREATE PROCEDURE dbo.RebuildIndexes
            AS
            BEGIN
                EXEC ('ALTER INDEX ALL ON dbo.Orders REBUILD');
            END
            """;

        var result = Analyze(sql);

        Assert.DoesNotContain(result.Problems, p => p.RuleId == NoConcatenatedDynamicSqlRule.RuleId);
    }
}
