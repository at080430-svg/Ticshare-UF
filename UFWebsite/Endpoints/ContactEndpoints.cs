using System.ComponentModel.DataAnnotations;
using UFWebsite.Services;

namespace UFWebsite.Endpoints;

// "Website" is a hidden field that people never see. Spam robots fill in every field,
// so if it has a value we know it's a robot.
public record ContactRequest(string FirstName, string LastName, string Email, string Topic, string Message,
    string? Website);

public static class ContactEndpoints
{
    // The topics in the dropdown on kontakt.html. Anything else is refused.
    public static readonly string[] Topics =
    [
        "Köp av biljett", "Betalning och Swish", "Sälja biljett", "Säsongskort",
        "Återbetalning", "Mitt konto", "Samarbete och press", "Övrigt",
    ];

    public static void MapContactEndpoints(this WebApplication app)
    {
        app.MapPost("/api/contact", async (ContactRequest req, EmailSender email, ILogger<EmailSender> logger) =>
        {
            // A robot filled in the hidden field: pretend it worked, but send nothing.
            if (!string.IsNullOrEmpty(req.Website))
                return Results.Ok();

            var firstName = Clean(req.FirstName);
            var lastName = Clean(req.LastName);
            var from = Clean(req.Email);
            var message = req.Message?.Trim() ?? "";

            if (firstName.Length == 0 || lastName.Length == 0 || firstName.Length > 50 || lastName.Length > 50)
                return Results.BadRequest(new { error = "Fyll i både förnamn och efternamn." });
            if (!new EmailAddressAttribute().IsValid(from) || from.Length > 100)
                return Results.BadRequest(new { error = "Skriv en giltig e-postadress." });
            if (!Topics.Contains(req.Topic))
                return Results.BadRequest(new { error = "Välj vad ditt ärende gäller." });
            if (message.Length < 10 || message.Length > 3000)
                return Results.BadRequest(new { error = "Meddelandet måste vara mellan 10 och 3000 tecken." });

            var body = $"""
                Nytt meddelande från kontaktformuläret på Ticshare

                Ärende: {req.Topic}
                Namn: {firstName} {lastName}
                E-post: {from}
                Skickat: {SwedishTime.Now:yyyy-MM-dd HH:mm}

                Meddelande:
                {message}

                Svara direkt på det här mejlet för att svara {firstName}.
                """;
            try
            {
                await email.SendToTicshareAsync($"[Kontakt] {req.Topic} – {firstName} {lastName}", body, from);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The contact email could not be sent");
                return Results.Json(new { error = $"Meddelandet kunde inte skickas just nu. Mejla oss på {email.ContactAddress} istället." },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            return Results.Ok();
        })
        .RequireRateLimiting("kontakt");
    }

    // Removes line breaks, so nobody can sneak extra email headers into the subject line.
    private static string Clean(string? text) =>
        (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
}
