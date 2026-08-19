using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public record FeedbackRequestDto(
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    int Rating,

    string? Category,

    [Required(ErrorMessage = "Feedback message is required.")]
    [MinLength(3, ErrorMessage = "Feedback message must be at least 3 characters long.")]
    string Message,

    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    string? UserEmail,

    string? UserName,
    string? AppVersion,
    string? Platform
);

public record FeedbackResponseDto(
    bool Success,
    string Message,
    string? FeedbackId = null
);