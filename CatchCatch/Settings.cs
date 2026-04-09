using System.Configuration;

namespace CatchCatch;

/// <summary>
/// Persistent user settings stored in user.config.
/// </summary>
internal sealed class Settings : ApplicationSettingsBase
{
    private static readonly Settings _default = (Settings)Synchronized(new Settings());
    public static Settings Default => _default;

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string CatName
    {
        get => (string)this[nameof(CatName)];
        set => this[nameof(CatName)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("cat")]
    public string CatTheme
    {
        get => (string)this[nameof(CatTheme)];
        set => this[nameof(CatTheme)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public double CatX
    {
        get => (double)this[nameof(CatX)];
        set => this[nameof(CatX)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public double CatY
    {
        get => (double)this[nameof(CatY)];
        set => this[nameof(CatY)] = value;
    }
}
