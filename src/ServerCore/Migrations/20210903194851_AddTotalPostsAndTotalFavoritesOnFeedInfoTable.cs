using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.ServerCore.Migrations
{
    public partial class AddTotalPostsAndTotalFavoritesOnFeedInfoTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalFavorites",
                table: "FeedInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPosts",
                table: "FeedInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalFavorites",
                table: "FeedInfos");

            migrationBuilder.DropColumn(
                name: "TotalPosts",
                table: "FeedInfos");
        }
    }
}
