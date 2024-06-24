using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace VendaIngressosCinema;

public class IngressosContext : DbContext
{
    public DbSet<Ingresso> Ingressos { get; init; }

    public IngressosContext(DbContextOptions options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Ingresso>().ToCollection("ingressos");
    }
}