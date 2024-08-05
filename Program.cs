using System.Runtime.ConstrainedExecution;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using veeam_task.helpers;

#pragma warning disable CS8600, CS8601, CS8602, CS8604 

namespace veeam_task
{
    class Program
    {
        private static string sourcePath = "";
        private static string replicaPath = "";
        private static int syncInterval;
        private static string logFile = "";
        private static string changesFile = "";
        private static string lastChangedFile = "";
        private static string lastCreatedFile = "";
        

        static async Task Main(string[] args)
        {
            GetInfoFromUser();

            PathHelper.CheckDir(sourcePath);
            PathHelper.CheckDir(replicaPath);

            PathHelper.FirstFileCheck(sourcePath, replicaPath);

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                CancellationToken token = cts.Token;

                File.WriteAllText(changesFile, "");
            
                Task watcherTask = Task.Run(() => CreateFileWatcher(token), token);
                Task monitorTask = Task.Run(() => MonitorChanges(token), token);

                Console.WriteLine("Press 'q' to quit.");

                while (Console.Read() != 'q') ;

                cts.Cancel();

                // Wait for the tasks to finish
                await Task.WhenAll(watcherTask, monitorTask);

            }
        }

        private static void GetInfoFromUser()
        {
            do
            {
                Console.WriteLine("Enter the source folder path");
                sourcePath = Console.ReadLine();
                sourcePath = PathHelper.CheckDirSeparator(sourcePath);
            } while(sourcePath == "");

            do
            {
                Console.WriteLine("Enter the replica folder path (must be different from the source folder)");
                replicaPath = Console.ReadLine();
                replicaPath = PathHelper.CheckDirSeparator(replicaPath);
            } while(replicaPath == "" || replicaPath == sourcePath);

            do
            {
                Console.WriteLine("Enter the log file path: ");
                string logFileAux = Console.ReadLine();
                logFile = PathHelper.LogPath(logFileAux);
                changesFile = PathHelper.ChangesPath(logFileAux);
            } while (logFile == "");

            do
            {
                Console.WriteLine("Enter the sync interval in seconds: ");
                syncInterval = Convert.ToInt32(Console.ReadLine());
                syncInterval = syncInterval * 1000;
            } while(syncInterval < 1);
        }
        
        /*
        The file watcher watches the source folder for changes in files and logs them
        */

        private static void CreateFileWatcher(CancellationToken token)
        {
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = sourcePath,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            

            watcher.Created += OnFileCreated;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Error += OnWatcherError;
            watcher.EnableRaisingEvents = true;
            
            while(!token.IsCancellationRequested)
            {
                Thread.Sleep(200);
            }

            watcher.EnableRaisingEvents = false;
        }

        /*
        Checks the changes file for changes and applies them to the replica folder
        */

        private static void MonitorChanges(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(syncInterval);
                if (File.Exists(changesFile))
                {
                    string existingJson = File.ReadAllText(changesFile);
                    File.WriteAllText(changesFile, "");
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        List<ChangesEvents> changesList = JsonSerializer.Deserialize<List<ChangesEvents>>(existingJson);
                        foreach (var change in changesList)
                        {
                            if (change.Event == "created" || change.Event == "changed")
                            {
                                File.Copy($"{sourcePath}{change.File}", $"{replicaPath}{Path.GetFileName(change.File)}", true);
                            }
                            else if (change.Event == "deleted")
                            {
                                File.Delete($"{replicaPath}{Path.GetFileName(change.File)}");
                            }
                        }
                        Console.WriteLine("Changes have bee made to the replica folder");
                    }
                }
            }
        }

        // Event handlers
        private static void OnFileCreated(object source, FileSystemEventArgs e)
        {
            if (e.FullPath != lastCreatedFile)
            {
                WriteToFiles(e.FullPath, "created");
                Console.WriteLine($"File created: {e.Name}");
            }
            lastCreatedFile = e.FullPath;
        }

        private static void OnFileChanged(object source, FileSystemEventArgs e)
        {
            if (e.FullPath == lastChangedFile)
            {
                Thread.Sleep(500);
                lastChangedFile = "";
                return;
            }

            lastChangedFile = e.FullPath;
            
            WriteToFiles(e.FullPath, "changed");
            Console.WriteLine($"File changed: {e.Name}");

        }

        private static void OnFileDeleted(object source, FileSystemEventArgs e)
        {
            WriteToFiles(e.FullPath, "deleted");
            Console.WriteLine($"File deleted: {e.Name}");
        }

        private static void OnWatcherError(object source, ErrorEventArgs e)
        {
            Console.WriteLine($"Error: {e.GetException().Message}");
            WriteToFiles(e.GetException().Message, "error");
        }

        private static void WriteToFiles(string e, string eventType)
        {
            ChangesEvents changes = new ChangesEvents
            {
                Event = eventType,
                File = e
            };

            WriteToLogFile(e, changes);
            WriteToChangesFile(e, changes);
        }

        /*
        Writes the changes to the log file
        */

        private static void WriteToLogFile(string e, ChangesEvents changes)
        {
            List<ChangesEvents> changesList = new List<ChangesEvents>();

            if (File.Exists(logFile))
            {
                string existingJson = File.ReadAllText(logFile);
                if (!string.IsNullOrWhiteSpace(existingJson))
                {
                    changesList = JsonSerializer.Deserialize<List<ChangesEvents>>(existingJson);
                }
            }
            changesList.Add(changes);

            string json = JsonSerializer.Serialize(changesList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(logFile, json);
        }

        /*
        Writes the changes to the changes file
        */

        private static void WriteToChangesFile(string e, ChangesEvents changes)
        {
            changes.File = Path.GetFileName(e);
            List<ChangesEvents> changesList = new List<ChangesEvents>();
            if (File.Exists(changesFile))
            {
                string existingJson = File.ReadAllText(changesFile);
                if (!string.IsNullOrWhiteSpace(existingJson))
                {
                    changesList = JsonSerializer.Deserialize<List<ChangesEvents>>(existingJson);
                }
            }
            bool fileExists = false;
            foreach (var change in changesList)
            {
                if (change.File == changes.File)
                {
                    change.Event = changes.Event;
                    fileExists = true;
                    break;
                }
            }

            if (!fileExists)
            {
                changesList.Add(changes);
            }

            string json = JsonSerializer.Serialize(changesList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(changesFile, json);
        }
    }
}