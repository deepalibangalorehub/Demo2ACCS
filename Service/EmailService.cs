using System.Net;
using System.Net.Mail;

namespace UniversalTennis.Algorithm.Service
{
    public class EmailService
    {
        public static void SendEmailNotification(string to, string subject, string content)
        {
            var mail = new MailMessage {From = new MailAddress("no-reply@universaltennis.com")};
            mail.To.Add(new MailAddress(to));
            mail.Subject = subject;
            mail.Body = content;
            mail.IsBodyHtml = true;
            SmtpSend("smtp-relay.gmail.com", 587, mail, null);
        }

        public static void SmtpSend(string host, int port, MailMessage mail, ICredentialsByHost cred)
        {
            var smtp = new SmtpClient();
            if (cred != null)
            {
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = cred;
            }
            smtp.EnableSsl = true;
            smtp.Host = host;
            smtp.Port = port;
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.Send(mail);
        }
    }
}
