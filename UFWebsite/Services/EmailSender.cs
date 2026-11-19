using System.Net;
using System.Net.Mail;

namespace UFWebsite.Services;

// Sends email through an SMTP server: Loopia (mailcluster.loopia.se) for a mailbox on
// ticshare.se, or Gmail. The login details come from user secrets on your own computer and
// from appsettings.Production.json on the server, never from the code or from Git.
// If no SMTP server is set up (while developing), the email is written to the terminal instead.
public class EmailSender(IConfiguration config, ILogger<EmailSender> logger)
{
    // Where contact messages end up. Also shown to visitors if sending fails.
    public string ContactAddress => config["Email:To"] ?? "ticshareuf@gmail.com";

    public async Task SendToTicshareAsync(string subject, string body, string replyTo)
    {
        var host = config["Email:SmtpHost"];
        var username = config["Email:Username"];
        var password = config["Email:Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("No SMTP server configured, so the email was not sent:\nTo: {To}\nReply-To: {ReplyTo}\nSubject: {Subject}\n{Body}",
                ContactAddress, replyTo, subject, body);
            return;
        }

        // The sender must be the mailbox we log in with, or Loopia/Gmail refuse the email.
        var from = config["Email:From"] ?? username;
        using var message = new MailMessage(new MailAddress(from, "Ticshare kontaktformulär"), new MailAddress(ContactAddress))
        {
            Subject = subject,
            Body = body,
        };
        // Pressing "Svara" in the inbox answers the person who wrote, not ourselves.
        message.ReplyToList.Add(new MailAddress(replyTo));

        // Port 587 with EnableSsl = true (STARTTLS) works for both Loopia and Gmail.
        // Don't use port 465: .NET's SmtpClient can't talk to it.
        using var smtp = new SmtpClient(host, config.GetValue("Email:Port", 587))
        {
            EnableSsl = config.GetValue("Email:EnableSsl", true),
            Credentials = new NetworkCredential(username, password),
            Timeout = 15000, // give up after 15 seconds instead of leaving the visitor waiting
        };
        await smtp.SendMailAsync(message);
        logger.LogInformation("Contact email sent to {To}: {Subject}", ContactAddress, subject);
    }
}
