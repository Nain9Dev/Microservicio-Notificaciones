using System.ComponentModel.DataAnnotations;

namespace Notificaciones.Api.Models;

/// <summary>
/// Data Transfer Object representing a new message submission from the contact form on naindev.com.
/// </summary>
public class ContactFormRequest
{
    [Required(ErrorMessage = "Sender name is required.")]
    [MaxLength(100)]
    public string SenderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sender email address is required.")]
    [EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Subject line is required.")]
    [MaxLength(150)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message content is required.")]
    [MinLength(10)]
    public string Message { get; set; } = string.Empty;
}
