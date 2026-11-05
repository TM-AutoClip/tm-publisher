using Avalonia.Data.Converters;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>Small value converters used by the views.</summary>
public static class Converters
{
    /// <summary>Full opacity when supported, faded otherwise.</summary>
    public static readonly IValueConverter SupportedOpacity = new FuncValueConverter<bool, double>(supported => supported ? 1.0 : 0.45);
}
