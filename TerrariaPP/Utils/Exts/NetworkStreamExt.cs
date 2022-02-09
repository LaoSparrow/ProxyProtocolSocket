using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaPP.Utils.Exts
{
    public static class NetworkStreamExt
    {
        public static async Task WaitUntilDataAvailableAsync(this NetworkStream stream, int frequency = 25, int timeout = -1) =>
            await TaskExt.WaitUntilAsync(() => stream.DataAvailable, frequency, timeout);
    }
}
