using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaPP.Utils.Net
{
    public interface IProxyProtocolParser
    {
        Task Parse();
        Task<IPEndPoint> GetSourceEndpoint();
        Task<IPEndPoint> GetDestEndpoint();
        Task<AddressFamily> GetAddressFamily();
        Task<ProxyProtocolCommand> GetCommand();
    }
}
