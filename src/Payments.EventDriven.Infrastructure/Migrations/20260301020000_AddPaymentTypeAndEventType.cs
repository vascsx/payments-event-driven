using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.EventDriven.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTypeAndEventType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adiciona coluna type na tabela payments
            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Adiciona coluna event_type na tabela outbox_messages
            migrationBuilder.AddColumn<string>(
                name: "event_type",
                table: "outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "payment-created");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "event_type",
                table: "outbox_messages");
        }
    }
}
