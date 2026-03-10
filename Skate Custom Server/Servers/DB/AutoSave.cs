using System.Threading;

namespace Servers
{
    public static class AutoSave
    {
        public static async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(5000, token);
                PlayerDatabase.Save();
            }
        }
    }
}
