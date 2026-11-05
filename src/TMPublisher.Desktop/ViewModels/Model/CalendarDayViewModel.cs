using System.Collections.ObjectModel;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A day cell.</summary>
public sealed class CalendarDayViewModel(DateTime date, bool inRange, bool isToday)
{
    /// <summary>Local date.</summary>
    public DateTime Date { get; } = date;

    /// <summary>Day of month.</summary>
    public string Number { get; } = date.Day.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Inside the shown month (always true in the week view).</summary>
    public bool InRange { get; } = inRange;

    /// <summary>Today.</summary>
    public bool IsToday { get; } = isToday;

    /// <summary>Posts of the day by time.</summary>
    public ObservableCollection<CalendarItemViewModel> Items { get; } = [];
}
