using System.Net;
using System.Net.Sockets;

namespace ProxyProtocolSocket.Utils.Net
{
    public class ProxyProtocolParserV2 : IProxyProtocolParser
    {
        #region Constants
        private const int SIGNATURE_LENGNTH = 16;
        private const int IPV4_ADDR_LENGTH = 4;
        private const int IPV6_ADDR_LENGTH = 16;
        #endregion

        #region Members
        private NetworkStream _stream;
        private IPEndPoint _remoteEndpoint;
        private byte[] _buffer;
        private int _bufferPosition;

        private bool _isParsed;
        private AddressFamily _addressFamily = AddressFamily.Unknown;
        private ProxyProtocolCommand _protocolCommand = ProxyProtocolCommand.Unknown;
        private IPEndPoint? _sourceEndpoint;
        private IPEndPoint? _destEndpoint;
        #endregion

        public ProxyProtocolParserV2(NetworkStream stream, IPEndPoint remoteEndpoint, byte[] buffer, ref int bufferPosition)
        {
            #region Args checking
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (stream.CanRead != true) throw new ArgumentException("argument 'stream' is unreadable");
            if (remoteEndpoint == null) throw new ArgumentNullException(nameof(remoteEndpoint));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferPosition > buffer.Length) throw new ArgumentException("argument 'bufferPosition' is larger than 'buffer.Length'");
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

            // Getting signature
            await GetBytesToPosition(SIGNATURE_LENGNTH);

            #region Parsing command
            ProxyProtocolCommand command;
            switch (_buffer[12] & 0x0F)
            {
                case 0x00:
                    command = ProxyProtocolCommand.Local;
                    break;

                case 0x01:
                    command = ProxyProtocolCommand.Proxy;
                    break;

                default:
                    throw new Exception("Invalid command");
            }
            #endregion

            #region Parsing address family and getting min address length
            AddressFamily family;
            int minAddressLength;
            switch (_buffer[13] & 0xF0)
            {
                case 0x00:
                    family = AddressFamily.Unspecified;
                    minAddressLength = 0;
                    break;

                case 0x10:
                    family = AddressFamily.InterNetwork;
                    minAddressLength = 12;
                    break;

                case 0x20:
                    family = AddressFamily.InterNetworkV6;
                    minAddressLength = 36;
                    break;

                case 0x30:
                    family = AddressFamily.Unix;
                    minAddressLength = 216;
                    break;

                default:
                    throw new Exception("Invalid address family");
            }
            // TODO: Implement address family UNIX
            if (family == AddressFamily.Unix)
                throw new NotImplementedException("Address family UNIX haven't implemented yet");
            #endregion

            #region Parsing transport protocol
            // TODO: Parsing transport protocol
            #endregion

            #region Parsing and checking address length
            int addressLength = GetAddressLength(_buffer);
            Logger.Log($"Address length is {addressLength}");
            if (addressLength < minAddressLength)
                throw new Exception("Address length is too small, is that you set the endian incorrectly?");
            if (SIGNATURE_LENGNTH + addressLength > _buffer.Length)
                throw new Exception("Address length is too large, is that you set the endian incorrectly?");
            #endregion

            #region Getting address data and check if need to parse address data
            await GetBytesToPosition(SIGNATURE_LENGNTH + addressLength);
            if (command != ProxyProtocolCommand.Proxy || family == AddressFamily.Unspecified)
            {
                _protocolCommand = command;
                _addressFamily = family;
                return;
            }
            #endregion

            #region Parsing address data
            IPEndPoint sourceEP;
            IPEndPoint destEP;
            try
            {
                switch (family)
                {
                    case AddressFamily.InterNetwork:
                        sourceEP = new IPEndPoint(GetSourceAddressIPv4(_buffer), GetSourcePortIPv4(_buffer));
                        destEP = new IPEndPoint(GetDestinationAddressIPv4(_buffer), GetDestinationPortIPv4(_buffer));
                    break;

                    case AddressFamily.InterNetworkV6:
                        sourceEP = new IPEndPoint(GetSourceAddressIPv6(_buffer), GetSourcePortIPv6(_buffer));
                        destEP = new IPEndPoint(GetDestinationAddressIPv6(_buffer), GetDestinationPortIPv6(_buffer));
                        break;

                    case AddressFamily.Unix:
                        throw new NotImplementedException("Address family UNIX haven't implemented yet");

                    default:
                        throw new Exception("Unhandled address family while parsing address data");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to parse ip addresses and ports", ex);
            }
            #endregion

            _protocolCommand = command;
            _addressFamily = family;
            _sourceEndpoint = sourceEP;
            _destEndpoint = destEP;
        }

        public async Task<IPEndPoint?> GetSourceEndpoint()
        {
            await Parse();
            return _sourceEndpoint;
        }

        public async Task<IPEndPoint?> GetDestEndpoint()
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
        private async Task GetBytesToPosition(int position)
        {
            Logger.Log($"Getting bytes to position {position} from {_remoteEndpoint}");
            if (position <= _bufferPosition)
                return;
            await GetBytesFromStream(position - _bufferPosition);
        }

        private async Task GetBytesFromStream(int length)
        {
            Logger.Log($"Getting {length} bytes from {_remoteEndpoint}");
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

        #region Private static methods
        private static int GetAddressLength(byte[] signature) =>
            BytesToUInt16(signature.Skip(SIGNATURE_LENGNTH - 2).Take(2).ToArray());

        #region IPv4
        private static IPAddress GetSourceAddressIPv4(byte[] header) =>
            new IPAddress(header.Skip(SIGNATURE_LENGNTH).Take(IPV4_ADDR_LENGTH).ToArray());

        private static IPAddress GetDestinationAddressIPv4(byte[] header) =>
            new IPAddress(header.Skip(SIGNATURE_LENGNTH + IPV4_ADDR_LENGTH).Take(IPV4_ADDR_LENGTH).ToArray());

        private static int GetSourcePortIPv4(byte[] header) =>
            BytesToUInt16(header.Skip(SIGNATURE_LENGNTH + 2 * IPV4_ADDR_LENGTH).Take(2).ToArray());

        private static int GetDestinationPortIPv4(byte[] header) =>
            BytesToUInt16(header.Skip(SIGNATURE_LENGNTH + 2 * IPV4_ADDR_LENGTH + 2).Take(2).ToArray());
        #endregion

        #region IPv6
        private static IPAddress GetSourceAddressIPv6(byte[] header) =>
            new IPAddress(header.Skip(SIGNATURE_LENGNTH).Take(IPV6_ADDR_LENGTH).ToArray());

        private static IPAddress GetDestinationAddressIPv6(byte[] header) =>
            new IPAddress(header.Skip(SIGNATURE_LENGNTH + IPV6_ADDR_LENGTH).Take(IPV6_ADDR_LENGTH).ToArray());

        private static int GetSourcePortIPv6(byte[] header) =>
            BytesToUInt16(header.Skip(SIGNATURE_LENGNTH + 2 * IPV6_ADDR_LENGTH).Take(2).ToArray());

        private static int GetDestinationPortIPv6(byte[] header) =>
            BytesToUInt16(header.Skip(SIGNATURE_LENGNTH + 2 * IPV6_ADDR_LENGTH + 2).Take(2).ToArray());
        #endregion

        private static int BytesToUInt16(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }
        #endregion
    }
}
