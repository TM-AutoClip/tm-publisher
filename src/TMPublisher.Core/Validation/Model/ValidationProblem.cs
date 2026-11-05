namespace TMPublisher.Core.Validation;

/// <summary>A reason a target cannot be sent: a localization key and its arguments.</summary>
public sealed record ValidationProblem(string Key, params object[] Args)
{
    /// <summary>English fallback text for logs.</summary>
    public override string ToString() => Args.Length == 0 ? Key : Key + " (" + string.Join(", ", Args) + ")";
}
