using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TCPServer.ServerImplemetation
{
	class TCPStream : IDisposable
	{
		public TCPStream(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException(nameof(stream));
			_Inner = stream;
		}

		public int MaxArrayLength
		{
			get; set;
		} = 1024 * 1024;

		public int MaxMessageSize
		{
			get; set;
		} = 1024 * 1024;

		private readonly Stream _Inner;
		public Stream Inner
		{
			get
			{
				return _Inner;
			}
		}

		public CancellationToken Cancellation
		{
			get; set;
		}

		public async Task<ulong> ReadVarIntAsync()
		{
			AddReaden(1);
			var b1 = await _Inner.ReadByteAsync(Cancellation).ConfigureAwait(false);
			if(b1 < 0xFD)
				return (uint)b1;
			if(b1 == 0xFD)
				return (uint)await ReadUShortAsync().ConfigureAwait(false);
			if(b1 == 0xFE)
				return (uint)await ReadUIntAsync().ConfigureAwait(false);
			AddReaden(8);
			await _Inner.ReadAsync(_SmallBuffer, 0, 8, Cancellation).ConfigureAwait(false);
			return (uint)(
				(_SmallBuffer[0]) + (_SmallBuffer[1] << 8) +
				(_SmallBuffer[2] << 16) + (_SmallBuffer[3] << 24) +
				(_SmallBuffer[4] << 32) + (_SmallBuffer[5] << 40) +
				(_SmallBuffer[6] << 48) + (_SmallBuffer[7] << 56)
				);
		}

		public async Task WriteVarIntAsync(ulong num)
		{
			if(num < 0xFD)
			{
				_SmallBuffer[0] = (byte)num;
				await _Inner.WriteAsync(_SmallBuffer, 0, 1, Cancellation);
			}
			else if(num <= ushort.MaxValue)
			{
				_SmallBuffer[0] = 0xFD;
				_SmallBuffer[1] = (byte)num;
				_SmallBuffer[2] = (byte)(num >> 8);
				await _Inner.WriteAsync(_SmallBuffer, 0, 3, Cancellation).ConfigureAwait(false);
			}
			else if(num <= uint.MaxValue)
			{
				_SmallBuffer[0] = 0xFE;
				_SmallBuffer[1] = (byte)num;
				_SmallBuffer[2] = (byte)(num >> 8);
				_SmallBuffer[3] = (byte)(num >> 16);
				_SmallBuffer[4] = (byte)(num >> 24);
				await _Inner.WriteAsync(_SmallBuffer, 0, 5, Cancellation).ConfigureAwait(false);
			}
			else
			{
				_SmallBuffer[0] = 0xFF;
				_SmallBuffer[1] = (byte)num;
				_SmallBuffer[2] = (byte)(num >> 8);
				_SmallBuffer[3] = (byte)(num >> 16);
				_SmallBuffer[4] = (byte)(num >> 24);
				_SmallBuffer[5] = (byte)(num >> 32);
				_SmallBuffer[6] = (byte)(num >> 40);
				_SmallBuffer[7] = (byte)(num >> 48);
				_SmallBuffer[8] = (byte)(num >> 56);
				await _Inner.WriteAsync(_SmallBuffer, 0, 9, Cancellation).ConfigureAwait(false);
			}
		}

		byte[] _SmallBuffer = new byte[8];
		public async Task<ushort> ReadUShortAsync()
		{
			AddReaden(2);
			await _Inner.ReadAsync(_SmallBuffer, 0, 2, Cancellation).ConfigureAwait(false);
			return (ushort)(_SmallBuffer[0] + (_SmallBuffer[1] << 8));
		}

		public async Task<uint> ReadUIntAsync()
		{
			AddReaden(4);
			await _Inner.ReadAsync(_SmallBuffer, 0, 4, Cancellation).ConfigureAwait(false);
			return (uint)(_SmallBuffer[0] + (_SmallBuffer[1] << 8) + (_SmallBuffer[2] << 16) + (_SmallBuffer[3] << 24));
		}

		public ArrayPool<byte> ArrayPool
		{
			get; set;
		} = ArrayPool<byte>.Shared;

		Encoding encoding = Encoding.ASCII;
		public async Task WriteStringAsync(string str)
		{
			var bytes = encoding.GetBytes(str);
			await WriteBytesAsync(bytes).ConfigureAwait(false);
		}

		public async Task WriteBytesAsync(byte[] bytes)
		{
			if(bytes.Length > MaxArrayLength)
				throw new ArgumentOutOfRangeException("MaxArrayLength");
			await WriteVarIntAsync((ulong)bytes.Length).ConfigureAwait(false);
			await _Inner.WriteAsync(bytes, 0, bytes.Length, Cancellation).ConfigureAwait(false);
		}

		public async Task<string> ReadStringAync()
		{
			var bytes = await ReadBytesAync(ReadType.ManualPool).ConfigureAwait(false);
			try
			{
				return encoding.GetString(bytes.Array, 0, bytes.Count);
			}
			finally
			{
				ArrayPool.Return(bytes.Array);
			}
		}

		public enum ReadType
		{
			ManagedPool,
			ManualPool,
			NewArray
		}
		public async Task<ArraySegment<byte>> ReadBytesAync(ReadType type)
		{
			var length = await ReadVarIntAsync().ConfigureAwait(false);
			if(length == 0)
				return new ArraySegment<byte>(new byte[0], 0, 0);
			if(length > (ulong)MaxArrayLength)
				throw new ArgumentOutOfRangeException();

			AddReaden(length);

			var array = type == ReadType.NewArray ? new byte[(int)length] : ArrayPool<byte>.Shared.Rent((int)length);
			if(type == ReadType.ManagedPool)
				_RentedArrays.Add(array);

			int readen = 0;
			while(readen != (int)length)
				readen += await _Inner.ReadAsync(array, readen, (int)length - readen, Cancellation).ConfigureAwait(false);

			return new ArraySegment<byte>(array, 0, (int)length);
		}

		ulong totalReaden = 0;
		private void AddReaden(ulong length)
		{
			totalReaden += length;
			if(totalReaden > (ulong)MaxMessageSize)
				throw new ArgumentOutOfRangeException("MaxMessageSize");
		}

		public void ReturnRentedArrays()
		{
			foreach(var array in _RentedArrays)
			{
				ArrayPool.Return(array);
			}
			_RentedArrays.Clear();
		}

		public void Dispose()
		{
			ReturnRentedArrays();
		}

		List<byte[]> _RentedArrays = new List<byte[]>();
	}
}
