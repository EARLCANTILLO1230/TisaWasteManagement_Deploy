using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TisaWasteManagement.Migrations
{
    /// <inheritdoc />
    public partial class SQLiteInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcement",
                columns: table => new
                {
                    AnnouncementId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ImageFileName = table.Column<string>(type: "TEXT", nullable: true),
                    DatePosted = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcement", x => x.AnnouncementId);
                });

            migrationBuilder.CreateTable(
                name: "BulletinBoardImage",
                columns: table => new
                {
                    BulletinBoardImageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageFileName = table.Column<string>(type: "TEXT", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    DateUploaded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulletinBoardImage", x => x.BulletinBoardImageId);
                });

            migrationBuilder.CreateTable(
                name: "Collector",
                columns: table => new
                {
                    CollectorId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ContactNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DaysOff = table.Column<string>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collector", x => x.CollectorId);
                });

            migrationBuilder.CreateTable(
                name: "GarbageTruck",
                columns: table => new
                {
                    TruckId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlateNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    MVFileNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: true),
                    StatusFlag = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarbageTruck", x => x.TruckId);
                });

            migrationBuilder.CreateTable(
                name: "InspectorAccount",
                columns: table => new
                {
                    InspectorAccountId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectorAccount", x => x.InspectorAccountId);
                });

            migrationBuilder.CreateTable(
                name: "ReportFile",
                columns: table => new
                {
                    ReportFileId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UploadDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportFile", x => x.ReportFileId);
                });

            migrationBuilder.CreateTable(
                name: "Sitio",
                columns: table => new
                {
                    SitioId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SitioName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sitio", x => x.SitioId);
                });

            migrationBuilder.CreateTable(
                name: "SmsLog",
                columns: table => new
                {
                    SmsLogId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipientNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    NotificationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Response = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SentBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsLog", x => x.SmsLogId);
                });

            migrationBuilder.CreateTable(
                name: "CollectionSchedule",
                columns: table => new
                {
                    CollectionScheduleId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DumpNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateOfCompletion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RepeatWeekly = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentScheduleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DriverId = table.Column<int>(type: "INTEGER", nullable: true),
                    TruckId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionSchedule", x => x.CollectionScheduleId);
                    table.ForeignKey(
                        name: "FK_CollectionSchedule_CollectionSchedule_ParentScheduleId",
                        column: x => x.ParentScheduleId,
                        principalTable: "CollectionSchedule",
                        principalColumn: "CollectionScheduleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionSchedule_Collector_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Collector",
                        principalColumn: "CollectorId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionSchedule_GarbageTruck_TruckId",
                        column: x => x.TruckId,
                        principalTable: "GarbageTruck",
                        principalColumn: "TruckId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Complaint",
                columns: table => new
                {
                    ComplaintId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResidentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContactNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    SitioId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComplaintType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    TicketNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Awaiting Review"),
                    FiledDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImageFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaint", x => x.ComplaintId);
                    table.ForeignKey(
                        name: "FK_Complaint_Sitio_SitioId",
                        column: x => x.SitioId,
                        principalTable: "Sitio",
                        principalColumn: "SitioId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionMonitoring",
                columns: table => new
                {
                    CollectionMonitoringId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CollectionScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SitioNames = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReasonForDelay = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LogDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionMonitoring", x => x.CollectionMonitoringId);
                    table.ForeignKey(
                        name: "FK_CollectionMonitoring_CollectionSchedule_CollectionScheduleId",
                        column: x => x.CollectionScheduleId,
                        principalTable: "CollectionSchedule",
                        principalColumn: "CollectionScheduleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionScheduleCollector",
                columns: table => new
                {
                    CollectionScheduleCollectorId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CollectionScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectorId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionScheduleCollector", x => x.CollectionScheduleCollectorId);
                    table.ForeignKey(
                        name: "FK_CollectionScheduleCollector_CollectionSchedule_CollectionScheduleId",
                        column: x => x.CollectionScheduleId,
                        principalTable: "CollectionSchedule",
                        principalColumn: "CollectionScheduleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionScheduleCollector_Collector_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Collector",
                        principalColumn: "CollectorId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionScheduleSitio",
                columns: table => new
                {
                    CollectionScheduleSitioId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CollectionScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SitioId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReasonForDelay = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReassignedToScheduleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionScheduleSitio", x => x.CollectionScheduleSitioId);
                    table.ForeignKey(
                        name: "FK_CollectionScheduleSitio_CollectionSchedule_CollectionScheduleId",
                        column: x => x.CollectionScheduleId,
                        principalTable: "CollectionSchedule",
                        principalColumn: "CollectionScheduleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionScheduleSitio_Sitio_SitioId",
                        column: x => x.SitioId,
                        principalTable: "Sitio",
                        principalColumn: "SitioId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMonitoring_CollectionScheduleId",
                table: "CollectionMonitoring",
                column: "CollectionScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSchedule_DriverId",
                table: "CollectionSchedule",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSchedule_ParentScheduleId",
                table: "CollectionSchedule",
                column: "ParentScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSchedule_TruckId",
                table: "CollectionSchedule",
                column: "TruckId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScheduleCollector_CollectionScheduleId_CollectorId",
                table: "CollectionScheduleCollector",
                columns: new[] { "CollectionScheduleId", "CollectorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScheduleCollector_CollectorId",
                table: "CollectionScheduleCollector",
                column: "CollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScheduleSitio_CollectionScheduleId_SitioId",
                table: "CollectionScheduleSitio",
                columns: new[] { "CollectionScheduleId", "SitioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScheduleSitio_SitioId",
                table: "CollectionScheduleSitio",
                column: "SitioId");

            migrationBuilder.CreateIndex(
                name: "IX_Collector_ContactNumber",
                table: "Collector",
                column: "ContactNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_SitioId",
                table: "Complaint",
                column: "SitioId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_TicketNumber",
                table: "Complaint",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GarbageTruck_MVFileNumber",
                table: "GarbageTruck",
                column: "MVFileNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GarbageTruck_PlateNumber",
                table: "GarbageTruck",
                column: "PlateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectorAccount_Username",
                table: "InspectorAccount",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsLog_SentDate",
                table: "SmsLog",
                column: "SentDate");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLog_Status",
                table: "SmsLog",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcement");

            migrationBuilder.DropTable(
                name: "BulletinBoardImage");

            migrationBuilder.DropTable(
                name: "CollectionMonitoring");

            migrationBuilder.DropTable(
                name: "CollectionScheduleCollector");

            migrationBuilder.DropTable(
                name: "CollectionScheduleSitio");

            migrationBuilder.DropTable(
                name: "Complaint");

            migrationBuilder.DropTable(
                name: "InspectorAccount");

            migrationBuilder.DropTable(
                name: "ReportFile");

            migrationBuilder.DropTable(
                name: "SmsLog");

            migrationBuilder.DropTable(
                name: "CollectionSchedule");

            migrationBuilder.DropTable(
                name: "Sitio");

            migrationBuilder.DropTable(
                name: "Collector");

            migrationBuilder.DropTable(
                name: "GarbageTruck");
        }
    }
}
