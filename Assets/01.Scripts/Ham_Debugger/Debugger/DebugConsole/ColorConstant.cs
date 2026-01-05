using UnityEngine;
namespace HAM_DeBugger.Core.Debugging
{

    public static class ColorConstant
    {
        public static string MainThemeColor = "#E27E2B";
        public static string SubThemeColor = "#994b0b";
        public static string SuccessThemeColor = "#00ff55";

        public static Color GetColorFromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }
            return Color.white; // Default Color : White
        }
    }
}