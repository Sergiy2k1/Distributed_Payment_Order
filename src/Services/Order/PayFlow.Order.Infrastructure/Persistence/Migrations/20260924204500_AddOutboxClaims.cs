using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Order.Infrastructure.Persistence.Migrations;

public partial class AddOutboxClaims : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "claim_token",
            table: "outbox_messages",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "claimed_until_utc",
            table: "outbox_messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_messages_claim_pair",
            table: "outbox_messages",
            sql: "(claim_token IS NULL AND claimed_until_utc IS NULL) OR "
                + "(claim_token IS NOT NULL AND claimed_until_utc IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_messages_claim_pair",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "claim_token",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "claimed_until_utc",
            table: "outbox_messages");
    }
}
