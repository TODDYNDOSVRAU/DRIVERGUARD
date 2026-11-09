using System.Diagnostics;

namespace DriverGuard.Infrastructure.Pnp;

/// <summary>
/// Executa o pnputil.exe com leitura assíncrona de stdout e stderr (evita deadlock de buffer)
/// e timeout real: se o processo não terminar no prazo, ele é encerrado.
/// </summary>
internal static class PnpUtilRunner
{
    public sealed record PnpUtilResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);

    public static async Task<PnpUtilResult> RunAsync(string arguments, int timeoutMs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pnputil.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Falha ao iniciar pnputil.exe.");

        // Lê as duas saídas em paralelo enquanto o processo roda
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        bool timedOut = false;
        using (var timeoutCts = new CancellationTokenSource(timeoutMs))
        {
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (Exception)
                {
                    // Processo já encerrado entre a checagem e o Kill
                }
            }
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        int exitCode = timedOut || !process.HasExited ? -1 : process.ExitCode;

        return new PnpUtilResult(exitCode, stdout, stderr, timedOut);
    }
}
