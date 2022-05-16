using System.Diagnostics;

namespace Obj2Tiles.Common
{
    public class TempFile : IDisposable
    {

        public string FilePath { get; }

        public TempFile(string url, string domain = "temp")
        {

            var uri = new Uri(url);

            var fileName = Path.GetFileName(uri.LocalPath);

            var folder = Path.Combine(Path.GetTempPath(), domain);
            FilePath = Path.Combine(folder, fileName);

            Debug.WriteLine("Temp file: " + FilePath);

            var info = new FileInfo(FilePath);

            if (!info.Exists || info.Length == 0)
            {

                Directory.CreateDirectory(folder);

                Debug.WriteLine("File does not exist, downloading it");
                
                HttpHelper.DownloadFileAsync(url, FilePath).Wait();
                
                Debug.WriteLine("File downloaded");
            }
            else
            {
                Debug.WriteLine("File already existing, leveraging temp folder1");
            }
        }

        public static void CleanDomain(string domain)
        {
            var folder = Path.Combine(Path.GetTempPath(), domain);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        public void Dispose()
        {
            Debug.WriteLine("Deleting: " + FilePath);
            File.Delete(FilePath);
            Debug.WriteLine("Deleted temp file");
        }
    }
}
