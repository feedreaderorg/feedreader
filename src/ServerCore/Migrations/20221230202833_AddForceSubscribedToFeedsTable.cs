using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedReader.ServerCore.Migrations
{
    /// <inheritdoc />
    public partial class AddForceSubscribedToFeedsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ForceSubscribed",
                table: "FeedInfos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForceSubscribed",
                table: "FeedInfos");
        }
    }
}
