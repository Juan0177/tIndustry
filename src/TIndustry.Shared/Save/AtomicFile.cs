namespace TIndustry.Shared;

/// <summary>Crash-safe writes: content goes to <c>&lt;path&gt;.tmp</c> beside the target, then replaces it in one move.</summary>
internal static class AtomicFile
{
    public const string TempSuffix = ".tmp";

    public static void WriteAllText(string path, string contents) =>
        Replace(path, tmp => File.WriteAllText(tmp, contents));

    public static void Copy(string source, string path) =>
        Replace(path, tmp => File.Copy(source, tmp, overwrite: true));

    private static void Replace(string path, Action<string> writeTemp)
    {
        var tmp = path + TempSuffix;
        try
        {
            writeTemp(tmp);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Leave no stray temp file behind; the previous target is untouched.
            try
            {
                if (File.Exists(tmp))
                {
                    File.Delete(tmp);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            throw;
        }
    }
}
