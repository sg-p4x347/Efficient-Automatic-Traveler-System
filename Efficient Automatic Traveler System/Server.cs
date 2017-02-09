﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Odbc;
using ExtensionMethods = Efficient_Automatic_Traveler_System.ExtensionMethods;

namespace Efficient_Automatic_Traveler_System
{

    class Server
    {
        //------------------------------
        // Public members
        //------------------------------
        public Server()
        {
            m_ip = "127.0.0.1";
            m_port = 8080;
            m_travelerCore = new TravelerCore();
            m_clientManager = new ClientManager(m_ip, m_port,ref m_travelerCore.m_travelers);
            // Subscribe events
            m_travelerCore.TravelersChanged += new TravelersChangedSubscriber(m_clientManager.HandleTravelersChanged);
            m_clientManager.TravelersChanged += new TravelersChangedSubscriber(m_travelerCore.HandleTravelersChanged);


            m_clientManagerThread = new Thread(m_clientManager.Start);
            m_clientManagerThread.Name = "Client Manager";
            m_updateInterval = new TimeSpan(0, 5, 0);
            
        }
        public void Start()
        {
            Console.WriteLine("Server has started on " + m_ip + ":" + m_port.ToString(), Environment.NewLine);
            m_clientManagerThread.Start();


            // start the MAS update loop
            m_travelerCore.CreateTravelers(); // update immediatly upon server start
            Update();
            GetInputAsync();
            while (true) ;
        }
        //------------------------------
        // Private members
        //------------------------------
        private async void GetInputAsync()
        {
            string input =  await Task.Run(() => Console.ReadLine());
            switch (input)
            {
                case "update": m_travelerCore.CreateTravelers(); break;
            }
            GetInputAsync();
        }
        private void Update()
        {
            DateTime current = DateTime.Now;
            TimeSpan timeToGo = current.RoundUp(m_updateInterval).TimeOfDay - current.TimeOfDay;
            Console.WriteLine("Will update again in: " + timeToGo.TotalMinutes + " Minutes");
            m_timer = new System.Threading.Timer(x =>
            {
                m_travelerCore.CreateTravelers();
                Update();
            }, null, timeToGo, Timeout.InfiniteTimeSpan);
        }
        
        //------------------------------
        // Properties
        //------------------------------
        private string m_ip;
        private int m_port;
        private ClientManager m_clientManager;
        private Thread m_clientManagerThread;
        private TimeSpan m_updateInterval;
        private Timer m_timer;
        private TravelerCore m_travelerCore;
    }
    
}
