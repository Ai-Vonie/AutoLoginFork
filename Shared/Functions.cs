namespace XIVPlugins.Shared
{
    public static class Functions
    {
        public static void OpenWebsite(string url)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
} 