using Microsoft.AspNetCore.Hosting;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using TCPServer;
using TCPServer.Client;
using Xunit;

namespace TCPServer.Tests
{
	public class UnitTest1
	{
		IPEndPoint serverBind = new System.Net.IPEndPoint(IPAddress.Parse("0.0.0.0"), 29472);
		IPEndPoint serverIp = new System.Net.IPEndPoint(IPAddress.Parse("127.0.0.1"), 29472);

		[Fact]
		public void CanSetupServer()
		{
			IWebHost host = CreateHost(true);
			using(var client = new HttpClient(new TCPHttpMessageHandler()))
			{
				var nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico").Result.Content.ReadAsStringAsync().Result;
				Assert.Equal("\"nico\"", nico);
				nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico?test=toto").Result.Content.ReadAsStringAsync().Result;
				Assert.Equal("\"nicototo\"", nico);


				var error = Assert.Throws<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:29472/v1/badrequest/nico").Result.EnsureSuccessStatusCode());
				Assert.Contains("400", error.Message);

				nico = client.PostAsync("http://127.0.0.1:29472/v1/hellojson/", new StringContent("{ \"Name\" : \"Nicoo\" }", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result;
				Assert.Equal("\"Nicoo\"", nico);
			}
		}

		private IWebHost CreateHost(bool includeHeader)
		{
			var host = new WebHostBuilder()
				.UseStartup<Startup>()
				.UseTCPServer(new ServerOptions(serverBind) { IncludeHeaders = includeHeader })
				.Build();
			host.Start();
			return host;
		}

		[Fact]
		public void CanSetupServerNoHeader()
		{
			IWebHost host = CreateHost(false);
			var client = new HttpClient(new TCPHttpMessageHandler() { IncludeHeaders = false });
			var nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico").Result.Content.ReadAsByteArrayAsync().Result;
			Assert.Equal(6, nico.Length);
			nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico?test=toto").Result.Content.ReadAsByteArrayAsync().Result;
			Assert.Equal(10, nico.Length);


			var error = Assert.Throws<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:29472/v1/badrequest/nico").Result.EnsureSuccessStatusCode());
			Assert.Contains("400", error.Message);
		}
	}
}
