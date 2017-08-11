using System;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public class NotificationManager
    {
        #region Public Methods
        public NotificationManager(string json)
        {
            m_mailingList = new List<MailAddress>();
            Dictionary<string, string> obj = new StringStream(json).ParseJSON();
            m_SMTPhost = obj["serverAddress"];
            m_address = obj["mailAddress"];
            m_username = obj["username"];
            m_password = obj["password"];
            m_domain = obj["domain"];
        }
        public void AddSubscriber(string address)
        {
            if (!m_mailingList.Exists(a => a.Address == address)) m_mailingList.Add(new MailAddress(address));
        }
        public void RemoveSubscriber(string address)
        {
            m_mailingList.RemoveAll(a => a.Address == address);
        }
        public void PushNotification(string subject, string body, List<Attachment> attachments = null)
        {
            foreach (MailAddress subscriber in m_mailingList)
            {
                SendMail(subject, body, subscriber,attachments);
            }
        }
        public void PushSummary()
        {
            string message = "";
            string[] fridayMessages = new string[]{
                "The weekend is HERE!",
                "Ready for the weekend?",
                "Look at the clock, its almost time!",
                "The last day of an awful week, am I right?",
                "Can't wait for the weekend",
                "It's Friday my friend"
            };
            Random rand = new Random();
            if (DateTime.Today.DayOfWeek == DayOfWeek.Friday)
            {
                message += fridayMessages[rand.Next(0, fridayMessages.Count() - 1)] + "\n\n";
            }
            message += DateTime.Today.ToString("MM/dd/yyyy") + " EATS Update\n\n";
            message += SectionDivider;
            message += AbnormalitySummary();
            message += SectionDivider;
            message += ShipDateSummary();
            message += SectionDivider;

            Summary dailySummary = new Summary(DateTime.Today, DateTime.Today);

            List<Attachment> attachments = new List<Attachment>();

            dailySummary.ExportCSV(SummaryType.Production);
            attachments.Add(new Attachment(System.IO.Path.Combine(Server.RootDir, "EATS Client", "production.csv"), MediaTypeNames.Application.Octet));
            dailySummary.ExportCSV(SummaryType.Scrap);
            attachments.Add( new Attachment(System.IO.Path.Combine(Server.RootDir, "EATS Client", "scrap.csv"), MediaTypeNames.Application.Octet));
            dailySummary.ExportCSV(SummaryType.Process);
            attachments.Add(new Attachment(System.IO.Path.Combine(Server.RootDir, "EATS Client", "process.csv"), MediaTypeNames.Application.Octet));
            
            PushNotification(DateTime.Today.ToString("MM/dd/yyyy") + " EATS Update", message, attachments);
        }
        public static string SectionDivider { get; } = "\n\n|" + new string('=', 60) + "|\n\n";
        public static string RowDivider { get; } = "\n" + new string('_',60) + "\n";
        public string ShipDateSummary()
        {
            string message = "Close Ship Dates\n\n";
            TimeSpan notifyWithin = new TimeSpan(3, 0, 0, 0);

            List<Order> sortedOrders = Server.OrderManager.GetOrders;
            sortedOrders.Sort((a, b) => a.ShipDate.CompareTo(b.ShipDate));
            foreach (Order order in sortedOrders)
            {
                TimeSpan timeUntil = order.ShipDate - DateTime.Today;
                if (timeUntil < notifyWithin)
                {

                    message += order.SalesOrderNo + "\tShips in " + timeUntil.Days + " days : " + order.ShipDate.ToString("MM/dd/yyyy") + Environment.NewLine;
                    List<OrderItem> travelerItems = order.Items.Where(i => i.ChildTraveler != -1).ToList();
                    if (travelerItems.Count > 0)
                    {
                        message += "\tTravelers:" + Environment.NewLine;
                        foreach (OrderItem item in travelerItems)
                        {
                            message += "\t\t" + item.ChildTraveler.ToString() + "\t; " + item.ItemCode + "\t; " + item.QtyNeeded + " need to ship" + Environment.NewLine;
                        }
                    }
                    message += "".PadLeft(50, '_') + Environment.NewLine;
                }
            }
            return message;
        }
        public string AbnormalitySummary()
        {
            string message = "";
            List<TravelerItem> items = Server.TravelerManager.GetTravelers.SelectMany(t => t.Items.Where(i => i.LocalState == LocalItemState.InProcess)).ToList();
            if (items.Any()) message += "UH OH!\nIn-Process items remain\n\n";
            foreach (TravelerItem item in items)
            {
                message += RowDivider;
                message += "\n" + item.PrintID() + "  In Process at " + item.Station.Name;
            }
            return message;
        }
        #endregion

        #region Private Methods
        private void SendMail(string subject, string body, MailAddress subscriber, List<Attachment> attachments = null)
        {
            if (!ConfigManager.GetJSON("debug") || subscriber.Address == "gage.coates@marcogroupinc.com")
            {
                SmtpClient client = new SmtpClient(m_SMTPhost);
                // Specify the e-mail sender.
                // Create a mailing address that includes a UTF8 character
                // in the display name.
                MailAddress from = new MailAddress(m_address);
                // Specify the message content.
                MailMessage message = new MailMessage(from, subscriber);
                message.Body = body;
                message.BodyEncoding = System.Text.Encoding.UTF8;
                message.Subject = subject;
                message.SubjectEncoding = System.Text.Encoding.UTF8;
                foreach (Attachment att in attachments)
                {
                    message.Attachments.Add(att);
                }
                client.Credentials = new NetworkCredential(m_username, m_password, m_domain);
                // Set the method that is called back when the send operation ends.
                client.SendCompleted += new
                SendCompletedEventHandler(SendCompletedCallback);
                // The userState can be any object that allows your callback 
                // method to identify this send operation.
                // For this example, the userToken is a string constant.
                string userState = "notification";
                client.SendAsync(message, userState);
                Server.WriteLine("- Notified " + subscriber.Address + Environment.NewLine);
                //Console.WriteLine("Sending message... press c to cancel mail. Press any other key to exit.");
                //string answer = Console.ReadLine();
                // If the user canceled the send, and mail hasn't been sent yet,
                // then cancel the pending operation.
                // Clean up.
                //message.Dispose();
            }
        }
        private async void SendCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
        }
        #endregion
        #region Properties
        private string m_SMTPhost;
        private string m_address;
        private string m_username;
        private string m_password;
        private string m_domain;
        private List<MailAddress> m_mailingList;
        #endregion
    }
}
