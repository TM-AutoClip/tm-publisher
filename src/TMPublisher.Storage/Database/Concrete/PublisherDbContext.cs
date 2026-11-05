using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;

namespace TMPublisher.Storage.Database;

/// <summary>
/// The encrypted database (SQLite3 Multiple Ciphers; the whole file is encrypted, table names included). One initial schema created with
/// EnsureCreated: there are no migrations before the first public data (no backward compatibility yet).
/// </summary>
public sealed class PublisherDbContext(DbContextOptions<PublisherDbContext> options) : DbContext(options)
{
    /// <summary>Accounts.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Account secrets.</summary>
    public DbSet<CredentialRow> Credentials => Set<CredentialRow>();

    /// <summary>Groups.</summary>
    public DbSet<Group> Groups => Set<Group>();

    /// <summary>Posts.</summary>
    public DbSet<Post> Posts => Set<Post>();

    /// <summary>Post targets.</summary>
    public DbSet<PostTarget> Targets => Set<PostTarget>();

    /// <summary>Settings documents.</summary>
    public DbSet<SettingRow> Settings => Set<SettingRow>();

    /// <summary>Default tag sets.</summary>
    public DbSet<TagSet> TagSets => Set<TagSet>();

    /// <summary>Publish log.</summary>
    public DbSet<PublishLogRow> PublishLog => Set<PublishLogRow>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        var json = StorageJsonContext.Default;
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Platform);
        });
        modelBuilder.Entity<CredentialRow>(e =>
        {
            e.ToTable("account_secrets");
            e.HasKey(c => c.AccountId);
        });
        modelBuilder.Entity<Group>(e =>
        {
            e.ToTable("groups");
            e.HasKey(g => g.Id);
            e.Property(g => g.AccountIds).HasConversion(JsonColumn.Converter(json.ListGuid), JsonColumn.Comparer(json.ListGuid));
        });
        modelBuilder.Entity<Post>(e =>
        {
            e.ToTable("posts");
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.Status, p.ScheduledAtUtc });
            e.Property(p => p.Tags).HasConversion(JsonColumn.Converter(json.ListString), JsonColumn.Comparer(json.ListString));
            e.Property(p => p.Media).HasConversion(JsonColumn.Converter(json.ListMediaItem), JsonColumn.Comparer(json.ListMediaItem));
            e.HasMany(p => p.Targets).WithOne().HasForeignKey(t => t.PostId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PostTarget>(e =>
        {
            e.ToTable("post_targets");
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.AccountId);
            e.Property(t => t.Keywords).HasConversion(JsonColumn.Converter(json.ListString), JsonColumn.Comparer(json.ListString));
            e.Property(t => t.Options).HasConversion(JsonColumn.Converter(json.DictionaryStringString), JsonColumn.Comparer(json.DictionaryStringString));
        });
        modelBuilder.Entity<SettingRow>(e =>
        {
            e.ToTable("settings");
            e.HasKey(s => s.Key);
        });
        modelBuilder.Entity<TagSet>(e =>
        {
            e.ToTable("tag_sets");
            e.HasKey(t => t.Platform);
            e.Property(t => t.Tags).HasConversion(JsonColumn.Converter(json.ListString), JsonColumn.Comparer(json.ListString));
        });
        modelBuilder.Entity<PublishLogRow>(e =>
        {
            e.ToTable("publish_log");
            e.HasKey(l => l.Id);
            e.HasIndex(l => new { l.AccountId, l.AtUtc });
        });
    }
}
