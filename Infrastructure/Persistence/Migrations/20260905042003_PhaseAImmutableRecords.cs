using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaluExamPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseAImmutableRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fees_Enrollments_EnrollmentId",
                table: "Fees");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                table: "Fees",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountTitle",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AmountInWordsEn",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseFee",
                table: "Fees",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ContentSha256",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAtUtc",
                table: "Fees",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "LateFee",
                table: "Fees",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MigrationFee",
                table: "Fees",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousFeeId",
                table: "Fees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedPaidAt",
                table: "Fees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedPaidBy",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptRelativePath",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Fees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedBy",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditLogs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Entity",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "BankReconciliationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MatchedCount = table.Column<int>(type: "int", nullable: false),
                    ExceptionCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliationBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentLedgers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankTxnId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScrollNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BranchCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaidAtBank = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowSha256 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrevRowSha256 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentLedgers_Fees_FeeId",
                        column: x => x.FeeId,
                        principalTable: "Fees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankReconciliationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChallanNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankTxnId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaidAtBank = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BranchCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankReconciliationLines_BankReconciliationBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "BankReconciliationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankReconciliationLines_Fees_FeeId",
                        column: x => x.FeeId,
                        principalTable: "Fees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fees_Status_DueDate",
                table: "Fees",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Fees_TransactionId",
                table: "Fees",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity_EntityId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "Entity", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliationLines_BatchId_ChallanNumber",
                table: "BankReconciliationLines",
                columns: new[] { "BatchId", "ChallanNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliationLines_FeeId",
                table: "BankReconciliationLines",
                column: "FeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgers_FeeId_CreatedAtUtc",
                table: "PaymentLedgers",
                columns: new[] { "FeeId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgers_IdempotencyKey",
                table: "PaymentLedgers",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Fees_Enrollments_EnrollmentId",
                table: "Fees",
                column: "EnrollmentId",
                principalTable: "Enrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.tr_PaymentLedgersPreventModification
                ON dbo.PaymentLedgers
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51001, 'PaymentLedger rows are append-only and cannot be modified or deleted.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.tr_AuditLogsPreventModification
                ON dbo.AuditLogs
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51002, 'AuditLog rows are append-only and cannot be modified or deleted.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fees_Enrollments_EnrollmentId",
                table: "Fees");

            migrationBuilder.DropTable(
                name: "BankReconciliationLines");

            migrationBuilder.DropTable(
                name: "PaymentLedgers");

            migrationBuilder.DropTable(
                name: "BankReconciliationBatches");

            migrationBuilder.DropIndex(
                name: "IX_Fees_Status_DueDate",
                table: "Fees");

            migrationBuilder.DropIndex(
                name: "IX_Fees_TransactionId",
                table: "Fees");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Entity_EntityId_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "AccountTitle",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "AmountInWordsEn",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "BaseFee",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "ContentSha256",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "IssuedAtUtc",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "LateFee",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "MigrationFee",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "PreviousFeeId",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "ProposedPaidAt",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "ProposedPaidBy",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "ReceiptRelativePath",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "VerifiedBy",
                table: "Fees");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Entity",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddForeignKey(
                name: "FK_Fees_Enrollments_EnrollmentId",
                table: "Fees",
                column: "EnrollmentId",
                principalTable: "Enrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.tr_PaymentLedgersPreventModification;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.tr_AuditLogsPreventModification;");
        }
    }
}
