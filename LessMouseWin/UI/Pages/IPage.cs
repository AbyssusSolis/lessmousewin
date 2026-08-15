using System.Windows;

namespace LessMouseWin.UI.Pages;

internal interface IPage
{
    FrameworkElement Content { get; }
    void RefreshDynamic();
}
