using System.Diagnostics;
using System.IO.Compression;

namespace Obj2Tiles.Common
{

    /// <summary>
    /// This class is used to setup a test file system contained in a zip file
    /// </summary>
    public class TestFS : IDisposable
    {
        /// <summary>
        /// Path of test archive (zip file)
        /// </summary>
        public string TestArchivePath { get; }

        /// <summary>
        /// Generated test folder (root file system)
        /// </summary>
        public string TestFolder { get; }

        /// <summary>
        /// Base test folder for test grouping
        /// </summary>
        public string BaseTestFolder { get; }

        private readonly string _oldCurrentDirectory = null;

        /// <summary>
        /// Creates a new instance of TestFS
        /// </summary>
        /// <param name="testArchivePath">The path of the test archive</param>
        /// <param name="baseTestFolder">The base test folder for test grouping</param>
        /// <param name="setCurrentDirectory">If it should set the current directory to the test directory</param>
        public TestFS(string testArchivePath, string baseTestFolder = "TestFS", bool setCurrentDirectory = false)
        {
            TestArchivePath = testArchivePath;
            BaseTestFolder = baseTestFolder;

            TestFolder = Path.Combine(Path.GetTempPath(), BaseTestFolder, CommonUtils.RandomString(16));

            Directory.CreateDirectory(TestFolder);

            if (!IsLocalPath(testArchivePath))
            {
                var uri = new Uri(testArchivePath);

                Debug.WriteLine($"Archive path is an url");

                var tempPath = Path.Combine(Path.GetTempPath(), baseTestFolder, uri.Segments.Last());

                if (File.Exists(tempPath))
                {
                    Debug.WriteLine("No need to download, using cached one");
                }
                else
                {
                    Debug.WriteLine("Downloading archive");
                    HttpHelper.DownloadFileAsync(testArchivePath, tempPath).Wait();
                }

                ZipFile.ExtractToDirectory(tempPath, TestFolder);

                // NOTE: Let's leverage the temp folder
                // File.Delete(tempPath);

            }
            else
                ZipFile.ExtractToDirectory(TestArchivePath, TestFolder);

            Debug.WriteLine($"Created test FS '{TestArchivePath}' in '{TestFolder}'");

            if (setCurrentDirectory)
            {
                _oldCurrentDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = TestFolder;

                Debug.WriteLine($"Set current directory to '{TestFolder}'");
            }

        }
        public void Dispose()
        {

            if (_oldCurrentDirectory != null)
            {
                Environment.CurrentDirectory = _oldCurrentDirectory;
                Debug.WriteLine($"Restored current directory to '{_oldCurrentDirectory}'");
            }

            Debug.WriteLine($"Disposing test FS '{TestArchivePath}' in '{TestFolder}");
            try
            {
                Directory.Delete(TestFolder, true);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Cannot recursive delete temp folder: {ex.Message}");
                Debug.WriteLine($"Consider manual cleanup of folder '{TestFolder}'");
            }
        }

        private static bool IsLocalPath(string path)
        {
            return path.StartsWith("file:/") ||
                   !path.StartsWith("http://") && (!path.StartsWith("https://") && !path.StartsWith("ftp://"));
        }

        public static void ClearCache(string baseTestFolder)
        {
            var folder = Path.Combine(Path.GetTempPath(), baseTestFolder);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }
}