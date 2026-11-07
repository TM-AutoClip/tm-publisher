using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;

namespace TMPublisher.Storage.Database;

/// <summary>
/// The encrypted database (SQLite3 Multiple Ciphers; the whole file is encrypted, table names included). The model is the one of every
/// version so far: EF Core made it in 0.1.0–0.1.2, 0.1.3–0.1.5 wrote the same tables by hand, so every existing file opens unchanged. Values:
/// ids are upper-case GUID text (EF Core's SQLite form), times are UTC ticks (INTEGER), states are numbers, flags 0/1, lists and maps are
/// source-generated JSON text. There are no migrations: the one schema is made by <see cref="Schema.EnsureAsync"/>.
/// </summary>
public sealed class PublisherDbContext : DbContext
{
    /// <summary>A context with the given options (<see cref="PublisherDbContextFactory"/>).</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "EF Core's own assemblies are not trimmed in the published program (IsTrimmable=false in TMPublisher.Desktop.csproj), "
            + "so nothing EF Core reaches by reflection inside itself is removed; the entity types are kept by the annotations of DbSet<T> and "
            + "ModelBuilder.Entity<T>; this model hands EF Core instances, not types to create. Every store operation was run in a trimmed build.")]
    public PublisherDbContext(DbContextOptions<PublisherDbContext> options)
        : base(options)
    {
    }

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
    /// <remarks>Composite indexes are named by their properties: an anonymous-type expression is not safe for trimming.</remarks>
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
            e.Property(g => g.AccountIds).HasConversion(new JsonValueConverter<List<Guid>>(json.ListGuid), new JsonValueComparer<List<Guid>>(json.ListGuid));
        });
        modelBuilder.Entity<Post>(e =>
        {
            e.ToTable("posts");
            e.HasKey(p => p.Id);
            e.HasIndex(nameof(Post.Status), nameof(Post.ScheduledAtUtc));
            e.Property(p => p.Tags).HasConversion(new JsonValueConverter<List<string>>(json.ListString), new JsonValueComparer<List<string>>(json.ListString));
            e.Property(p => p.Media)
                .HasConversion(new JsonValueConverter<List<MediaItem>>(json.ListMediaItem), new JsonValueComparer<List<MediaItem>>(json.ListMediaItem));
            e.HasMany(p => p.Targets).WithOne().HasForeignKey(t => t.PostId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PostTarget>(e =>
        {
            e.ToTable("post_targets");
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.AccountId);
            e.Property(t => t.Keywords).HasConversion(new JsonValueConverter<List<string>>(json.ListString), new JsonValueComparer<List<string>>(json.ListString));
            e.Property(t => t.Options).HasConversion(new JsonValueConverter<Dictionary<string, string>>(json.DictionaryStringString),
                new JsonValueComparer<Dictionary<string, string>>(json.DictionaryStringString));
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
            e.Property(t => t.Tags).HasConversion(new JsonValueConverter<List<string>>(json.ListString), new JsonValueComparer<List<string>>(json.ListString));
        });
        modelBuilder.Entity<PublishLogRow>(e =>
        {
            e.ToTable("publish_log");
            e.HasKey(l => l.Id);
            e.HasIndex(nameof(PublishLogRow.AccountId), nameof(PublishLogRow.AtUtc));
        });

        // Every time (DateTimeOffset and DateTimeOffset?) is stored as UTC ticks. One converter instance, not a converter type: EF Core would
        // create a type by reflection, and the trimmed program does not keep its constructor for that.
        var ticks = new UtcTicksConverter();
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
        {
            property.SetValueConverter(ticks);
        }
    }
}
