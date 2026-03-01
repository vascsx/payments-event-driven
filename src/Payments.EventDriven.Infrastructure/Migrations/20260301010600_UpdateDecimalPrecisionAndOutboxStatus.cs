using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.EventDriven.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDecimalPrecisionAndOutboxStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FailureReason",
                table: "payments",
                newName: "failure_reason");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "failure_reason",
                table: "payments",
                newName: "FailureReason");
        }
    }
}
