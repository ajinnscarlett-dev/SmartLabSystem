using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartLab.Server;
using Xunit;

namespace SmartLab.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTLAB_TEST_SQLSERVER")))
            Skip = "SQL Server integration NOT RUN: set SMARTLAB_TEST_SQLSERVER to an isolated test server.";
    }
}

public sealed class SqlServerOwnershipTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task MigrationsAndSimultaneousClaimsEnforceOwnershipAndRollbackHistory()
    {
        await using var database = new TestDatabase();
        await using (var db = database.Open())
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            db.Users.AddRange(new User { Username = "sql-one", PasswordHash = "hash", Role = "Student" }, new User { Username = "sql-two", PasswordHash = "hash", Role = "Student" });
            db.PCs.AddRange(new PC { PCNumber = "SQL-PC01" }, new PC { PCNumber = "SQL-PC02" });
            await db.SaveChangesAsync();
        }
        int[] users, pcs;
        await using (var db = database.Open())
        {
            users = await db.Users.OrderBy(u => u.UserId).Select(u => u.UserId).ToArrayAsync();
            pcs = await db.PCs.OrderBy(p => p.PCId).Select(p => p.PCId).ToArrayAsync();
        }
        await using (var first = database.Open())
        await using (var second = database.Open())
        {
            var a = (await first.PCs.FindAsync(pcs[0]))!;
            var b = (await second.PCs.FindAsync(pcs[1]))!;
            a.CurrentUserId = users[0]; a.Status = "Occupied";
            b.CurrentUserId = users[0]; b.Status = "Occupied";
            var outcomes = await Task.WhenAll(TrySave(first), TrySave(second));
            Assert.Single(outcomes, success => success);
        }
        await using (var db = database.Open())
        {
            Assert.Equal(1, await db.PCs.CountAsync(p => p.CurrentUserId == users[0]));
            Assert.Equal(1, await db.PcUsageHistory.CountAsync(s => s.LogoutTime == null));
            var owner = await db.PCs.SingleAsync(p => p.CurrentUserId == users[0]);
            owner.Status = "Offline";
            await db.SaveChangesAsync();
        }
        // Fresh contexts emulate process recovery: ownership and the open session are persistent.
        await using (var db = database.Open())
        {
            var pc = await db.PCs.SingleAsync(p => p.CurrentUserId == users[0]);
            Assert.Equal("Offline", pc.Status);
            Assert.Null((await db.PcUsageHistory.SingleAsync()).LogoutTime);
            pc.CurrentUserId = null; pc.Status = "Available";
            await db.SaveChangesAsync();
        }
        await using (var first = database.Open())
        await using (var second = database.Open())
        {
            var a = (await first.PCs.FindAsync(pcs[0]))!;
            var b = (await second.PCs.FindAsync(pcs[0]))!;
            a.CurrentUserId = users[0]; a.Status = "Occupied";
            b.CurrentUserId = users[1]; b.Status = "Occupied";
            var outcomes = await Task.WhenAll(TrySave(first), TrySave(second));
            Assert.Single(outcomes, success => success);
        }
        await using (var db = database.Open())
        {
            Assert.Equal(1, await db.PCs.CountAsync(p => p.CurrentUserId != null));
            Assert.Equal(1, await db.PcUsageHistory.CountAsync(s => s.LogoutTime == null));
            Assert.Equal(2, await db.PcUsageHistory.CountAsync());
        }
    }

    private static async Task<bool> TrySave(AppDbContext db)
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627)) { return false; }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string name = "SmartLab_HardeningTest_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        public TestDatabase()
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SMARTLAB_TEST_SQLSERVER"));
            builder.InitialCatalog = name; // Never use or delete a caller-supplied database.
            connection = builder.ConnectionString;
        }
        public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
        public async ValueTask DisposeAsync()
        {
            var actual = new SqlConnectionStringBuilder(connection).InitialCatalog;
            if (actual != name || !actual.StartsWith("SmartLab_HardeningTest_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test cleanup target.");
            await using var db = Open();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
