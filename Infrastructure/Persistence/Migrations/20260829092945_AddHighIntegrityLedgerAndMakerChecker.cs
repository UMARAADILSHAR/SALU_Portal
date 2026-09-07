using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaluExamPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHighIntegrityLedgerAndMakerChecker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdmitCards_Seats_SeatId",
                table: "AdmitCards");

            migrationBuilder.DropIndex(
                name: "IX_AdmitCards_SeatId",
                table: "AdmitCards");

            migrationBuilder.AlterColumn<Guid>(
                name: "SeatId",
                table: "AdmitCards",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "CenterCode",
                table: "AdmitCards",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAtUtc",
                table: "AdmitCards",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "QRVerificationHash",
                table: "AdmitCards",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RollNumber",
                table: "AdmitCards",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoomNumber",
                table: "AdmitCards",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SeatNumber",
                table: "AdmitCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MakerCheckerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetEntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProposedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MakerUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MakerSubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckerUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckerReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotpVerificationRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakerCheckerRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmitCards_RollNumber",
                table: "AdmitCards",
                column: "RollNumber",
                unique: true,
                filter: "[RollNumber] IS NOT NULL AND [RollNumber] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AdmitCards_SeatId",
                table: "AdmitCards",
                column: "SeatId",
                unique: true,
                filter: "[SeatId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_ReviewStatus",
                table: "MakerCheckerRequests",
                column: "ReviewStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_AdmitCards_Seats_SeatId",
                table: "AdmitCards",
                column: "SeatId",
                principalTable: "Seats",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdmitCards_Seats_SeatId",
                table: "AdmitCards");

            migrationBuilder.DropTable(
                name: "MakerCheckerRequests");

            migrationBuilder.DropIndex(
                name: "IX_AdmitCards_RollNumber",
                table: "AdmitCards");

            migrationBuilder.DropIndex(
                name: "IX_AdmitCards_SeatId",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "CenterCode",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "GeneratedAtUtc",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "QRVerificationHash",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "RollNumber",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "RoomNumber",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "SeatNumber",
                table: "AdmitCards");

            migrationBuilder.AlterColumn<Guid>(
                name: "SeatId",
                table: "AdmitCards",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmitCards_SeatId",
                table: "AdmitCards",
                column: "SeatId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AdmitCards_Seats_SeatId",
                table: "AdmitCards",
                column: "SeatId",
                principalTable: "Seats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
