using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.ServiceBus.Messaging;

namespace EventHubSample
{
    public class SensorDataItem
    {
        public decimal OilTemperature;
        public decimal PumpPressure;
        public decimal MeasuredWearX;
        public decimal MeasuredWearY;
    }

    public class AccelerometerDataItem
    {
        public decimal X;
        public decimal Y;
        public decimal Z;
    }


    public class Program
    {
        public static object _dummy = new object();

        private static string _connectionString = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString"];

        static void Main(string[] args)
        {
            Console.WriteLine("EventHub Sensor Data Data Generator Sample - DevWeek 2016 Edition");
            Console.WriteLine("(c) Davide Mauri, 2016");
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Which simulation do you want to run?");
                Console.WriteLine("Press 'c' during simulation to cancel it.");
                Console.WriteLine("[1] Sold Products");
                Console.WriteLine("[2] Elevator Sensors");
                Console.WriteLine("[q] Quit");

                Char c = Console.ReadKey(true).KeyChar;
                if (c == 'q') break;
                switch (c)
                {
                    case '1':
                        SoldProductDemo();
                        break;
                    case '2':
                        SensorReadingsDemo();
                        break;                   
                }
            }
        }

        private static void SoldProductDemo()
        {
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            int numOfTasks = 10;

            Task[] tasks = new Task[numOfTasks];
            for (int t = 0; t < numOfTasks; t++)
            {
                int nt = t + 1;
                Task newTask = Task.Factory.StartNew(() => SendSoldProducts(nt, token), TaskCreationOptions.LongRunning);
                tasks[t] = newTask;
            }

            RunTasks(tasks, tokenSource);
        }
        private static void SendSoldProducts(int taskNum, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

            var eventHubClient = EventHubClient.CreateFromConnectionString(_connectionString);

            List<string> rows = new List<string>();
            rows.Add(@"{'Name':'Laptop','Category':'Computers'}");
            rows.Add(@"{'Name':'Desktop','Category':'Computers'}");
            rows.Add(@"{'Name':'Headphones','Category':'Audio'}");
            rows.Add(@"{'Name':'LCD Tv','Category':'TV'}");

            Random rnd = new Random(taskNum);
            while (true)
            {
                try
                {
                    int r = rnd.Next(rows.Count());
                    JObject jsonMessage = JObject.Parse(rows[r]);

                    jsonMessage.Add("CustomerId", taskNum.ToString());
                    jsonMessage.Add("SoldDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    jsonMessage.Add("Quantity", rnd.Next(1, 3).ToString());
                    jsonMessage.Add("Value", rnd.Next(50, 1000).ToString());

                    Console.WriteLine("{0}", jsonMessage.ToString());

                    EventData ed = new EventData(Encoding.UTF8.GetBytes(jsonMessage.ToString()));
                    ed.PartitionKey = taskNum.ToString();
                    eventHubClient.Send(ed);
                }
                catch (Exception exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[{2}] {0} > Exception: {1}", DateTime.Now, exception.Message, taskNum);
                    Console.ResetColor();
                }

                if (ct.IsCancellationRequested) break;

                Thread.Sleep(rnd.Next(200, 1500));
            }
        }

        private static void SensorReadingsDemo()
        {
            Console.WriteLine("Loading sample data...");
            List<SensorDataItem> sensorDataSamples = new List<SensorDataItem>();
            IEnumerable<string> sensorDataRaw = File.ReadLines("data/sensor-data.csv", UTF8Encoding.UTF8);
            int r = 0;
            foreach (string s in sensorDataRaw)
            {
                if (r != 0)
                {
                    string[] splitString = s.Split(',');

                    SensorDataItem sdi = new SensorDataItem();
                    sdi.OilTemperature = decimal.Parse(splitString[1], System.Globalization.CultureInfo.InvariantCulture);
                    sdi.PumpPressure = decimal.Parse(splitString[2], System.Globalization.CultureInfo.InvariantCulture);
                    sdi.MeasuredWearX = decimal.Parse(splitString[3], System.Globalization.CultureInfo.InvariantCulture);
                    sdi.MeasuredWearY = decimal.Parse(splitString[4], System.Globalization.CultureInfo.InvariantCulture);

                    sensorDataSamples.Add(sdi);
                }

                r += 1;
            }
            Console.WriteLine("Done.");

            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            int numOfTasks = 10;

            Task[] tasks = new Task[numOfTasks];
            for (int t = 0; t < numOfTasks; t++)
            {
                int nt = t + 1;
                Task newTask = new Task(() => SendSensorData(nt, token, sensorDataSamples), TaskCreationOptions.LongRunning);
                tasks[t] = newTask;
                newTask.Start();
            }
            
            RunTasks(tasks, tokenSource);
        }
        private static void SendSensorData(int taskNum, CancellationToken ct, List<SensorDataItem> sensorDataSamples)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

            var eventHubClient = EventHubClient.CreateFromConnectionString(_connectionString);

            string status = "Good";
            int itemCount = 50;
            int offesetItemCount = 0;
            int sendCount = 0;

            Random rnd = new Random(taskNum);
            while (true)
            {                
                try
                {
                    int r = offesetItemCount + rnd.Next(itemCount);
                    JObject jsonMessage = JObject.FromObject(sensorDataSamples[r]);

                    jsonMessage.Add("SensorId", taskNum.ToString());
                    jsonMessage.Add("MeasurementDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                  
                    eventHubClient.Send(new EventData(Encoding.UTF8.GetBytes(jsonMessage.ToString())));
                    sendCount += 1;

                    UpdateConsole(taskNum, string.Format("{0} ({1})", status.PadRight(20), sendCount));
                }
                catch (Exception exception)
                {
                    UpdateConsole(taskNum, exception.Message);
                }

                if (ct.IsCancellationRequested) break;

                Thread.Sleep(1000);

                if (status == "Good" && rnd.Next(1000) > 991) { status = "Degraded"; offesetItemCount = 50; }
                if (status == "Degraded" && rnd.Next(1000) > 991) { status = "Immediate Maitenance"; offesetItemCount = 100; }
            }
        }
        
        private static void RunTasks(Task[] tasks, CancellationTokenSource tokenSource)
        {
            Console.Clear();
            while (true)
            {
                if (Console.ReadKey(true).KeyChar == 'c')
                {
                    tokenSource.Cancel();
                    lock (_dummy)
                    {
                        Console.CursorLeft = 0;
                        Console.CursorTop = 13;

                        Console.WriteLine("Task cancellation requested.");
                        Console.WriteLine("Waiting for all tasks to finish...");
                    }
                    try
                    {
                        Task.WaitAll(tasks);
                        Console.Clear();
                    }
                    catch (AggregateException e)
                    {
                        foreach (var v in e.InnerExceptions)
                            Console.WriteLine("Task Exception Message: " + v.Message);
                    }

                    for (int i = 0; i < tasks.Length; i++)
                    {
                        Console.WriteLine("[{0}] status is now {1}.", i + 1, tasks[i].Status);
                    }

                    Console.WriteLine("Simulation finished.");

                    break;
                }
            }
        }
        private static void UpdateConsole(int taskNum, string message)
        {
            lock(_dummy)
            {
                Console.CursorLeft = 0;
                Console.CursorTop = taskNum;

                Console.Write("{0:00}: {1}", taskNum, message);
            }
        }
    }
}
