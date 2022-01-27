using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace FeedReader.ServerCore.Migrations
{
    public partial class AddFullTextSupportOnFeedsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "FeedInfos",
                type: "tsvector",
                nullable: true)
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Name", "SubscriptionName", "Description" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedInfos_SearchVector",
                table: "FeedInfos",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeedInfos_SearchVector",
                table: "FeedInfos");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "FeedInfos");
        }
    }
}
