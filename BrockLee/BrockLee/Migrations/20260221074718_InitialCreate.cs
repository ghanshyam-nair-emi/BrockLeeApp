using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrockLee.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: false),
                    Wage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpenseCount = table.Column<int>(type: "int", nullable: false),
                    TotalExpenseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRemanent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NpsRealValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IndexRealValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxBenefit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    YearsToRetirement = table.Column<int>(type: "int", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseTimeMs = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLogs_LoggedAt",
                table: "UserLogs",
                column: "LoggedAt",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLogs");
        }
    }
}
