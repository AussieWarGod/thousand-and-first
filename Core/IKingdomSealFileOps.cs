using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal interface IKingdomSealFileOps
	{
		bool Exists(string Path);

		FileAttributes Attributes(string Path);

		long Length(string Path);

		string ReadAllText(string Path);

		void WriteAllTextDurable(string Path, string Text);

		void MoveNew(string Source, string Destination);

		void ReplaceAtomic(string Source, string Destination, string Backup);

		void DeleteIfExists(string Path);
	}
}
