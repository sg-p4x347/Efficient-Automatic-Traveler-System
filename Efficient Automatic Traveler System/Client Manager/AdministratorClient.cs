using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class AdministratorClient : Client
    {
        public AdministratorClient(TcpClient client) : base(client)
        {
            AccessLevel = AccessLevel.Administrator;
        }
        //public override ClientMessage Login(string json)
        //{
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        ClientMessage message = base.Login(json);
        //        if (message.Method == "LoginSuccess")
        //        {
        //            Dictionary<string, string> paramObj = new Dictionary<string, string>()
        //            {
        //                {"user",message.Parameters }
        //            };
        //            if (m_user.Login(obj["PWD"]))
        //            {
        //                return new ClientMessage("LoginSuccess", paramObj.Stringify());
        //            }
        //            else
        //            {
        //                return new ClientMessage("LoginPopup", ("Invalid password").Quotate());
        //            }
        //        }
        //        else
        //        {
        //            return message;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        return new ClientMessage("LoginPopup", ("System error! oops...").Quotate());
        //    }
        //}
        public ClientMessage LoadUsers(string json)
        {
            return new ClientMessage("LoadUsers", UserManager.Export());
        }
        public ClientMessage LoadConfig(string json)
        {
            return new ClientMessage("LoadConfig", ConfigManager.Export());
        }
    }
}
