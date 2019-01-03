using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Messaging;
using System.ServiceProcess;
using System.Threading;

namespace FileWacherService
{
    public partial class Service1 : ServiceBase
    {
        private FileSystemWatcher fsw = new FileSystemWatcher();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Thread MonitorThread;

        private CounterCreationDataCollection collection;
        private CounterCreationData counter;
        private PerformanceCounter countsPerSec;

        public Service1(string[] args)
        {
            InitializeComponent();

            if (args.Length > 0)
            {
                Constants.MonitorDir = args[0];
            }
            if (args.Length > 1)
            {
                Constants.IfMonitorSubDir = Convert.ToBoolean(args[1]);
            }
        }
        
        protected override void OnStart(string[] args)
        {
            log.Info("FileSystemWatcher Service started");

            if (!MessageQueue.Exists(@".\private$\ServiceOut"))
            {
                MessageQueue.Create(@".\private$\ServiceOut");
            }
            try
            {
                using (MessageQueue messageQueue = new MessageQueue(@".\private$\ServiceOut"))
                {
                    Message outcome = new Message("FileSystemWatcher Service started", new BinaryMessageFormatter());
                    messageQueue.Send(outcome);
                }
                using (MessageQueue messageQueue = new MessageQueue(@".\private$\ServicePath"))
                {
                    Message outcome = new Message(Constants.MonitorDir, new BinaryMessageFormatter());
                    messageQueue.Send(outcome);
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception: {0}", ex.Message));
            }

            try
            {
                collection = new CounterCreationDataCollection();
                counter = new CounterCreationData();
                counter.CounterName = "CountsPerEvent";
                counter.CounterHelp = "CountsPerEvent";
                counter.CounterType = PerformanceCounterType.NumberOfItems32;
                collection.Add(counter);

                if (!PerformanceCounterCategory.Exists("CounterCategory"))
                {
                    PerformanceCounterCategory.Create("CounterCategory", "This is counter for each file event",
                        PerformanceCounterCategoryType.SingleInstance, collection);
                }
                countsPerSec = new PerformanceCounter("CounterCategory", "CountsPerEvent", false);

            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception: {0}", ex.Message));
                return;
            }
           
            try
            {
                fsw.Path = Constants.MonitorDir;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception: {0}", ex.Message));
                return;
            }
            
            fsw.NotifyFilter = NotifyFilters.DirectoryName |
                               NotifyFilters.FileName |
                               NotifyFilters.LastAccess |
                               NotifyFilters.LastWrite;

            fsw.IncludeSubdirectories = Constants.IfMonitorSubDir;
            fsw.EnableRaisingEvents = true;

            try
            {
                MonitorThread = new Thread(Monitor);
                MonitorThread.Start();
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception: {0}", ex.Message));
                return;
            }
        }

        private void Monitor()
        {
            log.Info("Monitoring " + Constants.MonitorDir);

            fsw.Changed += new FileSystemEventHandler(OnChanged);
            fsw.Created += new FileSystemEventHandler(OnChanged);
            fsw.Deleted += new FileSystemEventHandler(OnChanged);
            fsw.Renamed += new RenamedEventHandler(OnRenamed);
        }

        protected override void OnStop()
        {
            log.Info("Service stopped");

            using (MessageQueue messageQueue = new MessageQueue(@".\private$\ServiceOut"))
            {
                Message outcome = new Message("Service stopped", new BinaryMessageFormatter());
                messageQueue.Send(outcome);
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            log.Info(string.Format("Renamed: {0} to: {1}", e.OldName, e.FullPath));

            try
            {
                countsPerSec.Increment();
                log.Debug(string.Format("Operations counter: {0}", countsPerSec.NextValue()));

                using (MessageQueue messageQueue = new MessageQueue(@".\private$\ServiceOut"))
                {
                    Message outcome = new Message(string.Format("Renamed: {0} to: {1}", e.OldName, e.FullPath), new BinaryMessageFormatter());
                    messageQueue.Send(outcome);
                }
                using (MessageQueue messageQueue = new MessageQueue(@".\private$\ServiceCounter"))
                {
                    Message outcome = new Message(countsPerSec.NextValue(), new BinaryMessageFormatter());
                    messageQueue.Send(outcome);
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception: {0}", ex.Message));
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            log.Info(string.Format("{0} {1}", e.ChangeType, e.FullPath));

            try
            {
                countsPerSec.Increment();
                log.Debug(string.Format("Operations counter: {0}", countsPerSec.NextValue()));

                using (MessageQueue messageQueue = new MessageQueue(@".\private$\ServiceOut"))
                {
                    Message outcome = new Message(string.Format("{0} {1}", e.ChangeType, e.FullPath), new BinaryMessageFormatter());
                    messageQueue.Send(outcome);
                }
                using (MessageQueue messageQueue = new MessageQueue(@".\private$\ServiceCounter"))
                {
                    Message outcome = new Message(countsPerSec.NextValue(), new BinaryMessageFormatter());
                    messageQueue.Send(outcome);
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Exception: {0}", ex.Message));
            }
        }
    }
}
