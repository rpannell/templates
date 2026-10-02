namespace Company.Product.Scheduler.Managers.Models;

/// <summary>A message to send to people (email, chat, SMS...).</summary>
/// <param name="Subject">One-line subject.</param>
/// <param name="Body">Message text.</param>
public sealed record NotificationDto(string Subject, string Body);
