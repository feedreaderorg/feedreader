using System;
using System.Threading.Tasks;
using FeedReader.ServerCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;

namespace FeedReader.WebServer.Services
{
    public class StaticFileService
    {
        FileService FileService;

        public StaticFileService(FileService fileSerivce)
        {
            FileService = fileSerivce;
        }

        public async Task ProcessUploadAsync(HttpContext context)
        {
            var form = await context.Request.ReadFormAsync();

            // Check file count, we only support uplaod one file each time.
            var files = form?.Files;
            if (files?.Count != 1)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Only support upload one file");
                return;
            }

            // Check file size, we only need upload at max 10MB file.
            var file = files[0];
            if (file.Length > 10 * 1024 * 1024)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await context.Response.WriteAsync("The maxium szie of uploading file is 10MB");
                return;
            }

            // Get file mime type.
            string contentType;
            if (!new FileExtensionContentTypeProvider().TryGetContentType(file.FileName, out contentType))
            {
                contentType = file.ContentType;
            }
            if (string.IsNullOrEmpty(contentType))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Content type is unknown");
                return;
            }

            // Get file content.
            var bytes = new byte[file.Length];
            using (var sr = file.OpenReadStream())
            {
                var readedBytes = await sr.ReadAsync(bytes);
                if (readedBytes != bytes.Length)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Upload is interrupted.");
                    return;
                }
            }

            // Save to db.
            await FileService.SaveFileAsync(bytes, contentType);
        }

        public async Task ProcessGetFileAsync(HttpContext context)
        {
            //context.Request.Path.
            var fileIdStr = context.Request.Path.ToString().Substring(1);

            // Assume the path is a file id.
            Guid fileId;
            if (!Guid.TryParse(fileIdStr, out fileId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("File id is invalid.");
                return;
            }

            // Try to get the file.
            var file = await FileService.GetFileAsync(fileId);
            if (file == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // Return the file.
            context.Response.ContentType = file.MimeType;
            context.Response.ContentLength = file.Size;
            await context.Response.Body.WriteAsync(file.Content, 0, file.Content.Length);
        }
    }
}