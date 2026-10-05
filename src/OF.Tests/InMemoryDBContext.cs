using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OF.Data;
using OF.Data.Database;

namespace OF.Tests;
internal class InMemoryDBContext : ApplicationDbContext
{
    private readonly Random _random = new();

    public InMemoryDBContext(string nameSeed = "")
        : base(new DbContextOptionsBuilder<ApplicationDbContext>()
              .UseInMemoryDatabase(databaseName: "TestDB" + nameSeed + Guid.NewGuid().ToString())
              .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
              .Options)
    {
    }

    internal InMemoryDBContext()
        : this(Guid.NewGuid().ToString())
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VwAssetItem>(entity =>
        {
            entity.HasKey(x => x.Id);
        });
    }

    internal async Task<int> AddOrderHeader(Header header)
    {
        await this.Headers.AddAsync(header);
        await this.SaveChangesAsync();

        return header.Id;
    }

    internal async Task<int> AddHeaderLine(int headerId, Line line)
    {
        Header? header = await this.Headers.FindAsync(headerId);

        if (header == null)
        {
            throw new InvalidOperationException("Header not found");
        }

        line.HeaderId = headerId;
        header.Lines.Add(line);

        await this.Lines.AddAsync(line);
        await this.SaveChangesAsync();

        return line.Id;
    }

    internal async Task<int> UpsertHeaderLine(int headerId, Line line)
    {
        Header? header = await this.Headers.FindAsync(headerId);

        if (header == null)
        {
            throw new InvalidOperationException("Header not found");
        }

        var existingLine = header.Lines.FirstOrDefault(l => l.Id == line.Id);
        if (existingLine != null)
        {
            CopyLineProperties(line, existingLine);
        }
        else
        {
            header.Lines.Add(line);
        }

        var contextLine = await this.Lines.FindAsync(line.Id);
        if (contextLine != null)
        {
            CopyLineProperties(line, contextLine);
        }
        else
        {
            await this.Lines.AddAsync(line);
        }

        await this.SaveChangesAsync();

        return line.Id;
    }

    private void CopyLineProperties(Line source, Line destination)
    {
        foreach (var property in typeof(Line).GetProperties())
        {
            if (property.CanWrite && property.Name != "Id" && property.Name != "HeaderId")
            {
                property.SetValue(destination, property.GetValue(source));
            }
        }
    }
    internal async Task<int> AddReservation(Reservation reservation)
    {
        await this.Reservations.AddAsync(reservation);
        await this.SaveChangesAsync();

        return reservation.Id;
    }

    internal async Task AddReservationRange(List<Reservation> reservations)
    {
        await this.Reservations.AddRangeAsync(reservations);
        await this.SaveChangesAsync();
    }
}
