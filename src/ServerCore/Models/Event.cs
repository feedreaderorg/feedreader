using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedReader.ServerCore.Models;

public class Event
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int DbId { get; set; }

    public EventCategory EventCategory { get; set; }

    public LogLevel LogLevel { get; set; }

    public string? Content { get; set; }
}