using MPBackend.Core;

namespace MPBackend.App;

public partial class App : Application
{
    private readonly Backend backend;

    public App(Backend backend)
    {
        this.backend = backend;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "MP GCS" };
        // close links and save settings when the (only) window goes away
        window.Destroying += (_, _) => backend.Dispose();
        return window;
    }
}
