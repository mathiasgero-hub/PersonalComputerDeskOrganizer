using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Registers/removes a per-user Task Scheduler entry that starts the app at
/// logon. A scheduled task with a LogonTrigger was chosen over a plain
/// "Run" registry key because it fires earlier and more predictably in the
/// logon sequence, and does not require administrator rights for a
/// per-user, InteractiveToken-logon task.
///
/// NOTE: Windows does not provide a documented, guaranteed way to be "first"
/// among startup applications — other apps registered the same way race for
/// the same trigger. A LogonTrigger is, in practice, the earliest hook
/// available without writing a custom Windows service.
/// </summary>
public class StartupService
{
    private const string TaskName = "PersonalComputerDeskOrganizer_Startup";

    public bool IsEnabled()
    {
        try
        {
            using var ts = new TaskService();
            return ts.RootFolder.Tasks.Any(t => t.Name == TaskName);
        }
        catch
        {
            return false;
        }
    }

    public void Enable()
    {
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath)) return;

        using var ts = new TaskService();
        TaskDefinition td = ts.NewTask();
        td.RegistrationInfo.Description =
            "Lance PersonalComputerDeskOrganizer à l'ouverture de session.";

        td.Principal.LogonType = TaskLogonType.InteractiveToken;
        td.Triggers.Add(new LogonTrigger());
        td.Actions.Add(new ExecAction(exePath, null, null));

        string userId = WindowsIdentity.GetCurrent().Name;
        td.Principal.UserId = userId;

        ts.RootFolder.RegisterTaskDefinition(
            TaskName, td, TaskCreation.CreateOrUpdate, userId, null, TaskLogonType.InteractiveToken);
    }

    public void Disable()
    {
        try
        {
            using var ts = new TaskService();
            ts.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false);
        }
        catch
        {
            // Nothing to clean up.
        }
    }
}
