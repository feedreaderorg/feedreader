using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedReader.ServerCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscribedToFeedSubscriptionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Subscribed",
                table: "FeedSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subscribed",
                table: "FeedSubscriptions");
        }
    }
}
