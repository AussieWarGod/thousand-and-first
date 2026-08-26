using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Version-first, manually bounded codec. It validates list counts and UTF-8 byte lengths
	/// before allocating. Only the two owning books are engine composites; nested rows cannot be
	/// independently reflection-deserialized into oversized lists.
	/// </summary>
	public static partial class KingdomLifecycleWireCodec
	{
		public const int LifecycleMagic = 0x544C4332; // TLC2
		public const int CarryMagic = 0x54434332; // TCC2
		public const int GrowthMagic = 0x54475231; // TGR1
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		private sealed class GrowthCappedWriteStream : Stream
		{
			private readonly MemoryStream Inner = new MemoryStream();
			private readonly long Maximum;

			public GrowthCappedWriteStream(long maximum)
			{
				if (maximum < 0L) throw new ArgumentOutOfRangeException(nameof(maximum));
				Maximum = maximum;
			}

			public byte[] ToArray() { return Inner.ToArray(); }

			private void RequireCapacity(long count)
			{
				if (count < 0L || Position > Maximum - count)
					throw new InvalidDataException(
						"growth aggregate cap reached before write allocation");
			}

			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => true;
			public override long Length => Inner.Length;
			public override long Position
			{
				get { return Inner.Position; }
				set
				{
					if (value < 0L || value > Maximum)
						throw new InvalidDataException("growth stream position exceeds cap");
					Inner.Position = value;
				}
			}

			public override void Flush() { Inner.Flush(); }
			public override int Read(byte[] buffer, int offset, int count)
			{
				return Inner.Read(buffer, offset, count);
			}
			public override long Seek(long offset, SeekOrigin origin)
			{
				long target;
				switch (origin)
				{
				case SeekOrigin.Begin: target = offset; break;
				case SeekOrigin.Current: target = Position + offset; break;
				case SeekOrigin.End: target = Length + offset; break;
				default: throw new ArgumentOutOfRangeException(nameof(origin));
				}
				Position = target;
				return target;
			}
			public override void SetLength(long value)
			{
				if (value < 0L || value > Maximum)
					throw new InvalidDataException("growth stream length exceeds cap");
				Inner.SetLength(value);
			}
			public override void Write(byte[] buffer, int offset, int count)
			{
				RequireCapacity(count); Inner.Write(buffer, offset, count);
			}
			public override void WriteByte(byte value)
			{
				RequireCapacity(1L); Inner.WriteByte(value);
			}
			protected override void Dispose(bool disposing)
			{
				if (disposing) Inner.Dispose();
				base.Dispose(disposing);
			}
		}
	}
}
