using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.ServerCore.Migrations
{
    public partial class AddFeedInfoIdToFavoriteTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FeedInfoId",
                table: "Favorites",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_FeedInfoId",
                table: "Favorites",
                column: "FeedInfoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_FeedInfos_FeedInfoId",
                table: "Favorites",
                column: "FeedInfoId",
                principalTable: "FeedInfos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_FeedInfos_FeedInfoId",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_FeedInfoId",
                table: "Favorites");

            migrationBuilder.DropColumn(
                name: "FeedInfoId",
                table: "Favorites");
        }
    }
}
