using System.Security.Cryptography;
using System.Text;
using ActorStore.Db;
using Crypto;
using DurableTask.Core;
using DurableTask.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// this is used to run migrate all actor stores in the PDS data directory to latest migration
// this needs a lot of improvements later

string ExpandPath(string path)
{
    if (string.IsNullOrEmpty(path))
        return path;
    
    if (path.StartsWith("~/"))
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
    
    if (path == "~")
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    
    return path;
}



if (args.Length != 1)
{
    // this should be derived from the current migrations state, but we will improve later
    Console.WriteLine("Enter the migration name to reverse too if migration process fails.");
    Console.WriteLine("Usage: dotnet run [MigrationNameToReverse]");
    return;
}
var migrationNameToReverse = args[0];


var dataPath = Environment.GetEnvironmentVariable("PDS_DATA_DIRECTORY");
if (string.IsNullOrEmpty(dataPath))
{
    throw new Exception("PDS_DATA_DIRECTORY environment variable is not set.");
}

dataPath = ExpandPath(dataPath);
var actorBasePath = Path.Join(dataPath, "actors");
if (!Directory.Exists(actorBasePath))
{
    throw new Exception($"Actor base directory does not exist: {actorBasePath}");
}


var actorDirs = Directory.GetDirectories(actorBasePath);
var actorDbPaths = new List<string>();
foreach (var actorHashDir in actorDirs)
{
    var actorDidDir = Directory.GetDirectories(actorHashDir).First();

    var dbPath = Path.Join(actorDidDir, "store.sqlite");
    if (File.Exists(dbPath))
    {
        actorDbPaths.Add(dbPath);
    }
}


var connString = Environment.GetEnvironmentVariable("SQL_DTFX_CONNECTION_STRING");

if (string.IsNullOrEmpty(connString))
{
    throw new Exception("SQL_DTFX_CONNECTION_STRING environment variable is not set.");
}

var settings = new SqlOrchestrationServiceSettings(connString);

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options => options.SingleLine = true);
});

var provider = new SqlOrchestrationService(settings);

await provider.CreateIfNotExistsAsync();


TaskHubWorker hubWorker = await new TaskHubWorker(provider)
    .AddTaskOrchestrations(typeof (MigrateOrchestration))
    .AddTaskActivities(typeof (MigrateActivity))
    .StartAsync();


var client = new TaskHubClient(provider, loggerFactory: loggerFactory);

var instance = await client.CreateOrchestrationInstanceAsync(typeof (MigrateOrchestration), 
    new OrchestrationInput(actorDbPaths.ToArray(), migrationNameToReverse));

await client.WaitForOrchestrationAsync(instance, TimeSpan.FromHours(1));





public record OrchestrationInput(string[] ActorDbPaths, string MigrationNameToReverse);

public class MigrateOrchestration : TaskOrchestration<string, OrchestrationInput>
{
    public override Task<string> RunTask(OrchestrationContext context, OrchestrationInput input)
    {
        var actorDbPaths = input.ActorDbPaths;
        for (int i = 0; i < actorDbPaths.Length; i++)
        {
            var dbPath = actorDbPaths[i];

            try
            {
                var activityInput = new MigrationInput(dbPath, null);
                var result = context.ScheduleTask<MigrationInput>(typeof(MigrateActivity), activityInput);
            }
            catch(Exception ex)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    var dbPathToCompensate = actorDbPaths[j];
                    try
                    {
                        var activityInput = new MigrationInput(dbPathToCompensate, input.MigrationNameToReverse);
                        var result = context.ScheduleTask<MigrationInput>(typeof(MigrateActivity), activityInput);
                    }
                    catch (Exception revertEx)
                    {
                        // log and continue
                        Console.WriteLine($"Failed to revert migration for actor at {dbPathToCompensate}: {revertEx.Message}");
                    }
                }
                throw;
            }
        }

        return Task.FromResult("Migration completed successfully.");
    }

}




public record MigrationInput(string DbPath, string? MigrationName);

public class MigrateActivity : TaskActivity<MigrationInput, string>
{

    protected override string Execute(TaskContext context, MigrationInput input)
    {


        if (!File.Exists(input.DbPath))
        {
            throw new Exception($"Actor database not found at {input.DbPath}");
        }

        var connectionString = $"Data Source={input.DbPath};";

        var options = new DbContextOptionsBuilder<ActorStoreDb>()
            .UseSqlite(connectionString)
            .Options;

        Console.WriteLine($"Migrating actor at {input.DbPath} to {input.MigrationName ?? "latest"}");
        using var db = new ActorStoreDb(options);

        db.Database.Migrate();
        Console.WriteLine($"Migration completed for actor at {input.DbPath}"); 

        return $"Migrated actor at {input.DbPath}";
    }
}
