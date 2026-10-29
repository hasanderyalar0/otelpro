using System.Globalization;

namespace Otel.Web.Components.Shared;

public static class Fmt
{
    static readonly CultureInfo Tr = new("tr-TR");
    public static string Money(decimal v) => v.ToString("N0", Tr) + " ₺";
    public static string Money2(decimal v) => v.ToString("N2", Tr) + " ₺";
    public static string D(DateTime d) => d.ToString("dd.MM.yyyy");
    public static string DT(DateTime d) => d.ToString("dd.MM.yyyy HH:mm");
}
