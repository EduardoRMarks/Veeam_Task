namespace veeam_task.helpers
{
    public class PathHelper
    {

        /*
        Sees if the path ends with a directory separator, if not, it adds one
        */

        public static string CheckDirSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                path += Path.DirectorySeparatorChar;
            }
            return path;
        }

        /*
        Does the same as CheckDirSeparator, but also adds the log.json file to the path
        */

        public static string LogPath(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                path += Path.DirectorySeparatorChar;
            }
            
            CheckDir(path);
            path += "log.json";
            CreateLogChangesFile(path);

            return path;
        }

        /*
        Does the same as CheckDirSeparator, but also adds the changes.json file to the path
        */

        public static string ChangesPath(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                path += Path.DirectorySeparatorChar;
            }
            
            CheckDir(path);
            path += "changes.json";
            CreateLogChangesFile(path);

            return path;
        }

        /*
        A check is made to see if the source and replica folders exists, if not, they are created
        */

        public static void CheckDir(string path)
        {
            bool sourceDirExists = Directory.Exists(path);

            if (!sourceDirExists)
            {
                Directory.CreateDirectory(path);
                return;
            }
        }

        /*
        The files migth be changed while the program is not running, so we copy them to the replica folder
        */
        public static void FirstFileCheck(string sourcePath, string replicaPath)
        {
            string[] sourcefiles = Directory.GetFiles(sourcePath);
            string[] replicafiles = Directory.GetFiles(replicaPath);

            foreach (var file in replicafiles)
            {
                File.Delete(file);
            }

            foreach (var file in sourcefiles)
            {
                File.Copy(file, $"{replicaPath}{Path.GetFileName(file)}", true);
            }

        }

        /*
        Creates files if they do not exist
        */

        private static void CreateLogChangesFile(string path)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Close();
            }
        }
    }
}