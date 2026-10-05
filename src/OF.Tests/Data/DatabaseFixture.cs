using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OF.Data;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using OF.Tests.Data.Design.dbo;

public class DatabaseFixture : IDisposable
{
    private bool testDBISAssumedValid = false;
    private string TestConnectionString = "Server=(localdb)\\mssqllocaldb;Database={0};Trusted_Connection=True;MultipleActiveResultSets=true";
    private readonly List<string> _createdDatabases = new List<string>();

    public DatabaseFixture()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")))
        {
            testDBISAssumedValid = true;
            TestConnectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")!;
        }
    }

    public IDbContextFactory<ApplicationDbContext> SetupDbContext(string databaseSuffix)
    {
        IServiceCollection services = new ServiceCollection();

        if (!testDBISAssumedValid)
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "We are not running on Windows so no LocalDB, skipping.");
        }

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var testHash = Math.Abs(databaseSuffix.GetHashCode()).ToString("X8");
        var shortName = databaseSuffix.Length > 30 ?
            databaseSuffix.Substring(0, 30) :
            databaseSuffix;

        var databaseName = $"dbof_{shortName.ToLower()}_{testHash}_{uniqueId}";

        if (databaseName.Length > 120)
        {
            databaseName = $"dbof_{testHash}_{uniqueId}";
        }

        CreateAndApplyTables(databaseName);

        lock (_createdDatabases)
        {
            _createdDatabases.Add(databaseName);
        }

        services.AddDbContextFactory<ApplicationDbContext>(optionsAction: (dbContextOptions) => dbContextOptions.UseSqlServer(string.Format(TestConnectionString, databaseName)));

        return services.BuildServiceProvider().GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    }

    public void Dispose()
    {
        if (testDBISAssumedValid && _createdDatabases.Any())
        {
            using var master = new SqlConnection(string.Format(TestConnectionString, "master"));
            master.Open();

            foreach (var dbName in _createdDatabases)
            {
                    ExecuteQuery($"IF EXISTS (SELECT 1 FROM sys.databases WHERE name = '{dbName}') DROP DATABASE {dbName};", master);
            }
        }
    }

    private void CreateAndApplyTables(string databaseName)
    {
        int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var master = new SqlConnection(string.Format(TestConnectionString, "master"));
                master.Open();
                ExecuteQuery($"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '{databaseName}') CREATE DATABASE {databaseName};", master);

                using var app = new SqlConnection(string.Format(TestConnectionString, databaseName));
                app.Open();

                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Lines)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Lines)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Headers)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Headers)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.DataRefresh)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.DataRefresh)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Reservations)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Reservations)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Assets)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Assets)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Alerts)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Alerts)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Notes)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Notes)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.WarehouseItems)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.WarehouseItems)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_ItemAttributeValue)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_ItemAttributeValue)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_LineAttributePurpose)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_LineAttributePurpose)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_RelatedSpecificSubstitution)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_RelatedSpecificSubstitution)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_GenericSubstitution)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_GenericSubstitution)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_Attribute)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_Attribute)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_Purpose)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_Purpose)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.AttributeLanguage)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.AttributeLanguage)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_Service)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_Service)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_Item)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_Item)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_Generic)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_Generic)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_Line)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_Line)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.CPQ_Family)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.CPQ_Family)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.AuditLog)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.AuditLog)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Ringfences)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Ringfences)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.RingfenceItems)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.RingfenceItems)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.Users)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.Users)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.ProcessingErrors)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.ProcessingErrors)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.ProductItemsSyncList)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.ProductItemsSyncList)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.ProductItems)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.ProductItems)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Tables.ProductItems_Staging)}', 'U') IS NOT NULL DROP TABLE {nameof(Tables.ProductItems_Staging)};", app);
                ExecuteTableQuery($"IF OBJECT_ID('{nameof(Views.vwHeaders)}', 'V') IS NOT NULL DROP VIEW {nameof(Views.vwHeaders)};", app);

                ExecuteTableQuery($@"
                    IF EXISTS (SELECT * FROM sys.partition_schemes WHERE name = N'AuditLogPS')
                    BEGIN
                        DROP PARTITION SCHEME AuditLogPS
                    END", app);

                ExecuteTableQuery($@"
                    IF EXISTS (SELECT * FROM sys.partition_functions WHERE name = N'AuditLogTableNamePF')
                    BEGIN
                        DROP PARTITION FUNCTION AuditLogTableNamePF
                    END", app);

                ExecuteTableQuery("WAITFOR DELAY '00:00:01';", app);

                ExecuteTableQuery(Tables.AuditLogPartitionFunction, app);
                ExecuteTableQuery(Tables.AuditLogPartitionScheme, app);
                ExecuteTableQuery(Tables.Headers, app);
                ExecuteTableQuery(Tables.Lines, app);
                ExecuteTableQuery(Tables.DataRefresh, app);
                ExecuteTableQuery(Tables.Reservations, app);
                ExecuteTableQuery(Tables.Assets, app);
                ExecuteTableQuery(Tables.Alerts, app);
                ExecuteTableQuery(Tables.Notes, app);
                ExecuteTableQuery(Tables.WarehouseItems, app);
                ExecuteTableQuery(Tables.CPQ_Family, app);
                ExecuteTableQuery(Tables.CPQ_Line, app);
                ExecuteTableQuery(Tables.CPQ_Generic, app);
                ExecuteTableQuery(Tables.CPQ_Item, app);
                ExecuteTableQuery(Tables.CPQ_Service, app);
                ExecuteTableQuery(Tables.ProductItemsSyncList, app);
                ExecuteTableQuery(Tables.ProductItems, app);
                ExecuteTableQuery(Tables.ProductItems_Staging, app);
                ExecuteTableQuery(Tables.ProcessingErrors, app);
                ExecuteTableQuery(Tables.AuditLog, app);
                ExecuteTableQuery(Tables.AuditReservations, app);
                ExecuteTableQuery(Tables.Ringfences, app);
                ExecuteTableQuery(Tables.RingfenceItems, app);
                ExecuteTableQuery(Tables.Users, app);
                ExecuteTableQuery(Views.vwHeaders, app);
                ExecuteTableQuery(Tables.AttributeLanguage, app);
                ExecuteTableQuery(Tables.CPQ_GenericSubstitution, app);
                ExecuteTableQuery(Tables.CPQ_RelatedSpecificSubstitution, app);
                ExecuteTableQuery(Tables.CPQ_Attribute, app);
                ExecuteTableQuery(Tables.CPQ_Purpose, app);
                ExecuteTableQuery(Tables.CPQ_LineAttributePurpose, app);
                ExecuteTableQuery(Tables.CPQ_ItemAttributeValue, app);

                ExecuteFunctionSql("AttributeStringToTable", app);
                ExecuteFunctionSql("GetFulfilmentGenericsWithSubstitutions", app);

                var spDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Design", "dbo", "StoredProcedures");
                if (Directory.Exists(spDir))
                {
                    foreach (var spFile in Directory.GetFiles(spDir, "*.sql"))
                    {
                        var spName = Path.GetFileNameWithoutExtension(spFile);
                        ExecuteTableQuery($"IF OBJECT_ID('dbo.{spName}', 'P') IS NOT NULL DROP PROCEDURE [dbo].[{spName}];", app);
                        ExecuteTableQuery(File.ReadAllText(spFile), app);
                    }
                }

                break;
            }
            catch when (attempt < maxRetries && testDBISAssumedValid)
            {
                Thread.Sleep(5000 * attempt);
            }
        }
    }


    private void ExecuteQuery(string sql, SqlConnection master)
    {
        using (SqlCommand command = new SqlCommand(sql, master))
        {
            command.ExecuteNonQuery();
        }
    }

    private void ExecuteTableQuery(string sql, SqlConnection app)
    {
        using (SqlCommand command = new SqlCommand(sql.Replace("GO", ""), app))
        {
            command.ExecuteNonQuery();
        }
    }

    private void ExecuteFunctionSql(string functionName, SqlConnection app)
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data",
            "Design",
            "dbo",
            "General",
            $"{functionName}.sql");

        ExecuteTableQuery($"IF OBJECT_ID('dbo.{functionName}') IS NOT NULL DROP FUNCTION [dbo].[{functionName}];", app);
        ExecuteTableQuery(File.ReadAllText(path), app);
    }
}

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{

}
