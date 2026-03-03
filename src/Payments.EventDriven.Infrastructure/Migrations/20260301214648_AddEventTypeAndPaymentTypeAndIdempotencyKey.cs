using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.EventDriven.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTypeAndPaymentTypeAndIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adiciona coluna event_type na tabela outbox_messages
            migrationBuilder.AddColumn<string>(
                name: "event_type",
                table: "outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "payment-created");

            // Adiciona coluna type na tabela payments
            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Adiciona coluna idempotency_key na tabela payments
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // Cria índice único para prevenir pagamentos duplicados
            migrationBuilder.CreateIndex(
                name: "idx_payment_idempotency_key",
                table: "payments",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_idempotency_key",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "type",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "event_type",
                table: "outbox_messages");
        }
    }
}
