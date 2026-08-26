#if TAF_TESTS
using System.Text;

namespace ConsoleLib.Console
{
	/// <summary>Test double for Qud's ColorUtility formatting escape.</summary>
	public static class ColorUtility
	{
		public static string EscapeFormatting(string value)
		{
			if (value == null) return null;
			StringBuilder escaped = new StringBuilder(value.Length);
			bool insideControl = false;
			int controls = 0;
			for (int i = 0; i < value.Length; i++)
			{
				char current = value[i];
				escaped.Append(current);
				if (insideControl)
				{
					if (current == '|') insideControl = false;
					continue;
				}
				if (i + 1 < value.Length && current == '{' && value[i + 1] == '{')
				{
					escaped.Append("\\{");
					insideControl = true;
					controls++;
					i++;
					continue;
				}
				if (i + 1 < value.Length && controls > 0 && current == '}'
					&& value[i + 1] == '}')
				{
					escaped.Append("\\}");
					controls--;
					i++;
					continue;
				}
				if (current == '&') escaped.Append('&');
				else if (current == '^') escaped.Append('^');
			}
			return escaped.ToString();
		}
	}
}
#endif
