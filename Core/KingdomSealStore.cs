using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		internal const string StagesFolder = "Stages";

		internal const string LegaciesFolder = "Legacies";

		internal const string ReceiptsFolder = "Receipts";

		internal const string ClaimsFolder = "Claims";

		internal const string SealExtension = ".seal";

		internal const string ReceiptExtension = ".receipt";

		internal const int MaxFilesScanned = 256;

		internal const int MaxStageFilesScanned = MaxFilesScanned * 2;

		private readonly string _root;

		private readonly string _rootPrefix;

		private readonly StringComparison _pathComparison;

		private readonly IKingdomSealFileOps _files;

		internal KingdomSealStore(string Root)
			: this(Root, SystemKingdomSealFileOps.Instance)
		{
		}

		internal KingdomSealStore(string Root, IKingdomSealFileOps Files)
		{
			if (string.IsNullOrEmpty(Root))
			{
				throw new ArgumentException("A seal store needs a root folder.");
			}
			if (Files == null)
			{
				throw new ArgumentNullException("Files");
			}
			_root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar);
			if (_root.Length == 0 || string.Equals(_root, Path.GetPathRoot(_root),
				StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException("A seal store root must be a bounded profile subfolder.");
			}
			_rootPrefix = _root + Path.DirectorySeparatorChar;
			_pathComparison = Path.DirectorySeparatorChar == '\\'
				? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_files = Files;
		}

		internal string Root => _root;

	}
}
