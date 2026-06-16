namespace CFS.Core.Services;

public static class SpanishMoneyWriter
{
    private static readonly string[] Units =
    [
        "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
        "diez", "once", "doce", "trece", "catorce", "quince", "dieciseis", "diecisiete",
        "dieciocho", "diecinueve", "veinte", "veintiuno", "veintidos", "veintitres",
        "veinticuatro", "veinticinco", "veintiseis", "veintisiete", "veintiocho", "veintinueve"
    ];

    private static readonly string[] Tens =
    [
        "", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"
    ];

    private static readonly string[] Hundreds =
    [
        "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos",
        "seiscientos", "setecientos", "ochocientos", "novecientos"
    ];

    public static string ToDollars(decimal amount)
    {
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var whole = (long)Math.Truncate(rounded);
        var cents = (int)((rounded - whole) * 100);
        return $"{ToWords(whole)} dolares con {cents:00}/100".ToUpperInvariant();
    }

    private static string ToWords(long value)
    {
        if (value == 0)
        {
            return Units[0];
        }

        if (value < 0)
        {
            return $"menos {ToWords(Math.Abs(value))}";
        }

        if (value < 1000)
        {
            return HundredsBlock((int)value);
        }

        if (value < 1_000_000)
        {
            var thousands = value / 1000;
            var remainder = value % 1000;
            var prefix = thousands == 1 ? "mil" : $"{ToWords(thousands)} mil";
            return remainder == 0 ? prefix : $"{prefix} {HundredsBlock((int)remainder)}";
        }

        var millions = value / 1_000_000;
        var millionRemainder = value % 1_000_000;
        var millionPrefix = millions == 1 ? "un millon" : $"{ToWords(millions)} millones";
        return millionRemainder == 0 ? millionPrefix : $"{millionPrefix} {ToWords(millionRemainder)}";
    }

    private static string HundredsBlock(int value)
    {
        if (value == 0)
        {
            return string.Empty;
        }

        if (value == 100)
        {
            return "cien";
        }

        if (value < 30)
        {
            return Units[value];
        }

        if (value < 100)
        {
            var ten = value / 10;
            var unit = value % 10;
            return unit == 0 ? Tens[ten] : $"{Tens[ten]} y {Units[unit]}";
        }

        var hundred = value / 100;
        var remainder = value % 100;
        return remainder == 0 ? Hundreds[hundred] : $"{Hundreds[hundred]} {HundredsBlock(remainder)}";
    }
}
