using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Database tables (DbSets)
        public DbSet<Sitio> Sitio { get; set; }
        public DbSet<Collector> Collector { get; set; }
        public DbSet<GarbageTruck> GarbageTruck { get; set; }
        public DbSet<CollectionSchedule> CollectionSchedule { get; set; }
        public DbSet<CollectionMonitoring> MonitoringLog { get; set; }
        public DbSet<Complaint> Complaint { get; set; }
        public DbSet<InspectorAccount> InspectorAccount { get; set; }
        public DbSet<CollectionScheduleSitio> CollectionScheduleSitio { get; set; }
        public DbSet<CollectionScheduleCollector> CollectionScheduleCollector { get; set; }
        public DbSet<Announcement> Announcement { get; set; }
        public DbSet<BulletinBoardImage> BulletinBoardImage { get; set; }
        public DbSet<ReportFile> ReportFiles { get; set; }
        public DbSet<SmsLog> SmsLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Map each entity to its corresponding database table
            modelBuilder.Entity<Sitio>().ToTable("Sitio");
            modelBuilder.Entity<Collector>().ToTable("Collector");
            modelBuilder.Entity<GarbageTruck>().ToTable("GarbageTruck");
            modelBuilder.Entity<CollectionSchedule>().ToTable("CollectionSchedule");

            // Keep DayOfWeek nullable
            modelBuilder.Entity<CollectionSchedule>()
                .Property(s => s.DayOfWeek)
                .IsRequired(false);

            // Self-referencing relationship for ParentScheduleId
            modelBuilder.Entity<CollectionSchedule>()
                .HasOne(s => s.ParentSchedule)
                .WithMany(s => s.ChildSchedules)
                .HasForeignKey(s => s.ParentScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            // A schedule belongs to at most one Driver
            modelBuilder.Entity<CollectionSchedule>()
                .HasOne(s => s.Driver)
                .WithMany()
                .HasForeignKey(s => s.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            // A schedule belongs to at most one GarbageTruck
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

            // GarbageTruck: MVFileNumber and PlateNumber must be unique
            modelBuilder.Entity<GarbageTruck>(entity =>
            {
                entity.HasIndex(g => g.MVFileNumber).IsUnique();
                entity.HasIndex(g => g.PlateNumber).IsUnique();
            });

            // Configure Collector DaysOff - SQLite uses TEXT
            modelBuilder.Entity<Collector>(entity =>
            {
                entity.Property(c => c.DaysOff)
                    .HasColumnType("TEXT")
                    .IsRequired(false);
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

                entity.HasIndex(e => new { e.CollectionScheduleId, e.SitioId }).IsUnique();
            });

            // Configure many-to-many between CollectionSchedule and Collector
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

                entity.HasIndex(e => new { e.CollectionScheduleId, e.CollectorId }).IsUnique();
            });

            // MonitoringLog - CollectionSchedule relationship
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
            });

            // InspectorAccount: Username must be unique
            modelBuilder.Entity<InspectorAccount>().ToTable("InspectorAccount");
            modelBuilder.Entity<InspectorAccount>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // Announcement / Bulletin Board
            modelBuilder.Entity<Announcement>().ToTable("Announcement");
            modelBuilder.Entity<BulletinBoardImage>().ToTable("BulletinBoardImage");

            // File Management
            modelBuilder.Entity<ReportFile>().ToTable("ReportFile");

            // SMS Module
            modelBuilder.Entity<SmsLog>().ToTable("SmsLog");
            modelBuilder.Entity<SmsLog>()
                .HasIndex(e => e.SentDate);
            modelBuilder.Entity<SmsLog>()
                .HasIndex(e => e.Status);

            base.OnModelCreating(modelBuilder);
        }
    }
}