using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed class SystemKingdomSealFileOps : IKingdomSealFileOps
	{
		internal static readonly SystemKingdomSealFileOps Instance = new SystemKingdomSealFileOps();

		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		private SystemKingdomSealFileOps()
		{
		}

		public bool Exists(string Path)
		{
			return File.Exists(Path);
		}

		public FileAttributes Attributes(string Path)
		{
			return File.GetAttributes(Path);
		}

		public long Length(string Path)
		{
			return new FileInfo(Path).Length;
		}

		public string ReadAllText(string Path)
		{
			return File.ReadAllText(Path, Utf8);
		}

		public void WriteAllTextDurable(string Path, string Text)
		{
			byte[] bytes = Utf8.GetBytes(Text ?? "");
			using (FileStream stream = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				stream.Write(bytes, 0, bytes.Length);
				stream.Flush(true);
			}
		}

		public void MoveNew(string Source, string Destination)
		{
			File.Move(Source, Destination);
		}

		public void ReplaceAtomic(string Source, string Destination, string Backup)
		{
			File.Replace(Source, Destination, Backup, true);
		}

		public void DeleteIfExists(string Path)
		{
			if (File.Exists(Path))
			{
				File.Delete(Path);
			}
		}
	}
}
