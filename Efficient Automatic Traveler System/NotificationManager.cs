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
        public void PushNotification(string subject, string body)
        {
            foreach (MailAddress subscriber in m_mailingList)
            {
                SendMail(subject, body, subscriber);
            }
        }
        #endregion

        #region Private Methods
        private void SendMail(string subject, string body, MailAddress subscriber)
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

            client.Credentials = new NetworkCredential(m_username, m_password, m_domain);
            // Set the method that is called back when the send operation ends.
            client.SendCompleted += new
            SendCompletedEventHandler(SendCompletedCallback);
            // The userState can be any object that allows your callback 
            // method to identify this send operation.
            // For this example, the userToken is a string constant.
            string userState = "notification";
            client.SendAsync(message, userState);
            //Console.WriteLine("Sending message... press c to cancel mail. Press any other key to exit.");
            //string answer = Console.ReadLine();
            // If the user canceled the send, and mail hasn't been sent yet,
            // then cancel the pending operation.
            // Clean up.
            //message.Dispose();
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
