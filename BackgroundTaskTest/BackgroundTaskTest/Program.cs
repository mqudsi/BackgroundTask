// Copyright Mahmoud Al-Qudsi, 2022.
// Released under the MIT Public License. This notice must be kept intact.

using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace BackgroundTaskTest
{
#if DISABLE_XAML_GENERATED_MAIN
    public static class Program
    {
        [MTAThread]
        static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            InitializeLogger();

            if (args.Contains("-ComBackgroundActivated", StringComparer.InvariantCultureIgnoreCase))
            {
                Log.Logger.Information("Application started in the background to handle COM background events");

                // Register COM background task factory
                Log.Information("Registering background task class factory to service background activation");

                using var comRegistration = RegistrationServices.Register<BackgroundTask, IBackgroundTask>();
                if (!BackgroundTask.CompleteHandle.WaitOne(BackgroundTask.MaximumExpectedRunTime))
                {
                    Log.Fatal("Timeout waiting for COM-invoked background task to run or complete!");
                }
                Log.CloseAndFlush();
            }
            else
            {
                Log.Logger.Information("Application (re)started");
                var staThread = new Thread(StartApp);
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
            }
        }

        static void StartApp()
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }

        static void InitializeLogger()
        {
            var temp = ApplicationData.Current.TemporaryFolder.Path;
            var logDir = Path.Combine(temp, "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            var configuration = new LoggerConfiguration();
            configuration.MinimumLevel.Verbose();

            var logger = configuration
                .WriteTo.File($"{logDir}\\BackgroundTaskTest-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Log.Logger = logger;
        }
    }
#endif
}
