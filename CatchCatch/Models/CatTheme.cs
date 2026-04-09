namespace CatchCatch.Models;

public enum CatTheme
{
    Cat,
    Cat2,
    Cat3,
}

public static class CatThemeExtensions
{
    public static string ToWireString(this CatTheme theme) => theme switch
    {
        CatTheme.Cat => "cat",
        CatTheme.Cat2 => "cat2",
        CatTheme.Cat3 => "cat3",
        _ => "cat",
    };

    public static CatTheme FromWireString(string? s) => s switch
    {
        "cat2" => CatTheme.Cat2,
        "cat3" => CatTheme.Cat3,
        _ => CatTheme.Cat,
    };

    public static string IdleImage(this CatTheme theme) => theme switch
    {
        CatTheme.Cat => "pack://application:,,,/Assets/cat_idle.png",
        CatTheme.Cat2 => "pack://application:,,,/Assets/cat2_idle.png",
        CatTheme.Cat3 => "pack://application:,,,/Assets/cat3_idle.png",
        _ => "pack://application:,,,/Assets/cat_idle.png",
    };

    public static string ActiveImage(this CatTheme theme) => theme switch
    {
        CatTheme.Cat => "pack://application:,,,/Assets/cat_active.png",
        CatTheme.Cat2 => "pack://application:,,,/Assets/cat2_active.png",
        CatTheme.Cat3 => "pack://application:,,,/Assets/cat3_active.png",
        _ => "pack://application:,,,/Assets/cat_active.png",
    };

    public static string DisplayName(this CatTheme theme) => theme switch
    {
        CatTheme.Cat => "Gray Cat",
        CatTheme.Cat2 => "White Cat",
        CatTheme.Cat3 => "Calico Cat",
        _ => "Gray Cat",
    };
}
