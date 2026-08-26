using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Pure admission rules for founder-authored presentation names. Identity and recovery codecs
	/// deliberately do not use this class: they preserve broader legacy evidence, while new input
	/// must satisfy the Foundation contract before any reservation or mutation begins.
	/// </summary>
	public static class KingdomPresentationRules
	{
		public const int MaxNameTextElements = 30;
		/// <summary>Broad UI-entry bound only; acceptance is the text-element limit above.</summary>
		public const int MaxRawCodeUnits = 256;

		/// <summary>
		/// Normalizes to NFC, rejects unsafe controls, trims, and counts Unicode text elements rather
		/// than UTF-16 code units. Accepted values remain plain data; rich-text escaping belongs at
		/// the engine presentation boundary.
		/// </summary>
		public static bool TryNormalizeName(string Raw, out string Name, out string Error)
		{
			Name = null;
			Error = null;
			if (Raw == null)
			{
				Error = "a name needs at least one character";
				return false;
			}
			if (Raw.Length > MaxRawCodeUnits)
			{
				Error = "a name is too large to read safely";
				return false;
			}

			string normalized;
			try
			{
				normalized = Raw.Normalize(NormalizationForm.FormC);
			}
			catch (ArgumentException)
			{
				Error = "a name must contain valid Unicode text";
				return false;
			}

			for (int i = 0; i < normalized.Length; i++)
			{
				char c = normalized[i];
				if (c <= '\u001f' || (c >= '\u007f' && c <= '\u009f') ||
					c == '\u061c' || (c >= '\u200e' && c <= '\u200f') ||
					(c >= '\u202a' && c <= '\u202e') ||
					(c >= '\u2066' && c <= '\u2069'))
				{
					Error = "a name cannot contain control or bidirectional formatting characters";
					return false;
				}
			}

			string trimmed = normalized.Trim();
			if (trimmed.Length == 0)
			{
				Error = "a name needs at least one character";
				return false;
			}

			int[] elements;
			try
			{
				elements = StringInfo.ParseCombiningCharacters(trimmed);
			}
			catch (ArgumentException)
			{
				Error = "a name must contain valid Unicode text";
				return false;
			}
			if (elements.Length > MaxNameTextElements)
			{
				Error = "a name can be at most " + MaxNameTextElements + " characters";
				return false;
			}

			Name = trimmed;
			return true;
		}
	}
}
