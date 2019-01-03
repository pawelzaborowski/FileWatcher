using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Messaging;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using ServiceGui.Annotations;
using Message = System.Messaging.Message;

namespace ServiceGui
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string logs;
        public string Logs
        {
            get => logs;
            set
            {
                logs = value;
                OnPropertyChanged();
            }
        }

        private string dirName;
        public string DirName
        {
            get => dirName;
            set
            {
                dirName = value;
                OnPropertyChanged();
            }
        }

        private float counter = 0;
        public float Counter
        {
            get => counter;
            set
            {
                counter = value;
                OnPropertyChanged();
            }
        }

        public ViewModel()
        {
            QueueChecking();

            QueueUtils();
        }

        private static void QueueChecking()
        {
            if (!MessageQueue.Exists(@".\private$\ServiceOut"))
            {
                MessageQueue.Create(@".\private$\ServiceOut");
            }
            if (!MessageQueue.Exists(@".\private$\ServiceCounter"))
            {
                MessageQueue.Create(@".\private$\ServiceCounter");
            }
            if (!MessageQueue.Exists(@".\private$\ServicePath"))
            {
                MessageQueue.Create(@".\private$\ServicePath");
            }
        }

        private void QueueUtils()
        {
            MessageQueue messageQueue = new MessageQueue(@".\private$\ServiceOut");
            messageQueue.ReceiveCompleted += new ReceiveCompletedEventHandler(mqReceive);
            messageQueue.BeginReceive();

            MessageQueue messageQueueCounter = new MessageQueue(@".\private$\ServiceCounter");
            messageQueueCounter.ReceiveCompleted += new ReceiveCompletedEventHandler(mqReceiveCounter);
            messageQueueCounter.BeginReceive();

            MessageQueue messageQueuePath = new MessageQueue(@".\private$\ServicePath");
            messageQueuePath.ReceiveCompleted += new ReceiveCompletedEventHandler(mqReceivePath);
            messageQueuePath.BeginReceive();
        }

        private void RestartService()
        {
            ServiceController sc = new ServiceController();
            sc.ServiceName = "Service1";
            
            if (sc.Status == ServiceControllerStatus.Running)
            {
                try
                {
                    sc.Stop();
                    var timeout = new TimeSpan(0, 0, 2);
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running);
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void mqReceivePath(object sender, ReceiveCompletedEventArgs e)
        {
            MessageQueue cmq = (MessageQueue)sender;
            try
            {
                Message msg = cmq.EndReceive(e.AsyncResult);
                msg.Formatter = new BinaryMessageFormatter();
                DirName = (String)msg.Body;
            }
            catch (Exception ex)
            {
                Logs = ex.Message;
            }
            cmq.Refresh();
            cmq.BeginReceive();
        }

        private void mqReceiveCounter(object sender, ReceiveCompletedEventArgs e)
        {
            MessageQueue cmq = (MessageQueue)sender;
            try
            {
                Message msg = cmq.EndReceive(e.AsyncResult);
                msg.Formatter = new BinaryMessageFormatter();
                Counter = (float)msg.Body;
            }
            catch (Exception ex)
            {
                Logs = ex.Message;
            }
            cmq.Refresh();
            cmq.BeginReceive();
        }

        private void mqReceive(object sender, ReceiveCompletedEventArgs e)
        {
            MessageQueue cmq = (MessageQueue)sender;
            try
            {
                Message msg = cmq.EndReceive(e.AsyncResult);
                msg.Formatter = new BinaryMessageFormatter();
                Logs += (String)msg.Body;
                Logs += '\n';
            }
            catch (Exception ex)
            {
                Logs = ex.Message;
            }
            cmq.Refresh();
            cmq.BeginReceive();
        }

    }
}
