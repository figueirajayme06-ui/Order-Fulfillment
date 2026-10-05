using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using OF.Data;
using OF.Data.Database;
using OF.UI.Database;

namespace OF.Tests.UI.Database;

public class SavedViewRepositoryTests
{
    [Fact]
    public void AddViewWithRecipients_NormalizesAndPersistsRecipientsWithTheView()
    {
        using var context = CreateContext();
        AddUsers(context, "one@example.com", "two@example.com");
        var repository = CreateRepository(context);
        var view = CreateView();

        repository.AddViewWithRecipients(
            view,
            [" one@example.com ", "ONE@example.com", "", "two@example.com"]);

        context.ChangeTracker.Clear();
        var persisted = repository.GetViewWithRecipients(view.Id);
        persisted.ViewRecipients.Select(recipient => recipient.RecipientLoginName)
            .Should().BeEquivalentTo("one@example.com", "two@example.com");
        persisted.ViewRecipients.Should().OnlyContain(recipient => recipient.Recipient != null);
    }

    [Fact]
    public void UpdateViewWithRecipients_ReconcilesRecipientsInOneSave()
    {
        using var context = CreateContext();
        AddUsers(context, "one@example.com", "two@example.com", "three@example.com");
        var repository = CreateRepository(context);
        var view = repository.AddViewWithRecipients(
            CreateView(),
            ["one@example.com", "two@example.com"]);
        context.ChangeTracker.Clear();
        var persisted = repository.GetViewWithRecipients(view.Id);

        repository.UpdateViewWithRecipients(
            persisted,
            [" ONE@example.com ", "three@example.com"]);

        context.ChangeTracker.Clear();
        repository.GetViewWithRecipients(view.Id).ViewRecipients
            .Select(recipient => recipient.RecipientLoginName)
            .Should().BeEquivalentTo("one@example.com", "three@example.com");
    }

    [Fact]
    public void UpdateViewWithRecipients_StaleWritersReplaceCurrentSetWithoutMerging()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        int viewId;
        using (var setupContext = CreateContext(databaseName, databaseRoot))
        {
            AddUsers(
                setupContext,
                "one@example.com",
                "two@example.com",
                "three@example.com");
            viewId = CreateRepository(setupContext)
                .AddViewWithRecipients(CreateView(), ["one@example.com"])
                .Id;
        }

        using var firstContext = CreateContext(databaseName, databaseRoot);
        using var secondContext = CreateContext(databaseName, databaseRoot);
        var firstRepository = CreateRepository(firstContext);
        var secondRepository = CreateRepository(secondContext);
        var firstStaleView = firstRepository.GetView(viewId);
        var secondStaleView = secondRepository.GetView(viewId);

        firstRepository.UpdateViewWithRecipients(firstStaleView, ["two@example.com"]);
        secondRepository.UpdateViewWithRecipients(secondStaleView, ["three@example.com"]);

        using var assertionContext = CreateContext(databaseName, databaseRoot);
        CreateRepository(assertionContext).GetViewWithRecipients(viewId).ViewRecipients
            .Select(recipient => recipient.RecipientLoginName)
            .Should().Equal("three@example.com");
    }

    [Fact]
    public void UpdateViewWithRecipients_EmptySelectionClearsRecipientLinks()
    {
        using var context = CreateContext();
        AddUsers(context, "one@example.com");
        var repository = CreateRepository(context);
        var view = repository.AddViewWithRecipients(CreateView(), ["one@example.com"]);
        context.ChangeTracker.Clear();
        var persisted = repository.GetViewWithRecipients(view.Id);

        repository.UpdateViewWithRecipients(persisted, []);

        context.ChangeTracker.Clear();
        repository.GetViewWithRecipients(view.Id).ViewRecipients.Should().BeEmpty();
    }

    [Fact]
    public void UpdateView_LegacyMutationPreservesRecipientLinksItCannotRepresent()
    {
        using var context = CreateContext();
        AddUsers(context, "one@example.com");
        var repository = CreateRepository(context);
        var view = repository.AddViewWithRecipients(CreateView(), ["one@example.com"]);
        context.ChangeTracker.Clear();
        var persisted = repository.GetView(view.Id);

        repository.UpdateView(persisted);

        context.ChangeTracker.Clear();
        repository.GetViewWithRecipients(view.Id).ViewRecipients.Should().ContainSingle();
    }

    [Fact]
    public void DeleteUser_CascadesRecipientLinkWithoutDeletingView()
    {
        using var context = CreateContext();
        AddUsers(context, "recipient@example.com");
        var repository = CreateRepository(context);
        var view = repository.AddViewWithRecipients(CreateView(), ["recipient@example.com"]);
        var recipient = context.Users.Single(user => user.LoginName == "recipient@example.com");

        repository.DeleteUser(recipient);

        context.ViewRecipients.Should().BeEmpty();
        context.Views.Should().ContainSingle(existing => existing.Id == view.Id);
    }

    [Fact]
    public void DeleteView_CascadesAllRecipientLinks()
    {
        using var context = CreateContext();
        AddUsers(context, "recipient@example.com");
        var repository = CreateRepository(context);
        var view = repository.AddViewWithRecipients(CreateView(), ["recipient@example.com"]);

        repository.DeleteView(view);

        context.Views.Should().BeEmpty();
        context.ViewRecipients.Should().BeEmpty();
        context.Users.Should().ContainSingle();
    }

    [Fact]
    public void Model_UsesCompositeKeyAndCascadeForeignKeys()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ViewRecipient));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().Equal(nameof(ViewRecipient.ViewId), nameof(ViewRecipient.RecipientLoginName));
        var foreignKeys = entity.GetForeignKeys().ToList();
        foreignKeys.Should().HaveCount(2)
            .And.OnlyContain(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        foreignKeys.Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(View)
            && foreignKey.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(ViewRecipient.ViewId) })
            && foreignKey.GetConstraintName() == "FK_ViewRecipients_Views_ViewId");
        foreignKeys.Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(User)
            && foreignKey.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(ViewRecipient.RecipientLoginName) })
            && foreignKey.GetConstraintName() == "FK_ViewRecipients_Users_RecipientLoginName");

        entity.GetIndexes().Should().ContainSingle(index =>
            index.GetDatabaseName() == "IX_ViewRecipients_RecipientLoginName"
            && index.Properties.Select(property => property.Name).SequenceEqual(new[] {
                nameof(ViewRecipient.RecipientLoginName),
                nameof(ViewRecipient.ViewId) }));
    }

    [Fact]
    public void Schema_DefinesCompositeKeyCascadeForeignKeysAndRecipientLookupIndex()
    {
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Design",
            "SchemaContracts",
            "ViewRecipients.sql");
        var schema = File.ReadAllText(schemaPath);

        schema.Should().Contain("CREATE TABLE [dbo].[ViewRecipients]");
        schema.Should().Contain(
            "CONSTRAINT [PK_ViewRecipients] PRIMARY KEY CLUSTERED ([ViewId] ASC, [RecipientLoginName] ASC)");
        schema.Should().Contain(
            "CONSTRAINT [FK_ViewRecipients_Views_ViewId] FOREIGN KEY ([ViewId]) REFERENCES [dbo].[Views] ([Id]) ON DELETE CASCADE");
        schema.Should().Contain(
            "CONSTRAINT [FK_ViewRecipients_Users_RecipientLoginName] FOREIGN KEY ([RecipientLoginName]) REFERENCES [dbo].[Users] ([LoginName]) ON DELETE CASCADE");
        schema.Should().Contain("CREATE NONCLUSTERED INDEX [IX_ViewRecipients_RecipientLoginName]");
        schema.Should().Contain("ON [dbo].[ViewRecipients]([RecipientLoginName] ASC, [ViewId] ASC)");
    }

    private static ApplicationDbContext CreateContext(
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(
                databaseName ?? Guid.NewGuid().ToString(),
                databaseRoot ?? new InMemoryDatabaseRoot())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DataRepository CreateRepository(ApplicationDbContext context) => new(
        context,
        new MemoryCache(new MemoryCacheOptions()),
        TimeProvider.System);

    private static View CreateView() => new()
    {
        Name = "Shared view",
        Owner = "owner@example.com",
        ViewJson = "{}",
    };

    private static void AddUsers(ApplicationDbContext context, params string[] loginNames)
    {
        context.Users.AddRange(loginNames.Select(loginName => new User
        {
            LoginName = loginName,
            FullName = loginName,
            Division = "UK",
            DateFormat = "dd/MM/yyyy",
        }));
        context.SaveChanges();
    }
}
