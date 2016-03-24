using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Ookii.Dialogs.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace ProcessPunisher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string PathToProtect;

        private TraceEventSession session;
        private ETWTraceEventSource source;
        private RegisteredTraceEventParser parser;

        private BackgroundWorker processingWorker;

        public MainWindow()
        {
            InitializeComponent();

            var folderDialogue = new VistaFolderBrowserDialog();
            folderDialogue.SelectedPath = AppDomain.CurrentDomain.BaseDirectory;
            folderDialogue.ShowNewFolderButton = false;

            var result = folderDialogue.ShowDialog();

            if (result.HasValue && result.Value == true)
            {
                PathToProtect = folderDialogue.SelectedPath;

                session = new TraceEventSession("ProcessPunisher", null);
                session.StopOnDispose = true;

                source = new ETWTraceEventSource("ProcessPunisher", TraceEventSourceType.Session);

                parser = new RegisteredTraceEventParser(source);

                Application.Current.Exit += OnAppShutdown;

                session.EnableKernelProvider(KernelTraceEventParser.Keywords.FileIO | KernelTraceEventParser.Keywords.FileIOInit);

                session.Source.Kernel.FileIODirEnum += OnAppReadsDirectory;
                session.Source.Kernel.FileIORead += OnAppReadsFile;
                session.Source.Kernel.FileIOWrite += OnAppWritesFile;
                session.Source.Kernel.FileIOQueryInfo += OnAppGetsFileNfo;

                processingWorker = new BackgroundWorker();
                processingWorker.DoWork += DoBackgroundEventProcessing; ;
                processingWorker.RunWorkerCompleted += OnBackgroundEventProcessingComplete; ;
                processingWorker.RunWorkerAsync();
            }
            else
            {
                // No path selected. Shut down.
                App.Current.Shutdown();
            }
        }

        private void KillIfUnknown(string processName, int processId)
        {
            if (!processName.Equals("Notepad", StringComparison.OrdinalIgnoreCase))
            {
                // If not a recycled or system protected process.
                if (processId != 0 && processId != 4)
                {
                    try
                    {
                        Process victim = Process.GetProcessById(processId);

                        if (victim != null)
                        {
                            victim.Kill();

                            Application.Current.Dispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Normal,
                                (Action)delegate ()
                                {
                                    victimsList.Text += "\n" + processName;
                                }
                            );
                        }
                    }
                    catch
                    {
                        // Catch all errors and ignore them because we're just that ignorant.
                    }                    
                }
            }
        }

        private bool IsFileUnderOurProtection(string filePath)
        {
            if(filePath.Length < PathToProtect.Length)
            {
                return false;
            }

            if (!filePath.Substring(0, PathToProtect.Length).Equals(PathToProtect, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private void OnAppGetsFileNfo(FileIOInfoTraceData obj)
        {
            if(IsFileUnderOurProtection(obj.FileName))
            {
                KillIfUnknown(obj.ProcessName, obj.ProcessID);
            }
        }

        private void OnAppWritesFile(FileIOReadWriteTraceData obj)
        {
            if (IsFileUnderOurProtection(obj.FileName))
            {
                KillIfUnknown(obj.ProcessName, obj.ProcessID);
            }
        }

        private void OnAppReadsFile(FileIOReadWriteTraceData obj)
        {
            if (IsFileUnderOurProtection(obj.FileName))
            {
                KillIfUnknown(obj.ProcessName, obj.ProcessID);
            }
        }

        private void OnAppReadsDirectory(FileIODirEnumTraceData obj)
        {
            if (IsFileUnderOurProtection(obj.DirectoryName))
            {
                KillIfUnknown(obj.ProcessName, obj.ProcessID);
            }
        }

        private void OnBackgroundEventProcessingComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            Debug.WriteLine("All done processing events.");
        }

        private void DoBackgroundEventProcessing(object sender, DoWorkEventArgs e)
        {
            if(session != null)
            {
                session.Source.Process();
            }            
        }

        private void OnAppShutdown(object sender, ExitEventArgs e)
        {
            if (session != null)
            {
                session.Dispose();
            }
        }
    }
}