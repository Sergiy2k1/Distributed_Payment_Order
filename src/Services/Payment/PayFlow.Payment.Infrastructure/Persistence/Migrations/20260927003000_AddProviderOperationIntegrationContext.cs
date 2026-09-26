using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Payment.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260927003000_AddProviderOperationIntegrationContext")]
public partial class AddProviderOperationIntegrationContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "causation_id",
            table: "provider_operations",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "correlation_id",
            table: "provider_operations",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "trace_parent",
            table: "provider_operations",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "causation_id",
            table: "provider_operations");

        migrationBuilder.DropColumn(
            name: "correlation_id",
            table: "provider_operations");

        migrationBuilder.DropColumn(
            name: "trace_parent",
            table: "provider_operations");
    }
}
