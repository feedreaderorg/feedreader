using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.WebServer.Migrations
{
    public partial class AddIdFromUriAndRegistrationTimeToFeedInfoTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdFromUri",
                table: "FeedInfos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationTime",
                table: "FeedInfos",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_FeedInfos_IdFromUri",
                table: "FeedInfos",
                column: "IdFromUri",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeedInfos_IdFromUri",
                table: "FeedInfos");

            migrationBuilder.DropColumn(
                name: "IdFromUri",
                table: "FeedInfos");

            migrationBuilder.DropColumn(
                name: "RegistrationTime",
                table: "FeedInfos");
        }
    }
}
