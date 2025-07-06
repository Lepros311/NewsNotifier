using HtmlAgilityPack;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;

public class ScraperFunction
{
    private readonly ILogger<ScraperFunction> _logger;

    public ScraperFunction(ILogger<ScraperFunction> logger)
    {
        _logger = logger;
    }

    [Function("ScraperFunction")]
    public async Task Run([TimerTrigger("0 0 12 * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

        try
        {
            // Scrape data from the website
            var url = "https://weather.com/weather/today/l/686b8c83227cb34166311be5e79f7f500d6554e3b79cd71d06cc3969c2ff7f27";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Scrape individual data points
            var highLowNode = htmlDoc.DocumentNode.SelectNodes("//span[@data-testid='TemperatureValue']");
            var windNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@data-testid='Wind']");
            var humidityNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@data-testid='PercentageValue']");
            var dewPointNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@data-testid='WeatherDetailsListItem' and .//div[text()='Dew Point']]//span[@data-testid='TemperatureValue']");
            var pressureNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@data-testid='PressureValue']");
            var uvNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@data-testid='UVIndexValue']");
            var visibilityNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@data-testid='VisibilityValue']");
            var moonPhaseNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@data-testid='WeatherDetailsListItem' and .//div[text()='Moon Phase']]//div[@data-testid='wxData']");
            var sunriseNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@data-testid='SunriseValue']//p");
            var sunsetNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@data-testid='SunsetValue']//p");

            // Format the data
            var emailBody = new StringBuilder();

            // Build email-safe HTML content
            emailBody.Append("<html><body>");
            emailBody.Append("<table style='width:100%; max-width:600px; margin:auto; font-family:Arial,sans-serif; border-collapse:collapse;'>");
            emailBody.Append("<tr><td colspan='2' style='background-color:#f2f2f2; text-align:center; padding:20px;'>");
            emailBody.Append("<h2 style='margin:0;'>Weather Today in Ashburn, VA</h2>");
            emailBody.Append($"<p style='margin:10px 0;'>for {DateTime.Now.ToString("D", CultureInfo.CurrentCulture)}</p>");
            emailBody.Append("</td></tr>");

            emailBody.Append($"<tr><td style='padding:10px;'>High / Low</td><td style='padding:10px;'>{highLowNode?[1]?.InnerText} / {highLowNode?[0]?.InnerText}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Wind</td><td style='padding:10px;'>{windNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Humidity</td><td style='padding:10px;'>{humidityNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Dew Point</td><td style='padding:10px;'>{dewPointNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Pressure</td><td style='padding:10px;'>{pressureNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>UV Index</td><td style='padding:10px;'>{uvNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Visibility</td><td style='padding:10px;'>{visibilityNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Moon Phase</td><td style='padding:10px;'>{moonPhaseNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Sunrise</td><td style='padding:10px;'>{sunriseNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append($"<tr><td style='padding:10px;'>Sunset</td><td style='padding:10px;'>{sunsetNode?.InnerText ?? "N/A"}</td></tr>");
            emailBody.Append("</table></body></html>");

            // Send the email
            using (SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 587))
            {
                string emailSenderAddress = Environment.GetEnvironmentVariable("EMAIL_SENDER_ADDRESS");
                string emailAppPassword = Environment.GetEnvironmentVariable("EMAIL_APP_PASSWORD");
                string emailRecipientAddress = Environment.GetEnvironmentVariable("EMAIL_RECIPIENT_ADDRESS");

                smtpClient.Credentials = new NetworkCredential(emailSenderAddress, emailAppPassword);

                smtpClient.EnableSsl = true;

                MailMessage mailMessage = new MailMessage
                {
                    From = new MailAddress(emailSenderAddress),
                    Subject = "Today's Weather",
                    Body = emailBody.ToString(),
                    IsBodyHtml = true // Critical for HTML formatting
                };

                mailMessage.To.Add(emailRecipientAddress);

                try
                {
                    smtpClient.Send(mailMessage);
                    _logger.LogInformation("\nEmail sent successfully!");
                }
                catch (SmtpException smtpEx)
                {
                    _logger.LogError($"SMTP error while sending email: {smtpEx.Message}");
                    _logger.LogError(smtpEx.StackTrace);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"\nFailed to send email: {ex.Message}");
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError($"HTTP request error: {httpEx.Message}");
            _logger.LogError(httpEx.StackTrace);
        }
        catch (HtmlWebException htmlEx)
        {
            _logger.LogError($"HTML parsing error: {htmlEx.Message}");
            _logger.LogError(htmlEx.StackTrace);
        }
        catch (Exception ex)
        {
            _logger.LogError($"An unexpected error occurred: {ex.Message}");
            _logger.LogError(ex.StackTrace);
        }
    }
}