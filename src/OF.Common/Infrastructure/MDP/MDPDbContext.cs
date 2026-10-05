using Microsoft.EntityFrameworkCore;
using OF.Data.Database;

namespace OF.Common.Infrastructure.MDP
{
    public class MDPDbContext : DbContext
    {
        public MDPDbContext(DbContextOptions<MDPDbContext> options) : base(options)
        {
        }

        public virtual DbSet<ProductHierarchyStaging> ProductHierarchy { get; set; }

        public virtual DbSet<WarehouseItemsStaging> OrganisationalHierarchy { get; set; }
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductHierarchyStaging>(entity =>
            {
                entity.HasKey(e => e.IndividualItemNumber).HasName("[PK_ProductHierarchy_Staging]");

                entity.ToTable("ProductHierarchy");
            });

            modelBuilder.Entity<WarehouseItemsStaging>(entity =>
            {
                entity.HasKey(e => e.WarehouseCode).HasName("[PK_OrganisationalHierarchy_Staging]");

                entity.ToTable("OrganisationalHierarchy");
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}
