using System.IO;

namespace SchoolDirectorTycoon.Desktop;

internal sealed class LanguagePreference(string directory)
{
    private string Pathname => Path.Combine(directory, "language.txt");

    public bool ReadEnglish()
    {
        try
        {
            using var stream = File.OpenRead(Pathname);
            return stream.Length == 2 && stream.ReadByte() == 'e' && stream.ReadByte() == 'n';
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TryWrite(bool english)
    {
        string temporary = Path.Combine(directory, $"language-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, english ? "en" : "ar");
            File.Move(temporary, Pathname, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
