using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AnimationEditor.Core.Logging;

/// <summary>
/// A text picture of what this process is doing, for a process that will not exit. .NET cannot list managed threads or their
/// stacks from inside, so it reports the OS threads (id, state, what each waits on) plus thread-pool and timer counts.
/// </summary>
public static class ProcessSnapshot
{
    private const int MaxThreadsListed = 80;

    public static string Capture()
    {
        var sb = new StringBuilder();
        try
        {
            using var process = Process.GetCurrentProcess();
            var threads = process.Threads;
            sb.Append($"  pid={Environment.ProcessId} uptime={(DateTime.Now - process.StartTime).TotalSeconds:0}s threads={threads.Count} ")
              .Append($"handles={process.HandleCount} workingSetMB={process.WorkingSet64 / (1024 * 1024)} ")
              .Append($"mainWindow=0x{process.MainWindowHandle.ToInt64():X} \"{process.MainWindowTitle}\"").AppendLine();
        }
        catch (Exception ex)
        {
            sb.Append("  process info unavailable: ").Append(ex.GetType().Name).AppendLine();
        }

        sb.Append($"  threadPool threads={ThreadPool.ThreadCount} pendingWork={ThreadPool.PendingWorkItemCount} ")
          .Append($"completedWork={ThreadPool.CompletedWorkItemCount} activeTimers={Timer.ActiveCount} ")
          .Append($"lockContentions={Monitor.LockContentionCount}").AppendLine();

        try
        {
            using var process = Process.GetCurrentProcess();
            int listed = 0;
            foreach (ProcessThread thread in process.Threads)
            {
                if (listed++ == MaxThreadsListed)
                {
                    sb.AppendLine("  ...");
                    break;
                }

                sb.Append($"  os-thread {thread.Id} state={thread.ThreadState}");
                if (thread.ThreadState == System.Diagnostics.ThreadState.Wait)
                    sb.Append($" waitReason={thread.WaitReason}");
                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            sb.Append("  thread list unavailable: ").Append(ex.GetType().Name).AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
