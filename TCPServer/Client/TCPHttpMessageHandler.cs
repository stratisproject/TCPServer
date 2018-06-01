using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPServer;
using TCPServer.ServerImplemetation;
using System.Buffers;
using System.Runtime.ExceptionServices;

namespace TCPServer.Client
{
	public class ConnectionEndpoint
	{
		public ConnectionEndpoint(string host, int port)
		{
			Host = host;
			Port = port;
		}

		public string Host
		{
			get; set;
		}
		public int Port
		{
			get; set;
		}

		Tuple<string, int> Id()
		{
			return Tuple.Create(Host, Port);
		}


		public override bool Equals(object obj)
		{
			ConnectionEndpoint item = obj as ConnectionEndpoint;
			if(item == null)
				return false;
			return Id().Equals(item.Id());
		}
		public static bool operator ==(ConnectionEndpoint a, ConnectionEndpoint b)
		{
			if(System.Object.ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a.Id().Equals(b.Id());
		}

		public static bool operator !=(ConnectionEndpoint a, ConnectionEndpoint b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return Id().GetHashCode();
		}
	}
	public class TCPHttpMessageHandler : HttpMessageHandler
	{
		public TCPHttpMessageHandler() : this(null)
		{

		}
		public TCPHttpMessageHandler(ClientOptions options)
		{
			options = options ?? new ClientOptions();
			Options = options;
		}
		public ClientOptions Options
		{
			get; set;
		}

		CancellationTokenSource _DisposedToken = new CancellationTokenSource();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			AssertNotDisposed();

			var socket = GetSocket(request.RequestUri);
			try
			{
				return await SendAsyncCore(request, cancellationToken);
			}
			catch(IOException)
			{
				if(!Options.AutoReconnect)
					throw;
			}
			catch(SocketException)
			{
				if(!Options.AutoReconnect)
					throw;
			}
			await DisconnectAsync(GetEndpoint(request.RequestUri)).ConfigureAwait(false);
			return await SendAsyncCore(request, cancellationToken);
		}

		private async Task<HttpResponseMessage> SendAsyncCore(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			using(var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _DisposedToken.Token))
			{
				cancellationToken = linked.Token;
				var socket = await EnsureConnectedAsync(request.RequestUri, cancellationToken).ConfigureAwait(false);
				var networkStream = new NetworkStream(socket, false);
				using(TCPStream tcpStream = new TCPStream(networkStream)
				{
					MaxArrayLength = Options.MaxArrayLength,
					MaxMessageSize = Options.MaxMessageSize,
					Cancellation = cancellationToken,
					ArrayPool = Options.ArrayPool
				})
				{
					await tcpStream.WriteStringAsync(request.Method.Method).ConfigureAwait(false);

					await tcpStream.WriteStringAsync(request.RequestUri.AbsoluteUri).ConfigureAwait(false);

					if(Options.IncludeHeaders)
					{
						var requestHeaders = request.Headers.ToList();
						var contentHeaders = request.Content?.Headers.ToList() ?? new List<KeyValuePair<string, IEnumerable<string>>>();

						var headers = requestHeaders.Concat(contentHeaders).SelectMany(h => h.Value.Select(v => new
						{
							Key = h.Key,
							Value = v
						})).ToList();

						await tcpStream.WriteVarIntAsync((ulong)headers.Count()).ConfigureAwait(false);

						foreach(var header in headers)
						{
							await tcpStream.WriteStringAsync(header.Key).ConfigureAwait(false);
							await tcpStream.WriteStringAsync(header.Value).ConfigureAwait(false);
						}
					}

					await tcpStream.WriteVarIntAsync(request.Content == null ? 0UL : 1).ConfigureAwait(false);
					if(request.Content != null)
					{
						await tcpStream.WriteVarIntAsync((ulong)request.Content.Headers.ContentLength).ConfigureAwait(false);
						await (await request.Content.ReadAsStreamAsync().ConfigureAwait(false)).CopyToAsync(networkStream, 81920, cancellationToken).ConfigureAwait(false);
					}
					await networkStream.FlushAsync(cancellationToken).ConfigureAwait(false);

					return await GetResponseAsync(tcpStream).ConfigureAwait(false);
				}
			}
		}

		ConcurrentDictionary<ConnectionEndpoint, Task<Socket>> _Sockets = new ConcurrentDictionary<ConnectionEndpoint, Task<Socket>>();

		private Task<Socket> GetSocket(Uri uri)
		{
			var endpoint = GetEndpoint(uri);
			Task<Socket> socket = null;
			_Sockets.TryGetValue(endpoint, out socket);
			return socket;
		}

		private async Task DisconnectAsync(ConnectionEndpoint remoteEndPoint)
		{
			Task<Socket> existing = null;
			if(_Sockets.TryRemove(remoteEndPoint, out existing))
			{
				try
				{
					var socket = await existing.ConfigureAwait(false);
					socket.AsSafeDisposable().Dispose();
				}
				catch { }
			}
		}

		private async Task<Socket> EnsureConnectedAsync(Uri request, CancellationToken cancellationToken)
		{
			var endpoint = GetEndpoint(request);
			var socket = _Sockets.GetOrAdd(endpoint, async _ =>
			{
				CancellationTokenSource connectTimeout = new CancellationTokenSource();
				connectTimeout.CancelAfter(Options.ConnectTimeout);
				var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeout.Token);
				Socket s = null;
				try
				{
					s = await CreateSocket(endpoint, linked.Token);
				}
				catch
				{
					Task<Socket> ss;
					_Sockets.TryRemove(_, out ss);
					throw;
				}
				return s;
			});
			return await socket.ConfigureAwait(false);
		}

		protected virtual async Task<Socket> CreateSocket(ConnectionEndpoint endpoint, CancellationToken cancellation)
		{
			List<IPAddress> possibleAddresses = new List<IPAddress>();
			IPAddress address;
			if(!IPAddress.TryParse(endpoint.Host, out address))
			{
				foreach(var add in (await Dns.GetHostEntryAsync(endpoint.Host).ConfigureAwait(false)).AddressList)
				{
					possibleAddresses.Add(add);
				}
				if(possibleAddresses.Count == 0)
					throw new SocketException(11001); //WSAHOST_NOT_FOUND
			}
			else
				possibleAddresses.Add(address);

			Exception lastException = null;
			foreach(var possibleAddress in possibleAddresses)
			{
				try
				{
					Socket s = new Socket(possibleAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        // Windows default to true, but some linux distro does not support this
                        s.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                    }
                    catch(SocketException) { }
					await s.ConnectAsync(new IPEndPoint(possibleAddress, endpoint.Port), cancellation).ConfigureAwait(false);
					return s;
				}
				catch(Exception ex) { lastException = ex; }
			}
			ExceptionDispatchInfo.Capture(lastException).Throw();
			throw new NotImplementedException("impossible to reach");
		}

		protected virtual ConnectionEndpoint GetEndpoint(Uri request)
		{
			return new ConnectionEndpoint(request.DnsSafeHost, request.Port);
		}

		private async Task<HttpResponseMessage> GetResponseAsync(TCPStream tcpStream)
		{
			var response = new HttpResponseMessage()
			{
				StatusCode = (HttpStatusCode)await tcpStream.ReadVarIntAsync().ConfigureAwait(false)
			};

			List<Tuple<string, string>> headers = new List<Tuple<string, string>>();
			if(Options.IncludeHeaders)
			{
				var headersCount = await tcpStream.ReadVarIntAsync().ConfigureAwait(false);
				for(int i = 0; i < (int)headersCount; i++)
				{
					var key = await tcpStream.ReadStringAync().ConfigureAwait(false);
					var value = await tcpStream.ReadStringAync().ConfigureAwait(false);
					headers.Add(Tuple.Create(key, value));
				}
			}

			var headersByKey = headers.GroupBy(h => h.Item1, h => h.Item2);
			var length = await tcpStream.ReadVarIntAsync().ConfigureAwait(false);
			if((int)length > tcpStream.MaxArrayLength)
				throw new IOException("Server's response is too long");
			var bodyArray = new byte[(int)length];
			if(length != 0)
			{
				int readen = 0;
				while(readen != bodyArray.Length)
					readen += await tcpStream.Inner.ReadAsync(bodyArray, readen, bodyArray.Length - readen, tcpStream.Cancellation).ConfigureAwait(false);
			}
			response.Content = new StreamContent(new MemoryStream(bodyArray));
			response.Content.Headers.ContentLength = (int)length;
			if(Options.IncludeHeaders)
			{
				foreach(var g in headersByKey)
				{
					if(response.Content.Headers.Contains(g.Key))
						response.Content.Headers.Remove(g.Key);
					response.Content.Headers.TryAddWithoutValidation(g.Key, g);
				}
			}

			return response;
		}

		bool _Disposed;
		protected override void Dispose(bool disposing)
		{
			AssertNotDisposed();
			_Disposed = true;
			_DisposedToken.Cancel();
			foreach(var socket in _Sockets.Values)
			{
				try
				{
					socket.Result.AsSafeDisposable().Dispose();
				}
				catch { }
			}
			_Sockets.Clear();
			base.Dispose(disposing);
		}

		private void AssertNotDisposed()
		{
			if(_Disposed)
				throw new ObjectDisposedException("TCPHttpMessageHandler");
		}
	}
}
