using System.ComponentModel.DataAnnotations;

namespace CFS.Core.Models;

public sealed class CheckPrintSettings
{
    public decimal SheetOffsetX { get; set; }
    public decimal SheetOffsetY { get; set; }
    public decimal DateLeft { get; set; } = 465;
    public decimal DateTop { get; set; } = 157;
    public decimal PayeeLeft { get; set; } = 84;
    public decimal PayeeTop { get; set; } = 196;
    public decimal AmountLeft { get; set; } = 500;
    public decimal AmountTop { get; set; } = 196;
    public decimal WordsLeft { get; set; } = 36;
    public decimal WordsTop { get; set; } = 220;
    public decimal AddressLeft { get; set; } = 72;
    public decimal AddressTop { get; set; } = 344;
    public decimal MemoLeft { get; set; } = 63;
    public decimal MemoTop { get; set; } = 304;
    public decimal StubTitleLeft { get; set; } = 36;
    public decimal StubTitleTop { get; set; }
    public decimal StubPayeeLeft { get; set; } = 149;
    public decimal StubPayeeTop { get; set; }
    public decimal StubDateLeft { get; set; } = 144;
    public decimal StubDateTop { get; set; } = 24;
    public decimal StubAccountLeft { get; set; } = 36;
    public decimal StubAccountTop { get; set; } = 48;
    public decimal StubMemoLeft { get; set; } = 36;
    public decimal StubMemoTop { get; set; } = 70;
    public decimal StubAmountLeft { get; set; } = 470;
    public decimal StubAmountTop { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public static CheckPrintSettings Defaults() => new();
}

public sealed class CheckPrintSettingsEntry
{
    [Range(-200, 200, ErrorMessage = "El ajuste debe estar entre -200 y 200 puntos.")]
    public decimal SheetOffsetX { get; set; }

    [Range(-200, 200, ErrorMessage = "El ajuste debe estar entre -200 y 200 puntos.")]
    public decimal SheetOffsetY { get; set; }

    [Range(0, 612, ErrorMessage = "La posición horizontal debe estar dentro de la página.")]
    public decimal DateLeft { get; set; }

    [Range(0, 792, ErrorMessage = "La posición vertical debe estar dentro de la página.")]
    public decimal DateTop { get; set; }

    [Range(0, 612, ErrorMessage = "La posición horizontal debe estar dentro de la página.")]
    public decimal PayeeLeft { get; set; }

    [Range(0, 792, ErrorMessage = "La posición vertical debe estar dentro de la página.")]
    public decimal PayeeTop { get; set; }

    [Range(0, 612, ErrorMessage = "La posición horizontal debe estar dentro de la página.")]
    public decimal AmountLeft { get; set; }

    [Range(0, 792, ErrorMessage = "La posición vertical debe estar dentro de la página.")]
    public decimal AmountTop { get; set; }

    [Range(0, 612, ErrorMessage = "La posición horizontal debe estar dentro de la página.")]
    public decimal WordsLeft { get; set; }

    [Range(0, 792, ErrorMessage = "La posición vertical debe estar dentro de la página.")]
    public decimal WordsTop { get; set; }

    [Range(0, 612, ErrorMessage = "La posición horizontal debe estar dentro de la página.")]
    public decimal AddressLeft { get; set; }

    [Range(0, 792, ErrorMessage = "La posición vertical debe estar dentro de la página.")]
    public decimal AddressTop { get; set; }

    [Range(0, 612, ErrorMessage = "La posición horizontal debe estar dentro de la página.")]
    public decimal MemoLeft { get; set; }

    [Range(0, 792, ErrorMessage = "La posición vertical debe estar dentro de la página.")]
    public decimal MemoTop { get; set; }

    public decimal StubTitleLeft { get; set; }
    public decimal StubTitleTop { get; set; }
    public decimal StubPayeeLeft { get; set; }
    public decimal StubPayeeTop { get; set; }
    public decimal StubDateLeft { get; set; }
    public decimal StubDateTop { get; set; }
    public decimal StubAccountLeft { get; set; }
    public decimal StubAccountTop { get; set; }
    public decimal StubMemoLeft { get; set; }
    public decimal StubMemoTop { get; set; }
    public decimal StubAmountLeft { get; set; }
    public decimal StubAmountTop { get; set; }

    public static CheckPrintSettingsEntry FromSettings(CheckPrintSettings settings) => new()
    {
        SheetOffsetX = settings.SheetOffsetX,
        SheetOffsetY = settings.SheetOffsetY,
        DateLeft = settings.DateLeft,
        DateTop = settings.DateTop,
        PayeeLeft = settings.PayeeLeft,
        PayeeTop = settings.PayeeTop,
        AmountLeft = settings.AmountLeft,
        AmountTop = settings.AmountTop,
        WordsLeft = settings.WordsLeft,
        WordsTop = settings.WordsTop,
        AddressLeft = settings.AddressLeft,
        AddressTop = settings.AddressTop,
        MemoLeft = settings.MemoLeft,
        MemoTop = settings.MemoTop,
        StubTitleLeft = settings.StubTitleLeft,
        StubTitleTop = settings.StubTitleTop,
        StubPayeeLeft = settings.StubPayeeLeft,
        StubPayeeTop = settings.StubPayeeTop,
        StubDateLeft = settings.StubDateLeft,
        StubDateTop = settings.StubDateTop,
        StubAccountLeft = settings.StubAccountLeft,
        StubAccountTop = settings.StubAccountTop,
        StubMemoLeft = settings.StubMemoLeft,
        StubMemoTop = settings.StubMemoTop,
        StubAmountLeft = settings.StubAmountLeft,
        StubAmountTop = settings.StubAmountTop
    };
}
