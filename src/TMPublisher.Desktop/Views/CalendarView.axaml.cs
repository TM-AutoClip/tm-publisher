using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TMPublisher.Desktop.ViewModels;

namespace TMPublisher.Desktop.Views;

/// <summary>Calendar page: dragging a post onto another day moves it (the view model decides whether it may).</summary>
public sealed partial class CalendarView : UserControl
{
    private static readonly DataFormat<string> PostFormat = DataFormat.CreateStringApplicationFormat("tm-publisher-post");

    /// <summary>Creates.</summary>
    public CalendarView() => InitializeComponent();

    private CalendarViewModel? Model => DataContext as CalendarViewModel;

    private void OnDayLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control day)
        {
            DragDrop.AddDropHandler(day, OnDrop);
        }
    }

    private async void OnItemPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (sender is Control { DataContext: CalendarItemViewModel item } control && item.CanMove && e.ClickCount == 1
                && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(PostFormat, item.Post.Id.ToString()));
                await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Model?.Report(ex);
        }
    }

    private async void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is Control { DataContext: CalendarItemViewModel item } && Model is { } model)
            {
                await model.OpenAsync(item).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Model?.Report(ex);
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (sender is Control { DataContext: CalendarDayViewModel day } && Model is { } model
                && e.DataTransfer.TryGetValue(PostFormat) is { } id && Guid.TryParse(id, out var postId))
            {
                await model.MoveAsync(postId, day.Date).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Model?.Report(ex);
        }
    }
}
