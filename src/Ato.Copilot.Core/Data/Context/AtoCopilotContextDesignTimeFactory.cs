using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ato.Copilot.Core.Data.Context;

/// <summary>
/// Design-time factory for <see cref="AtoCopilotContext"/>.
/// Used by EF Core tools (dotnet ef migrations) when the application
/// cannot be started due to missing service registrations.
/// Always uses SQLite for migration generation.
/// </summary>
public class AtoCopilotContextDesignTimeFactory : IDesignTimeDbContextFactory<AtoCopilotContext>
{
    /// <inheritdoc />
    public AtoCopilotContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AtoCopilotContext>();
        optionsBuilder.UseSqlite("Data Source=ato-copilot-design.db");
        return new AtoCopilotContext(optionsBuilder.Options);
    }
}
