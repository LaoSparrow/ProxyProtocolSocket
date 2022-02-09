using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TerrariaPP.Utils.Exts;

namespace TerrariaPP.Utils.Net
{
    #region Enums
    public enum ProxyProtocolVersion
    {
        V1,
        V2,
        UNKNOWN
    }

    public enum ProxyProtocolCommand
    {
        LOCAL,
        PROXY,
        UNKNOWN
    }
    #endregion

    public class ProxyProtocol
    {
        #region Constants
        private static readonly byte[] V1_SIGNATURE = Encoding.ASCII.GetBytes("PROXY");
        private static readonly byte[] V2_OR_ABOVE_SIGNATURE = { 0x0D, 0x0A, 0x0D, 0x0A, 0x00, 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A };
        private const int V2_OR_ABOVE_ID_LENGTH = 16;
        private const int BUFFER_SIZE = 107;
        #endregion

        #region Members
        private NetworkStream _stream;
        private IPEndPoint _remoteEndpoint;

        private byte[] _buffer                          = new byte[BUFFER_SIZE];
        private int _bufferPosition                     = 0;

        private bool _isParserCached                    = false;
        private IProxyProtocolParser _cachedParser      = null;
        private ProxyProtocolVersion? _protocolVersion  = null;
        #endregion

        public ProxyProtocol(NetworkStream stream, IPEndPoint remoteEndpoint)
        {
            #region Args checking
            if (stream == null)         throw new ArgumentNullException("argument 'stream' cannot be null");
            if (stream.CanRead != true) throw new     ArgumentException("argument 'stream' is unreadable");
            if (remoteEndpoint == null) throw new ArgumentNullException("argument 'remoteEndpoint' cannot be null");
            #endregion

            #region Filling members
            _stream = stream;
            _remoteEndpoint = remoteEndpoint;
            #endregion
        }

        #region Public methods
        public async Task Parse()
        {
            IProxyProtocolParser parser = await GetParser();
            Logger.Log($"Calling parser {parser.GetType().Name}");
            await parser?.Parse();
        }

        public async Task<IPEndPoint> GetSourceEndpoint() =>
            await (await GetParser())?.GetSourceEndpoint();

        public async Task<IPEndPoint> GetDestEndpoint() =>
            await (await GetParser())?.GetDestEndpoint();

        public async Task<AddressFamily> GetAddressFamily() =>
            await (await GetParser())?.GetAddressFamily();

        public async Task<ProxyProtocolCommand> GetCommand() =>
            await (await GetParser())?.GetCommand();

        public async Task<IProxyProtocolParser> GetParser()
        {
            // Read from cache
            if (_isParserCached)
                return _cachedParser;

            Logger.Log("Getting parser");
            // Get parser corresponding to version
            switch (await GetVersion())
            {
                case ProxyProtocolVersion.V1:
                    _cachedParser = new ProxyProtocolParserV1(_stream, _remoteEndpoint, _buffer, ref _bufferPosition);
                    break;

                case ProxyProtocolVersion.V2:
                    _cachedParser = new ProxyProtocolParserV2(_stream, _remoteEndpoint, _buffer, ref _bufferPosition);
                    break;

                default:
                    _cachedParser = null;
                    break;
            }
            _isParserCached = true;
            return _cachedParser;
        }

        public async Task<ProxyProtocolVersion> GetVersion()
        {
            // Read from cache
            if (_protocolVersion != null)
                return (ProxyProtocolVersion)_protocolVersion;

            Logger.Log("Getting version info");

            _protocolVersion = ProxyProtocolVersion.UNKNOWN;
            // Check if is version 1
            await GetBytesToPosition(V1_SIGNATURE.Length);
            if (IsVersion1(_buffer))
                _protocolVersion = ProxyProtocolVersion.V1;
            else
            {
                // Check if is version 2 or above
                await GetBytesToPosition(V2_OR_ABOVE_ID_LENGTH);
                if (IsVersion2OrAbove(_buffer))
                {
                    // Check versions
                    if (IsVersion2(_buffer, true))
                        _protocolVersion = ProxyProtocolVersion.V2;
                }
            }

            return (ProxyProtocolVersion)_protocolVersion;
        }
        #endregion

        #region Private methods
        private async Task GetBytesToPosition(int position)
        {
            if (position <= _bufferPosition)
                return;
            await GetBytesFromStream(position - _bufferPosition);
        }

        private async Task GetBytesFromStream(int length)
        {
            if ((_bufferPosition + length) > _buffer.Length)
                throw new InternalBufferOverflowException();

            while (length > 0)
            {
                if (!_stream.DataAvailable)
                    throw new EndOfStreamException();

                int count = await _stream.ReadAsync(_buffer, _bufferPosition, length);
                length -= count;
                _bufferPosition += count;
            }
        }
        #endregion

        #region Public static methods
        public static bool IsVersion1(byte[] header) => header.Take(V1_SIGNATURE.Length).SequenceEqual(V1_SIGNATURE);
        public static bool IsVersion2OrAbove(byte[] header) => header.Take(V2_OR_ABOVE_SIGNATURE.Length).SequenceEqual(V2_OR_ABOVE_SIGNATURE);
        public static bool IsVersion2(byte[] header, bool checkedSignature = false) =>
            checkedSignature || IsVersion2OrAbove(header) &&
            (header[12] & 0xF0) == 0x20;
        #endregion
    }
}
