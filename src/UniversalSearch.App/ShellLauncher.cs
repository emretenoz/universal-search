using System.Diagnostics;

namespace UniversalSearch.App;

internal static class ShellLauncher
{
    public static Task OpenAsync(string path)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                // Shell extensions and file associations may need an STA and may block.
                // Never run the OS handoff or filesystem existence probes on the dispatcher.
                if (path.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal))
                    Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = true })?.Dispose();
                else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
                completion.TrySetResult();
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        }) { IsBackground = true, Name = "UniversalSearch.ShellLaunch" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
