using Microsoft.EntityFrameworkCore;
using OF.Data.Database;

namespace OF.Common.Infrastructure.CPQ
{
    public class CPQDbContext : DbContext
    {
        public CPQDbContext(DbContextOptions<CPQDbContext> options) : base(options)
        {
        }

        public virtual DbSet<CpqAttributeStaging> CPQ_Attribute { get; set; }

        public virtual DbSet<CpqAttributeLookupStaging> CPQ_AttributeLookup { get; set; }

        public virtual DbSet<CpqFamilyStaging> CPQ_Family { get; set; }

        public virtual DbSet<CpqGenericStaging> CPQ_Generic { get; set; }

        public virtual DbSet<CpqGenericRatingStaging> CPQ_GenericRating { get; set; }

        public virtual DbSet<CpqGenericSubstitutionStaging> CPQ_GenericSubstitution { get; set; }

        public virtual DbSet<CpqGenericToGenericStaging> CPQ_GenericToGeneric { get; set; }

        public virtual DbSet<CpqItemStaging> CPQ_Item { get; set; }

        public virtual DbSet<CpqItemAttributeValueStaging> CPQ_ItemAttributeValue { get; set; }

        public virtual DbSet<CpqLineStaging> CPQ_Line { get; set; }

        public virtual DbSet<CpqLineAttributePurposeStaging> CPQ_LineAttributePurpose { get; set; }

        public virtual DbSet<CpqLineSellingRuleStaging> CPQ_LineSellingRule { get; set; }

        public virtual DbSet<CpqPurposeStaging> CPQ_Purpose { get; set; }

        public virtual DbSet<CpqRegionStaging> CPQ_Region { get; set; }

        public virtual DbSet<CpqRegionCurrencyStaging> CPQ_RegionCurrency { get; set; }

        public virtual DbSet<CpqRegionGenericStaging> CPQ_RegionGeneric { get; set; }

        public virtual DbSet<CpqRelatedSpecificSubstitutionStaging> CPQ_RelatedSpecificSubstitution { get; set; }

        public virtual DbSet<CpqServiceStaging> CPQ_Service { get; set; }

        public virtual DbSet<CpqVersionStaging> CPQ_Version { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CpqAttributeStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_Attirbute_PK");

                entity.ToTable("CPQ_Attribute");

                entity.HasIndex(e => e.AttributeDescription, "CPQ_AttributeDescription_UC").IsUnique();

                entity.HasIndex(e => e.Cpqattribute, "CPQ_Attribute_UC").IsUnique();

                entity.Property(e => e.AttributeDescription).HasMaxLength(40);
                entity.Property(e => e.AttributeName).HasMaxLength(100);
                entity.Property(e => e.Cpqattribute)
                    .HasMaxLength(40)
                    .HasColumnName("CPQAttribute");
                entity.Property(e => e.DataType).HasMaxLength(6);
                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqAttributeLookupStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_AttributeLookup_PK");

                entity.ToTable("CPQ_AttributeLookup");

                entity.Property(e => e.Value).HasMaxLength(100);
            });

            modelBuilder.Entity<CpqFamilyStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_Family_PK");

                entity.ToTable("CPQ_Family");

                entity.Property(e => e.FamilyDescription).HasMaxLength(50);
            });

            modelBuilder.Entity<CpqGenericStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_Generic_PK");

                entity.ToTable("CPQ_Generic");

                entity.HasIndex(e => e.GenericCode, "CPQ_Generic_UC").IsUnique();

                entity.HasIndex(e => e.LineId, "IX_CPQ_Generic_LineId");

                entity.Property(e => e.AmperageLimit)
                    .HasMaxLength(100)
                    .HasColumnName("Amperage_Limit");
                entity.Property(e => e.CableM3itemNumber)
                    .HasMaxLength(100)
                    .HasColumnName("Cable_M3ItemNumber");
                entity.Property(e => e.CableSizeAwg)
                    .HasMaxLength(10)
                    .HasColumnName("Cable_Size_AWG");
                entity.Property(e => e.CableSizeMm)
                    .HasMaxLength(10)
                    .HasColumnName("Cable_Size_mm");
                entity.Property(e => e.CableType).HasMaxLength(100);
                entity.Property(e => e.Conductors).HasMaxLength(100);
                entity.Property(e => e.ConfigurationType).HasMaxLength(100);
                entity.Property(e => e.CpqSequence).HasColumnName("CPQ_Sequence");
                entity.Property(e => e.GenericCode).HasMaxLength(20);
                entity.Property(e => e.GenericDescription).HasMaxLength(255);
                entity.Property(e => e.IsFuel).HasMaxLength(10);
                entity.Property(e => e.IsMeter).HasMaxLength(10);
                entity.Property(e => e.M3Type)
                    .HasMaxLength(15)
                    .HasColumnName("M3_Type");
                entity.Property(e => e.RatingIntl)
                    .HasMaxLength(50)
                    .HasColumnName("Rating_Intl");
                entity.Property(e => e.RatingUs)
                    .HasMaxLength(50)
                    .HasColumnName("Rating_US");
                entity.Property(e => e.Rehire).HasMaxLength(3);
                entity.Property(e => e.RentalTermDays).HasColumnName("Rental_Term_Days");
                entity.Property(e => e.ShiftFactor).HasMaxLength(100);
                entity.Property(e => e.UomIntl)
                    .HasMaxLength(50)
                    .HasColumnName("UOM_Intl");
                entity.Property(e => e.UomUs)
                    .HasMaxLength(50)
                    .HasColumnName("UOM_US");
                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqGenericRatingStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_GenericRating_PK");

                entity.ToTable("CPQ_GenericRating");

                entity.Property(e => e.GenericCode).HasMaxLength(20);
                entity.Property(e => e.RatingIntl).HasColumnName("Rating_Intl");
            });

            modelBuilder.Entity<CpqGenericSubstitutionStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_GenericSubstitution_PK");

                entity.ToTable("CPQ_GenericSubstitution");

                entity.HasIndex(e => e.ChildGenericId, "IX_CPQ_GenericSubstitution_ChildGenericId");

                entity.HasIndex(e => e.ParentGenericId, "IX_CPQ_GenericSubstitution_ParentGenericId");

                entity.Property(e => e.Purpose).HasMaxLength(50);
                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqGenericToGenericStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_GenericToGeneric_PK");

                entity.ToTable("CPQ_GenericToGeneric");

                entity.HasIndex(e => e.ChildGenericId, "IX_CPQ_GenericToGeneric_ChildGenericId");

                entity.HasIndex(e => e.ParentGenericId, "IX_CPQ_GenericToGeneric_ParentGenericId");

                entity.Property(e => e.Optionality).HasMaxLength(20);
                entity.Property(e => e.Purpose).HasMaxLength(50);
                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqItemStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_Item_PK");

                entity.ToTable("CPQ_Item");

                entity.HasIndex(e => e.ItemNumber, "CPQ_Item_UC").IsUnique();

                entity.HasIndex(e => e.GenericId, "IX_CPQ_Item_GenericId");

                entity.Property(e => e.CpqSequence).HasColumnName("CPQ_Sequence");
                entity.Property(e => e.DescriptionIntl).HasMaxLength(100);
                entity.Property(e => e.DescriptionNam).HasMaxLength(100);
                entity.Property(e => e.ItemNumber).HasMaxLength(100);
                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqItemAttributeValueStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_ItemAttirbuteValue_PK");

                entity.ToTable("CPQ_ItemAttributeValue");

                entity.HasIndex(e => e.AttributeId, "IX_CPQ_ItemAttributeValue_AttributeId");

                entity.HasIndex(e => new { e.ItemId, e.AttributeId, e.Value }, "IX_CPQ_ItemAttributeValue_ItemId_AttributeId_Value");

                entity.Property(e => e.Value).HasMaxLength(800);
                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqLineStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_Line_PK");

                entity.ToTable("CPQ_Line");

                entity.HasIndex(e => e.FamilyId, "IX_CPQ_Line_FamilyId");

                entity.Property(e => e.LineDescription).HasMaxLength(50);
            });

            modelBuilder.Entity<CpqLineAttributePurposeStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_LineAttributePurpose_PK");

                entity.ToTable("CPQ_LineAttributePurpose");

                entity.HasIndex(e => e.AttributeId, "IX_CPQ_LineAttributePurpose_AttributeId");

                entity.HasIndex(e => e.LineId, "IX_CPQ_LineAttributePurpose_LineId");

                entity.HasIndex(e => e.PurposeId, "IX_CPQ_LineAttributePurpose_PurposeId");

                entity.Property(e => e.Active).HasMaxLength(5);
                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqLineSellingRuleStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_LineSellingRules_PK");

                entity.ToTable("CPQ_LineSellingRules");

                entity.HasIndex(e => e.LineId, "IX_CPQ_LineSellingRule_LineId");

                entity.Property(e => e.CpqRatingGroup)
                    .HasMaxLength(25)
                    .HasColumnName("CPQ_RatingGroup");
                entity.Property(e => e.MaxValue).HasColumnType("numeric(18, 0)");
                entity.Property(e => e.MinValue).HasColumnType("numeric(18, 0)");
                entity.Property(e => e.NamIntl)
                    .HasMaxLength(4)
                    .HasColumnName("NAM_Intl");
                entity.Property(e => e.Uom)
                    .HasMaxLength(50)
                    .HasColumnName("UOM");
            });

            modelBuilder.Entity<CpqPurposeStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_Purpose_PK");

                entity.ToTable("CPQ_Purpose");

                entity.Property(e => e.PurposeDescription).HasMaxLength(50);
            });

            modelBuilder.Entity<CpqRegionStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_Region_PK");

                entity.ToTable("CPQ_Region");

                entity.Property(e => e.CpqpriceBook)
                    .HasMaxLength(255)
                    .HasColumnName("CPQPriceBook");
                entity.Property(e => e.RegionAbbreviation).HasMaxLength(50);
                entity.Property(e => e.RegionDescription).HasMaxLength(50);
                entity.Property(e => e.RegionOrgCode).HasMaxLength(20);
            });

            modelBuilder.Entity<CpqRegionCurrencyStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_CPQ_Currency");

                entity.ToTable("CPQ_RegionCurrency");

                entity.HasIndex(e => e.RegionId, "IX_CPQ_RegionCurrency_RegionId");

                entity.Property(e => e.CurrencyIsoCode).HasMaxLength(50);
            });

            modelBuilder.Entity<CpqRegionGenericStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CPQ_RegionGeneric_PK");

                entity.ToTable("CPQ_RegionGeneric");

                entity.HasIndex(e => e.GenericId, "IX_CPQ_RegionGeneric_GenericId");

                entity.HasIndex(e => e.RegionId, "IX_CPQ_RegionGeneric_RegionId");

                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqRelatedSpecificSubstitutionStaging>(entity =>
            {
                entity.ToTable("CPQ_RelatedSpecificSubstitution");

                entity.HasIndex(e => e.ChildItemId, "IX_CPQ_RelatedSpecificSubstitution_ChildItemId");

                entity.HasIndex(e => e.ParentItemId, "IX_CPQ_RelatedSpecificSubstitution_ParentItemId");

                entity.Property(e => e.VerCol)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<CpqServiceStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("IX_CPQ_Service_ID_PK");

                entity.ToTable("CPQ_Service");

                entity.HasIndex(e => e.LineId, "IX_CPQ_Service_LineId");

                entity.Property(e => e.AuspacpriceBook).HasColumnName("AUSPACPriceBook");
                entity.Property(e => e.ChargeFrequency).HasMaxLength(100);
                entity.Property(e => e.ChargeMethod).HasMaxLength(100);
                entity.Property(e => e.CoeurpriceBook).HasColumnName("COEURPriceBook");
                entity.Property(e => e.Configuration).HasMaxLength(100);
                entity.Property(e => e.ConfigurationEvent).HasMaxLength(100);
                entity.Property(e => e.ConfigurationType).HasMaxLength(100);
                entity.Property(e => e.ContractedServiceField).HasMaxLength(100);
                entity.Property(e => e.ExternalId).HasMaxLength(100);
                entity.Property(e => e.ExternalId2).HasMaxLength(100);
                entity.Property(e => e.GeneratedValue).HasMaxLength(100);
                entity.Property(e => e.LineChargeId)
                    .HasMaxLength(100)
                    .HasColumnName("LineChargeID");
                entity.Property(e => e.M3itemNumber)
                    .HasMaxLength(100)
                    .HasColumnName("M3ItemNumber");
                entity.Property(e => e.M3lineType)
                    .HasMaxLength(100)
                    .HasColumnName("M3LineType");
                entity.Property(e => e.M3type)
                    .HasMaxLength(100)
                    .HasColumnName("M3Type");
                entity.Property(e => e.NampriceBook).HasColumnName("NAMPriceBook");
                entity.Property(e => e.NoeurpriceBook).HasColumnName("NOEURPriceBook");
                entity.Property(e => e.NonRentalCharge).HasMaxLength(100);
                entity.Property(e => e.OptionLayout).HasMaxLength(100);
                entity.Property(e => e.PricingMethod).HasMaxLength(100);
                entity.Property(e => e.ProductCode).HasMaxLength(100);
                entity.Property(e => e.ProductFamily).HasMaxLength(100);
                entity.Property(e => e.ProposalSection).HasMaxLength(100);
                entity.Property(e => e.RecordTypeId).HasMaxLength(100);
                entity.Property(e => e.SbqqdefaultQuantity)
                    .HasMaxLength(100)
                    .HasColumnName("SBQQDefaultQuantity");
                entity.Property(e => e.SbqqoptionSelectionMethod)
                    .HasMaxLength(100)
                    .HasColumnName("SBQQOptionSelectionMethod");
                entity.Property(e => e.SbqqsortOrder)
                    .HasMaxLength(100)
                    .HasColumnName("SBQQSortOrder");
                entity.Property(e => e.SbqqsubscriptionBase)
                    .HasMaxLength(100)
                    .HasColumnName("SBQQSubscriptionBase");
                entity.Property(e => e.SbqqsubscriptionType)
                    .HasMaxLength(100)
                    .HasColumnName("SBQQSubscriptionType");
                entity.Property(e => e.ServiceCode).HasMaxLength(100);
                entity.Property(e => e.ShiftFactor).HasMaxLength(100);
            });

            modelBuilder.Entity<CpqVersionStaging>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("IX_CPQ_Version_ID_PK");

                entity.ToTable("CPQ_Version");

                entity.Property(e => e.DataAdded).HasColumnType("datetime");
                entity.Property(e => e.VersionNumber).HasMaxLength(10);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}
