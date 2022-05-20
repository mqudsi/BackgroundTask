// Copyright Mahmoud Al-Qudsi, 2022.
// Released under the MIT Public License. This notice must be kept intact.

using Serilog;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Windows.ApplicationModel.Background;

namespace BackgroundTaskTest
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("A8082001-73F7-4607-8521-60F03476E462")]
    [ComSourceInterfaces(typeof(IBackgroundTask))]
    public sealed class BackgroundTask : IBackgroundTask
    {
        private static ManualResetEventSlim BackgroundTaskComplete = new(false);
        public static WaitHandle CompleteHandle => BackgroundTaskComplete.WaitHandle;
        public static TimeSpan MaximumExpectedRunTime = TimeSpan.FromMinutes(2);

        private CancellationTokenSource _cancel = new();

        public BackgroundTask()
        {
            Log.Information($"New {nameof(BackgroundTask)} instance created");
        }

        [MTAThread]
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            Log.Information("BackgroundTask::Run() called");

            taskInstance.Canceled += OnCanceled;
            var deferral = taskInstance.GetDeferral();

            try
            {
                // Handle background task here
            }
            finally
            {
                Log.Information("Background task run completed successfully");

                deferral.Complete();
                BackgroundTaskComplete.Set();
                taskInstance.Canceled -= OnCanceled;
                _cancel.Dispose();
            }
        }

        [MTAThread]
        public void OnCanceled(IBackgroundTaskInstance taskInstance, BackgroundTaskCancellationReason cancelReason)
        {
            Log.Warning("Background task cancelled! Reason: {CancelReason}", cancelReason);
            _cancel.Cancel();
        }

        abstract class BackgroundName
        {
            public const string SystemTimer = nameof(SystemTimer);
        }

        /// <summary>
        /// Registers the background task to run at the specified interval.
        ///
        /// As this uses the COM/CLSID-based IBackgroundTask pattern, it is not available except on Windows 10 19041 and above.
        /// </summary>
        [UnsupportedOSPlatform("windows")]
        [SupportedOSPlatform("windows10.0.19041")]
        public static bool Register(TimeSpan interval)
        {
            if (interval.TotalMinutes < 15)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Time trigger interval cannot be less than 15 minutes!");
            }

            Log.Information("Attempting to register background tasks via COM");
            var builder = new BackgroundTaskBuilder();

            var result = RegisterTask(
                builder,
                BackgroundName.SystemTimer,
                new TimeTrigger((uint) interval.TotalMinutes, oneShot: false),
                condition: null);

            if (result is not null)
            {
                Log.Debug("Registered COM background tasks with system");
                return true;
            }

            return false;
        }

        [UnsupportedOSPlatform("windows")]
        [SupportedOSPlatform("windows10.0.19041")]
        private static BackgroundTaskRegistration?
            RegisterTask(BackgroundTaskBuilder builder, string taskName, IBackgroundTrigger trigger, IBackgroundCondition? condition = null)
        {
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == taskName)
                {
                    // Already registered
                    Log.Verbose("Unregistering existing copy of background task {TaskName}", cur.Value.Name);
                    cur.Value.Unregister(false);
                    break;
                }
            }

            builder.Name = taskName;
            builder.SetTrigger(trigger);
            builder.SetTaskEntryPointClsid(typeof(BackgroundTask).GUID);

            if (condition is not null)
            {
                builder.AddCondition(condition);
            }

            try
            {
                Log.Verbose("Registering background task {TaskName}", taskName);
                return builder.Register();
            }
            catch
            {
                Log.Error("Failed to register background task {TaskName} with trigger type {TriggerType}", taskName, trigger switch
                {
                    SystemTrigger systemTrigger => Enum.GetName(systemTrigger.TriggerType),
                    TimeTrigger timeTrigger => $"Timer: {timeTrigger.FreshnessTime}",
                    _ => "Unknown",
                });
                return null;
            }
        }
    }
}
