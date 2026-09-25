namespace Obhijatri.Bangla;

public enum NumberGrouping
{
    /// <summary>1234567</summary>
    None,

    /// <summary>1,234,567 (international)</summary>
    Thousands,

    /// <summary>12,34,567 (lakh and crore, common in Bangladesh)</summary>
    Lakh,
}
