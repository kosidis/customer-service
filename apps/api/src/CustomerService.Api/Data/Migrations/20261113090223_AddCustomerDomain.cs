using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerService.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "Customers",
                type: "nvarchar(253)",
                maxLength: 253,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Domain",
                table: "Customers");
        }
    }
}
