using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructor - passes options to base DbContext
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Database tables (DbSets)
        public DbSet<Sitio> Sitio { get; set; }
        public DbSet<Collector> Collector { get; set; }
        public DbSet<GarbageTruck> GarbageTruck { get; set; }
        public DbSet<CollectionSchedule> CollectionSchedule { get; set; }
        // NOTE: CollectionScheduleCollector and CollectionScheduleTruck DbSets were removed.
        // A schedule now links to exactly one Collector and one GarbageTruck directly
        // (see CollectorId/TruckId on CollectionSchedule), so the old many-to-many join
        // tables for collectors and trucks are no longer used.
        public DbSet<CollectionMonitoring> MonitoringLog { get; set; }
        public DbSet<Complaint> Complaint { get; set; }

        // Inspector staff login accounts, managed by Admin via
        // InspectorAccountController. Replaces the old hardcoded inspector
        // username/password that used to live in AccountController.
        public DbSet<InspectorAccount> InspectorAccount { get; set; }

        // Admin staff login accounts, managed by Admin via
        // AdminAccountController. Replaces the old hardcoded admin
        // username/password that used to live in AccountController.
        // Same pattern as InspectorAccount right above.
        public DbSet<AdminAccount> AdminAccount { get; set; }

        // New join table for many-to-many between CollectionSchedule and Sitio
        public DbSet<CollectionScheduleSitio> CollectionScheduleSitio { get; set; }

        // Join table for many-to-many between CollectionSchedule and Collector (the crew).
        // A schedule's single Driver is still a direct DriverId FK below - this table is
        // only for the (possibly several) Collectors riding along.
        public DbSet<CollectionScheduleCollector> CollectionScheduleCollector { get; set; }

        // --- Announcement / Bulletin Board module -----------------------
        // Announcement: posts created by the Admin, shown on the public Home page.
        public DbSet<Announcement> Announcement { get; set; }
        // BulletinBoardImage: educational pictures (e.g. waste segregation guides)
        // shown on the public Home page. Managed separately from Announcements.
        public DbSet<BulletinBoardImage> BulletinBoardImage { get; set; }

        // --- File Management module -----------------------------------
        // ReportFile: metadata for documents (PDF/Word/Excel) uploaded by Admin.
        // The actual file bytes live on disk in wwwroot/uploads/ - this table
        // just tracks the file name, category, and where to find it (FilePath).
        // NOTE: The Report GENERATION module (Reports/Generate) does NOT need
        // a DbSet here - it builds report files on the fly from existing data
        // and never saves anything to the database.
        public DbSet<ReportFile> ReportFiles { get; set; }

        // --- SMS Module -------------------------------------------------
        // SmsLog: one row per SMS attempt (sent or failed), used by the
        // Complaint status-update notification feature and the SMS Logs
        // page for Admin.
        public DbSet<SmsLog> SmsLogs { get; set; }

        // Configure entity mappings and relationships
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Map each entity to its corresponding database table
            modelBuilder.Entity<Sitio>().ToTable("Sitio");
            modelBuilder.Entity<Collector>().ToTable("Collector");
            modelBuilder.Entity<GarbageTruck>().ToTable("GarbageTruck");
            modelBuilder.Entity<CollectionSchedule>().ToTable("CollectionSchedule");
            // Keep this nullable in storage so existing schedules can be assigned a day later.
            // The form still requires a day for all newly created or edited schedules.
            modelBuilder.Entity<CollectionSchedule>()
                .Property(s => s.DayOfWeek)
                .IsRequired(false);

            // Self-referencing relationship: ParentScheduleId links an auto-generated
            // "next occurrence" schedule back to the schedule it was created from.
            //
            // NOTE: SQL Server does NOT allow CASCADE, SET NULL, or SET DEFAULT on a
            // self-referencing foreign key (a table pointing back to itself) - it
            // always requires NO ACTION, even though there's no real risk of a cycle
            // here. Attempting SetNull throws: "may cause cycles or multiple cascade
            // paths... Specify ON DELETE NO ACTION." DeleteBehavior.Restrict maps to
            // NO ACTION, so this is the only option EF Core can generate for SQL Server.
            //
            // Practical effect: the database will now BLOCK deleting a schedule that
            // still has a child (an auto-generated next occurrence) pointing at it,
            // instead of automatically clearing the link like SetNull would have.
            // To keep the original "deleting old history never breaks an active
            // schedule" behavior, CollectionScheduleController.DeleteConfirmed now
            // manually clears any child schedules' ParentScheduleId before deleting
            // the parent - doing in application code what the database can't do
            // automatically for a self-referencing key.
            modelBuilder.Entity<CollectionSchedule>()
                .HasOne(s => s.ParentSchedule)
                .WithMany(s => s.ChildSchedules)
                .HasForeignKey(s => s.ParentScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            // A schedule belongs to at most one Driver, same pattern as GarbageTruck below.
            // Restrict delete for the same reason - see the ParentScheduleId comment above.
            modelBuilder.Entity<CollectionSchedule>()
                .HasOne(s => s.Driver)
                .WithMany()
                .HasForeignKey(s => s.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            // A schedule belongs to at most one GarbageTruck. Restrict delete for the same reason.
            modelBuilder.Entity<CollectionSchedule>()
                .HasOne(s => s.GarbageTruck)
                .WithMany()
                .HasForeignKey(s => s.TruckId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CollectionMonitoring>().ToTable("CollectionMonitoring");
            modelBuilder.Entity<Complaint>().ToTable("Complaint");
            modelBuilder.Entity<CollectionScheduleSitio>().ToTable("CollectionScheduleSitio");

            // Collector: ContactNumber must be unique
            modelBuilder.Entity<Collector>()
                .HasIndex(c => c.ContactNumber)
                .IsUnique();

            // GarbageTruck: MVFileNumber and PlateNumber must be unique (if not null)
            modelBuilder.Entity<GarbageTruck>(entity =>
            {
                entity.HasIndex(g => g.MVFileNumber)
                    .IsUnique()
                    .HasFilter("[MVFileNumber] IS NOT NULL");

                entity.HasIndex(g => g.PlateNumber)
                    .IsUnique()
                    .HasFilter("[PlateNumber] IS NOT NULL");

                // (soft-delete removed) no IsActive property on GarbageTruck
            });

            modelBuilder.Entity<Sitio>(entity =>
            {
                // (soft-delete removed) no IsActive property on Sitio
            });

            modelBuilder.Entity<Collector>(entity =>
            {
                // (soft-delete removed) no IsActive property on Collector

                // Configure DaysOff property
                entity.Property(c => c.DaysOff)
                    .HasColumnType("nvarchar(max)")
                    .IsRequired(false); // nullable
            });

            // Configure many-to-many via explicit join entity
            modelBuilder.Entity<CollectionScheduleSitio>(entity =>
            {
                entity.HasKey(e => e.CollectionScheduleSitioId);

                entity.HasOne(e => e.CollectionSchedule)
                      .WithMany(cs => cs.CollectionScheduleSitios)
                      .HasForeignKey(e => e.CollectionScheduleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Sitio)
                      .WithMany()
                      .HasForeignKey(e => e.SitioId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Optional: prevent duplicate (CollectionScheduleId, SitioId) entries
                entity.HasIndex(e => new { e.CollectionScheduleId, e.SitioId }).IsUnique();
            });

            // Configure many-to-many between CollectionSchedule and Collector (the crew),
            // same pattern as CollectionScheduleSitio above.
            modelBuilder.Entity<CollectionScheduleCollector>(entity =>
            {
                entity.ToTable("CollectionScheduleCollector");
                entity.HasKey(e => e.CollectionScheduleCollectorId);

                entity.HasOne(e => e.CollectionSchedule)
                      .WithMany(cs => cs.CollectionScheduleCollectors)
                      .HasForeignKey(e => e.CollectionScheduleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Collector)
                      .WithMany()
                      .HasForeignKey(e => e.CollectorId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Prevent the same collector being added twice to the same schedule.
                entity.HasIndex(e => new { e.CollectionScheduleId, e.CollectorId }).IsUnique();
            });

            // MonitoringLog - CollectionSchedule relationship (Cascade delete - logs deleted when schedule deleted)
            modelBuilder.Entity<CollectionMonitoring>()
                .HasOne(m => m.CollectionSchedule)
                .WithMany()
                .HasForeignKey(m => m.CollectionScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Complaint: TicketNumber must be unique
            modelBuilder.Entity<Complaint>()
                .HasIndex(c => c.TicketNumber)
                .IsUnique();

            // Complaint: Status has max length 20, defaults to "Awaiting Review"
            modelBuilder.Entity<Complaint>(entity =>
            {
                entity.Property(c => c.Status)
                    .HasMaxLength(20)
                    .HasDefaultValue("Awaiting Review");

                // NOTE: Complaint photos are no longer stored as Base64 text in the
                // database - they're saved as files in wwwroot/images/complaints
                // and only the file name is stored (see Complaint.ImageFileName),
                // same approach as Announcement and BulletinBoardImage. So there's
                // no more ImageData column to configure here.
            });

            // InspectorAccount: Username must be unique, same pattern as
            // Collector.ContactNumber and GarbageTruck.PlateNumber above.
            modelBuilder.Entity<InspectorAccount>().ToTable("InspectorAccount");
            modelBuilder.Entity<InspectorAccount>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // AdminAccount: Username must be unique, same pattern as
            // InspectorAccount right above.
            modelBuilder.Entity<AdminAccount>().ToTable("AdminAccount");
            modelBuilder.Entity<AdminAccount>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // --- Announcement / Bulletin Board module -----------------------
            modelBuilder.Entity<Announcement>().ToTable("Announcement");
            modelBuilder.Entity<BulletinBoardImage>().ToTable("BulletinBoardImage");

            // --- File Management module -----------------------------------
            modelBuilder.Entity<ReportFile>().ToTable("ReportFile");

            base.OnModelCreating(modelBuilder);
        }
    }
}
