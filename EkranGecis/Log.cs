namespace EkranGecis;

/// <summary>Sorun giderme için %APPDATA%\EkranGecis\kayit.txt dosyasına kısa kayıtlar yazar.</summary>
static class Log
{
    static readonly object Gate = new();
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EkranGecis", "kayit.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            }
        }
        catch { }
    }
}
