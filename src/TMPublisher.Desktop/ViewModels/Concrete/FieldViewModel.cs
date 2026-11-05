using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Connections;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>An input of the connection window.</summary>
public sealed partial class FieldViewModel(ConnectionField field, string label, string? hint) : ObservableObject
{
    /// <summary>Field.</summary>
    public ConnectionField Field { get; } = field;

    /// <summary>Label.</summary>
    public string Label { get; } = label + (field.Required && !field.ReadOnly ? " *" : string.Empty);

    /// <summary>Hint below the box.</summary>
    public string Hint { get; } = hint ?? string.Empty;

    /// <summary>Whether a hint exists.</summary>
    public bool HasHint => Hint.Length > 0;

    /// <summary>Masked.</summary>
    public bool IsSecret => Field.Secret;

    /// <summary>Only shown for copying.</summary>
    public bool IsReadOnly => Field.ReadOnly;

    /// <summary>Value.</summary>
    [ObservableProperty]
    public partial string Value { get; set; } = field.DefaultValue ?? string.Empty;
}
