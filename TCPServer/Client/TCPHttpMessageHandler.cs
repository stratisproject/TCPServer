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

namespace TCPServer.Client
{
	public class TCPHttpMessageHandler : HttpMessageHandler
	{
		public bool IncludeHeaders
		{
			get; set;
		} = true;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			AssertNotDisposed();
			var socket = await EnsureConnectedAsync(request.RequestUri, cancellationToken).ConfigureAwait(false);
			var networkStream = new NetworkStream(socket, false);
			using(TCPStream tcpStream = new TCPStream(networkStream))
			{
				await tcpStream.WriteStringAsync(request.Method.Method).ConfigureAwait(false);

				await tcpStream.WriteStringAsync(request.RequestUri.AbsoluteUri).ConfigureAwait(false);

				if(IncludeHeaders)
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
					await request.Content.CopyToAsync(networkStream).ConfigureAwait(false);
				}
				await networkStream.FlushAsync().ConfigureAwait(false);

				return await GetResponseAsync(tcpStream).ConfigureAwait(false);
			}
		}

		ConcurrentDictionary<string, Func<Task<Socket>>> _Sockets = new ConcurrentDictionary<string, Func<Task<Socket>>>();
		ConcurrentBag<Socket> _CreatedSockets = new ConcurrentBag<Socket>();
		private async Task<Socket> EnsureConnectedAsync(Uri request, CancellationToken cancellationToken)
		{
			var key = $"{request.Host}:{request.Port}";
			var socketFactory = _Sockets.GetOrAdd(key, _ =>
			{
				var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				_CreatedSockets.Add(socket);
				return async () =>
				{
					if(socket.Connected)
						return socket;
					await socket.ConnectAsync(new IPEndPoint(IPAddress.Parse(request.Host), request.Port), cancellationToken).ConfigureAwait(false);
					return socket;
				};
			});
			return await socketFactory().ConfigureAwait(false);
		}

		private async Task<HttpResponseMessage> GetResponseAsync(TCPStream tcpStream)
		{
			var response = new HttpResponseMessage()
			{
				StatusCode = (HttpStatusCode)await tcpStream.ReadVarIntAsync().ConfigureAwait(false)
			};

			List<Tuple<string, string>> headers = new List<Tuple<string, string>>();
			if(IncludeHeaders)
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
			await tcpStream.Inner.ReadAsync(bodyArray, 0, bodyArray.Length, tcpStream.Cancellation).ConfigureAwait(false);
			response.Content = new StreamContent(new MemoryStream(bodyArray));
			response.Content.Headers.ContentLength = (int)length;

			if(IncludeHeaders)
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
			foreach(var socket in _CreatedSockets)
			{
				socket.AsSafeDisposable().Dispose();
			}
			_Disposed = true;
			base.Dispose(disposing);
		}

		private void AssertNotDisposed()
		{
			if(_Disposed)
				throw new ObjectDisposedException("TCPHttpMessageHandler");
		}
	}
}
