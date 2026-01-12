using System;
using Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ActorStore.Db;

public class ActorDbDesignFactory : IDesignTimeDbContextFactory<ActorStoreDb>
{
    // https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli
    // This is only used for design time tools like ef migrations
    public ActorStoreDb CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ActorStoreDb>();
        optionsBuilder.UseSqlite("Data Source=stubactorstore.db");

        return new ActorStoreDb(optionsBuilder.Options);
    }
}
