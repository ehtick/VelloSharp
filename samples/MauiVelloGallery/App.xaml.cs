using Microsoft.Maui.Controls;

namespace MauiVelloGallery;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new NavigationPage(new MainPage());
    }
}
