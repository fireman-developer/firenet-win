using System;
using System.Windows.Media;

namespace v2rayN.Converters;

public class MaterialDesignFonts
{
    // رفع ابهام FontFamily
    public static System.Windows.Media.FontFamily MyFont { get; }

    static MaterialDesignFonts()
    {
        try
        {
            var fontFamily = AppManager.Instance.Config.UiItem.CurrentFontFamily;
            if (fontFamily.IsNotEmpty())
            {
                var fontPath = Utils.GetFontsPath();
                MyFont = new System.Windows.Media.FontFamily(new Uri(@$"file:///{fontPath}\"), $"./#{fontFamily}");
            }
        }
        catch
        {
        }
        MyFont ??= new System.Windows.Media.FontFamily("Microsoft YaHei");
    }
}