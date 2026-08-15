namespace LessMouseWin.Services;

public static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "LessMouse");
        }
    }
}
