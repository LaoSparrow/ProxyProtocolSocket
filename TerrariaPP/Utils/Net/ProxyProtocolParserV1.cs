using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaPP.Utils.Net
{
    public class ProxyProtocolParserV1 : IProxyProtocolParser
    {
        #region Constants
        private const string DELIMITER = "\r\n";
        private const char SEPARATOR = ' ';
        #endregion

        #region Members
        private NetworkStream _stream;
        private IPEndPoint _remoteEndpoint;
        private byte[] _buffer;
        private int _bufferPosition;

        private bool _isParsed;
        private AddressFamily _addressFamily;
        private ProxyProtocolCommand _protocolCommand;
        private IPEndPoint _sourceEndpoint;
        private IPEndPoint _destEndpoint;
        #endregion

        public ProxyProtocolParserV1(NetworkStream stream, IPEndPoint remoteEndpoint, byte[] buffer, ref int bufferPosition)
        {
            #region Args checking
            if (stream == null)                 throw new ArgumentNullException("argument 'stream' cannot be null");
            if (stream.CanRead != true)         throw new     ArgumentException("argument 'stream' is unreadable");
            if (remoteEndpoint == null)         throw new ArgumentNullException("argument 'remoteEndpoint' cannot be null");
            if (buffer == null)                 throw new ArgumentNullException("argument 'buffer' cannot be null");
            if (bufferPosition > buffer.Length) throw new     ArgumentException("argument 'bufferPosition' is larger than 'buffer.Length'");
            #endregion

            #region Filling members
            _stream = stream;
            _remoteEndpoint = remoteEndpoint;
            _buffer = buffer;
            _bufferPosition = bufferPosition;
            #endregion
        }

        #region Public methods
        public async Task Parse()
        {
            if (_isParsed)
                return;
            _isParsed = true;
            Logger.Log("Parsing header");

            #region Getting full header and do first check
            await GetFullHeader();
            if (_bufferPosition < 2 || _buffer[_bufferPosition - 2] != '\r')
                throw new Exception("Header must end with CRLF");

            string[] tokens = Encoding.ASCII.GetString(_buffer.Take(_bufferPosition - 2).ToArray()).Split(SEPARATOR);
            if (tokens.Length < 2)
                throw new Exception("Unable to read AddressFamily and protocol");
            #endregion

            #region Parse address family
            AddressFamily addressFamily;
            switch (tokens[1])
            {
                case "TCP4":
                    addressFamily = AddressFamily.InterNetwork;
                    break;

                case "TCP6":
                    addressFamily = AddressFamily.InterNetworkV6;
                    break;

                case "UNKNOWN":
                    addressFamily = AddressFamily.Unspecified;
                    break;

                default:
                    throw new Exception("Invalid address family");
            }
            #endregion

            #region Do second check
            if (addressFamily == AddressFamily.Unspecified)
            {
                _protocolCommand = ProxyProtocolCommand.LOCAL;
                _sourceEndpoint = _remoteEndpoint;
                _isParsed = true;
                return;
            }
            else if (tokens.Length < 6)
                throw new Exception("Unable to read ipaddresses and ports");
            #endregion

            #region Parse source and dest end point
            IPEndPoint sourceEP;
            IPEndPoint destEP;
            try
            {
                // TODO: IP format validation
                IPAddress sourceAddr = IPAddress.Parse(tokens[2]);
                IPAddress destAddr = IPAddress.Parse(tokens[3]);
                int sourcePort = Convert.ToInt32(tokens[4]);
                int destPort = Convert.ToInt32(tokens[5]);
                sourceEP = new IPEndPoint(sourceAddr, sourcePort);
                destEP = new IPEndPoint(destAddr, destPort);
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to parse ip addresses and ports", ex);
            }
            #endregion

            _addressFamily      = addressFamily;
            _protocolCommand    = ProxyProtocolCommand.PROXY;
            _sourceEndpoint     = sourceEP;
            _destEndpoint       = destEP;
        }

        public async Task<IPEndPoint> GetSourceEndpoint()
        {
            await Parse();
            return _sourceEndpoint;
        }

        public async Task<IPEndPoint> GetDestEndpoint()
        {
            await Parse();
            return _destEndpoint;
        }

        public async Task<AddressFamily> GetAddressFamily()
        {
            await Parse();
            return _addressFamily;
        }

        public async Task<ProxyProtocolCommand> GetCommand()
        {
            await Parse();
            return _protocolCommand;
        }
        #endregion

        #region Private methods
        private async Task GetFullHeader()
        {
            Logger.Log($"Getting full header");
            for (int i = 1; ; i++)
            {
                if (await GetOneByteOfPosition(i) == '\n')
                    break;
                if (i >= _buffer.Length)
                    throw new Exception("Reaching the end of buffer without reaching the delimiter of version 1");
            }
        }

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

        private async Task<byte> GetOneByteOfPosition(int position)
        {
            await GetBytesToPosition(position);
            return _buffer[position - 1];
        }

        private byte GetOneByteFromStream()
        {
            if ((_bufferPosition + 1) > _buffer.Length)
                throw new InternalBufferOverflowException();

            int readState = _stream.ReadByte();
            if (readState < 0)
                throw new EndOfStreamException();

            _buffer[_bufferPosition] = (byte)readState;
            _bufferPosition++;
            return (byte)readState;
        }
        #endregion
    }
}
