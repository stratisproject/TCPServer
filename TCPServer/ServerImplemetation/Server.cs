using Microsoft.AspNetCore.Hosting.Server;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http.Features;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Text;
using System.Buffers;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Hosting.Internal;
using TCPServer.ServerImplemetation;
using Microsoft.Extensions.Logging;

namespace TCPServer
{
	class Server : IServer
	{
		public Server(IServiceProvider serviceProvider, ServerOptions options)
		{
			if(serviceProvider == null)
				throw new ArgumentNullException(nameof(serviceProvider));
			if(options == null)
				throw new ArgumentNullException(nameof(options));
			_Options = options;

			var serverAddressesFeature = new ServerAddressesFeature();
			serverAddressesFeature.Addresses.Add(new UriBuilder("http", options.EndPoint.Address.ToString(), options.EndPoint.Port).Uri.AbsoluteUri);

			Features.Set<IHttpRequestFeature>(new HttpRequestFeature());
			Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
			Features.Set<IServerAddressesFeature>(serverAddressesFeature);
			Features.Set<IServiceProvidersFeature>(new ServiceProvidersFeature() { RequestServices = serviceProvider });

			_Logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TCPServer");
		}

		ILogger _Logger;

		private readonly ServerOptions _Options;
		public ServerOptions Options
		{
			get
			{
				return _Options;
			}
		}

		public IFeatureCollection Features { get; } = new FeatureCollection();

		CancellationTokenSource _Stopped = new CancellationTokenSource();

		public Task StopAsync(CancellationToken cancellationToken)
		{
			if(!_Stopped.IsCancellationRequested)
			{
				_Stopped.Cancel();
				_AcceptLoopStopped.Wait(cancellationToken);
				if(_ListeningSocket != null)
					_ListeningSocket.AsSafeDisposable().Dispose();
				_ListeningEndPoint = null;
			}
			return Task.CompletedTask;
		}

		public void Dispose()
		{
			StopAsync(default(CancellationToken)).GetAwaiter().GetResult();
		}


		IPEndPoint _ListeningEndPoint;
		public IPEndPoint ListeningEndPoint
		{
			get
			{
				return _ListeningEndPoint;
			}
		}
		Socket _ListeningSocket;

		object l = new object();

		public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
		{
			Socket socket = null;
			lock(l)
			{
				if(_ListeningSocket != null)
					throw new InvalidOperationException("The server is already started");
				socket = Options.CreateSocket();
				_ListeningSocket = socket;
				_ListeningEndPoint = (IPEndPoint)_ListeningSocket.LocalEndPoint;

			}
			var unused = StartAsync(socket, application);
			return Task.CompletedTask;
		}


		private async Task StartAsync<TContext>(Socket socket, IHttpApplication<TContext> application)
		{
			try
			{
				while(true)
				{
					var client = await socket.AcceptAsync(_Stopped.Token).ConfigureAwait(false);
					var connectedSocket = new ConnectedSocket(client);
					_Clients.TryAdd(connectedSocket, connectedSocket);
					if(Options.MaxConnections < _Clients.Count)
						await EvictAsync().ConfigureAwait(false);
					var unused = ListenClient(connectedSocket, application);
					_Stopped.Token.ThrowIfCancellationRequested();
				}
			}
			catch(OperationCanceledException)
			{
				if(!_Stopped.IsCancellationRequested)
					throw;
			}
			finally
			{
				_AcceptLoopStopped.Set();
			}
		}

		private Task EvictAsync()
		{
			var evicted = _Clients.Keys.OrderBy(o => o.LastReceivedMessage).FirstOrDefault();
			if(evicted != null)
				DisconnectClient(evicted);
			return Task.CompletedTask;
		}

		private async Task ListenClient<TContext>(ConnectedSocket connectedSocket, IHttpApplication<TContext> application)
		{
			bool exceptionHandled = false;
			CancellationTokenSource idleTimeout = null;
			CancellationTokenSource sendTimeout = null;
			try
			{
				var client = connectedSocket.Socket;
				var networkStream = new NetworkStream(client, false);
				while(true)
				{
					using(var disposables = new CompositeDisposable())
					{
						var stream = new TCPStream(networkStream)
						{
							ArrayPool = Options.ArrayPool,
							Cancellation = _Stopped.Token,
							MaxArrayLength = Options.MaxBytesArrayLength,
							MaxMessageSize = Options.MaxMessageSize
						};
						disposables.Children.Add(stream);
						idleTimeout = new CancellationTokenSource();
						idleTimeout.CancelAfter(Options.IdleTimeout);
						disposables.Children.Add(idleTimeout);
						var linked = CancellationTokenSource.CreateLinkedTokenSource(stream.Cancellation, idleTimeout.Token);
						disposables.Children.Add(linked);
						stream.Cancellation = linked.Token;
						TCPRequest request = null;
						try
						{
							request = await TCPRequest.Parse(stream, Options.IncludeHeaders).ConfigureAwait(false);
						}
						catch(Exception ex)
						{

							_Logger.LogWarning(new EventId(), ex, $"Error while parsing the request of {EndpointString(connectedSocket)}");
							exceptionHandled = true;
							throw;
						}

						disposables.Children.Add(connectedSocket.MarkProcessingRequest());

						var context = (HostingApplication.Context)(object)application.CreateContext(Features);

						context.HttpContext = new TCPContext(request, Features, new TCPConnectionInfo(client, _ListeningEndPoint));
						try
						{
							await application.ProcessRequestAsync((TContext)(object)context);
						}
						catch(Exception ex)
						{
							_Logger.LogError(new EventId(), ex, "Error during request processing");
							exceptionHandled = true;
							throw;
						}

						sendTimeout = new CancellationTokenSource();
						sendTimeout.CancelAfter(Options.SendTimeout);
						disposables.Children.Add(sendTimeout);
						linked = CancellationTokenSource.CreateLinkedTokenSource(stream.Cancellation, sendTimeout.Token);
						disposables.Children.Add(linked);
						stream.Cancellation = linked.Token;
						var response = (TCPResponse)context.HttpContext.Response;
						try
						{
							response.OnStarting();
							await stream.WriteVarIntAsync((ulong)response.StatusCode).ConfigureAwait(false);

							if(Options.IncludeHeaders)
							{
								await stream.WriteVarIntAsync((ulong)response.Headers.Count).ConfigureAwait(false);
								foreach(var header in response.Headers)
								{
									await stream.WriteStringAsync(header.Key).ConfigureAwait(false);
									await stream.WriteStringAsync(header.Value).ConfigureAwait(false);
								}
							}

							await stream.WriteVarIntAsync((ulong)response.Body.Length);
							response.Body.Position = 0;
							await response.Body.CopyToAsync(networkStream, 81920, stream.Cancellation).ConfigureAwait(false);
							await networkStream.FlushAsync(stream.Cancellation).ConfigureAwait(false);
						}
						finally
						{
							response.OnCompleted();
							connectedSocket.LastReceivedMessage = DateTimeOffset.UtcNow;
						}
					}
				}

			}
			catch(OperationCanceledException)
			{
				if(connectedSocket.Socket.Connected)
				{
					if(_Stopped.Token.IsCancellationRequested)
					{
						_Logger.LogInformation($"Connection to {EndpointString(connectedSocket)} stopped");
					}
					else if(idleTimeout != null && idleTimeout.IsCancellationRequested)
					{
						_Logger.LogWarning($"Connection idle detected, kicking {EndpointString(connectedSocket)}");
					}
					else if(sendTimeout != null && sendTimeout.IsCancellationRequested)
					{
						_Logger.LogWarning($"Send timeout detected, kicking {EndpointString(connectedSocket)}");
					}
				}
			}
			catch(Exception ex)
			{
				if(connectedSocket.Socket.Connected && !exceptionHandled)
					_Logger.LogCritical(new EventId(), ex, "TCPServer internal error");
			}
			finally
			{
				if(!connectedSocket.Socket.Connected)
				{
					_Logger.LogInformation($"{EndpointString(connectedSocket)} dropped connection");
				}
				DisconnectClient(connectedSocket);
			}
		}

		private string EndpointString(ConnectedSocket connectedSocket)
		{
			var ip = (IPEndPoint)connectedSocket.Socket.RemoteEndPoint;
			return $"{ip.Address}:{ip.Port}";
		}

		private void DisconnectClient(ConnectedSocket client)
		{
			client.Socket.AsSafeDisposable().Dispose();
			ConnectedSocket unused;
			_Clients.TryRemove(client, out unused);
		}

		ManualResetEventSlim _AcceptLoopStopped = new ManualResetEventSlim(false);
		ConcurrentDictionary<ConnectedSocket, ConnectedSocket> _Clients = new ConcurrentDictionary<ConnectedSocket, ConnectedSocket>();

		public class ConnectedSocket
		{
			class ProcessingRequest : IDisposable
			{
				private ConnectedSocket connectedSocket;

				public ProcessingRequest(ConnectedSocket connectedSocket)
				{
					this.connectedSocket = connectedSocket;
					if(connectedSocket.IsProcessing)
						throw new InvalidOperationException("Already processing request");
					connectedSocket.IsProcessing = true;
				}

				public void Dispose()
				{
					connectedSocket.IsProcessing = false;
				}
			}
			public ConnectedSocket(Socket client)
			{
				Socket = client;
				ConnectedAt = DateTimeOffset.UtcNow;
				LastReceivedMessage = DateTimeOffset.UtcNow;
			}
			public Socket Socket
			{
				get; set;
			}
			public DateTimeOffset ConnectedAt
			{
				get; set;
			}

			public bool IsProcessing
			{
				get; set;
			}

			public DateTimeOffset LastReceivedMessage
			{
				get; set;
			}

			internal IDisposable MarkProcessingRequest()
			{
				return new ProcessingRequest(this);
			}
		}

	}
}
