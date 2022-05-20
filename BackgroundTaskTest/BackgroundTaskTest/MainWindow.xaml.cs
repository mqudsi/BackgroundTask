// Copyright Mahmoud Al-Qudsi, 2022.
// Released under the MIT Public License. This notice must be kept intact.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.ApplicationModel.Background;
using WinRT;

namespace BackgroundTaskTest
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void myButton_Click(object sender, RoutedEventArgs e)
        {
            // Incompatible cast:
            //var ppvObject = ((IWinRTObject)(IBackgroundTask)new BackgroundTask()).NativeObject.GetRef();

            //var ppvObject = MarshalInterface<IBackgroundTask>.FromManaged(new BackgroundTask());

            string message;
            if (OperatingSystem.IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                if (BackgroundTask.Register(TimeSpan.FromMinutes(15)))
                {
                    message = "Background tasks registered successfully!";
                }
                else
                {
                    message = "Failed to register background tasks!";
                }
            }
            else
            {
                message = "Windows 10 2004 or greater is required to register COM-based background tasks!";
            }

            var dialog = new ContentDialog()
            {
                Title = "Background Task Registration",
                Content = message,
                CloseButtonText = "Ok",
            };
            dialog.XamlRoot = this.Content.XamlRoot;
            await dialog.ShowAsync(ContentDialogPlacement.Popup);
        }
    }
}
