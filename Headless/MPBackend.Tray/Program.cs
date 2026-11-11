namespace MPBackend.Tray;

internal static class Program
{
    private const string MutexName = @"Local\MPBackend.Tray.SingleInstance";
    private const string ShowEventName = @"Local\MPBackend.Tray.Show";

    [STAThread]
    private static void Main()
    {
        // the MP libraries are static state and the port can be bound once: one instance per session.
        // A second start just asks the running instance to show its window.
        using var mutex = new Mutex(true, MutexName, out var first);
        if (!first)
        {
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ShowEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
        {
            LogBuffer.Add(new LogLine(DateTime.Now, "ERROR", "UI", e.Exception.ToString()));
            MessageBox.Show(e.Exception.Message, "MPBackend", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var options = TrayOptions.Load();
        using var context = new TrayContext(options, showEvent);
        Application.Run(context);
    }
}
