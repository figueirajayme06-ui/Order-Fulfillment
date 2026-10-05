using Microsoft.EntityFrameworkCore;
using OF.Common.Infrastructure.MDP.Models;
using OF.Data.Database;

namespace OF.Common.Infrastructure.MDP
{
    public class FDPDbContext : DbContext
    {
        public FDPDbContext(DbContextOptions<FDPDbContext> options) : base(options)
        {
        }

        public virtual DbSet<UserLanguage> UserLanguages { get; set; }

        public virtual DbSet<ProductRule> ProductRules { get; set; }

        public virtual DbSet<ConfigurationRule> ConfigurationRules { get; set; }

        public virtual DbSet<ErrorCondition> ErrorConditions { get; set; }

        public virtual DbSet<SummaryVariable> SummaryVariables { get; set; }

        public virtual DbSet<ProductAction> ProductActions { get; set; }

        public virtual DbSet<ConfigurationField> ConfigurationFields { get; set; }

        public virtual DbSet<ConfiguredProduct> ConfiguredProducts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserLanguage>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("user_pkey");

                entity.ToTable("user");
            });

            modelBuilder.Entity<ProductRule>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("sbqq__productrule__c_pkey");

                entity.ToTable("sbqq__productrule__c");
            });

            modelBuilder.Entity<ConfigurationRule>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("sbqq__configurationrule__c_pkey");

                entity.ToTable("sbqq__configurationrule__c");
            });

            modelBuilder.Entity<ErrorCondition>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("sbqq__errorcondition__c_pkey");

                entity.ToTable("sbqq__errorcondition__c");
            });

            modelBuilder.Entity<SummaryVariable>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("sbqq__summaryvariable__c_pkey");

                entity.ToTable("sbqq__summaryvariable__c");
            });

            modelBuilder.Entity<ProductAction>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("sbqq__summaryvariable__c_pkey");

                entity.ToTable("sbqq__productaction__c");
            });

            modelBuilder.Entity<ConfigurationField>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("sbqq__configurationattribute__c_pkey");

                entity.ToTable("sbqq__configurationattribute__c");
            });

            modelBuilder.Entity<ConfiguredProduct>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("product2_pkey");

                entity.ToTable("product2");
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}

