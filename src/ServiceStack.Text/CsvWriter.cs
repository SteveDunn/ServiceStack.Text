using System;
using System.Collections.Generic;
using System.IO;
using ServiceStack.Text.Common;
using ServiceStack.Text.Reflection;

namespace ServiceStack.Text
{
	internal class CsvWriter<T>
	{
		public const char DelimiterChar = ',';

		public static List<string> Headers { get; set; }

		internal static List<Func<T, object>> PropertyGetters;

		private static readonly WriteObjectDelegate OptimizedWriter;

		static CsvWriter()
		{
			if (typeof(T) == typeof(string))
			{
				OptimizedWriter = (w, o,y) => WriteRow(w, (IEnumerable<string>)o, false);
				return;
			}

			Reset();
		}

		internal static void Reset()
		{
			Headers = new List<string>();

			PropertyGetters = new List<Func<T, object>>();
			foreach (var propertyInfo in TypeConfig<T>.Properties)
			{
				if (!propertyInfo.CanRead || propertyInfo.GetGetMethod() == null) continue;
				if (!TypeSerializer.CanCreateFromString(propertyInfo.PropertyType)) continue;

				PropertyGetters.Add(propertyInfo.GetValueGetter<T>());
				Headers.Add(propertyInfo.Name);
			}
		}

		internal static void ConfigureCustomHeaders(Dictionary<string, string> customHeadersMap)
		{
			Reset();

			for (var i = Headers.Count - 1; i >= 0; i--)
			{
				var oldHeader = Headers[i];
				string newHeaderValue;
				if (!customHeadersMap.TryGetValue(oldHeader, out newHeaderValue))
				{
					Headers.RemoveAt(i);
					PropertyGetters.RemoveAt(i);
				}
				else
				{
					Headers[i] = newHeaderValue.EncodeJsv();
				}
			}
		}

		private static List<string> GetSingleRow(IEnumerable<T> records, Type recordType)
		{
			var row = new List<string>();
			foreach (var value in records)
			{
				var strValue = recordType == typeof(string)
				   ? value as string
				   : TypeSerializer.SerializeToString(value);

				row.Add(strValue);
			}
			return row;
		}

		public static List<List<string>> GetRows(IEnumerable<T> records)
		{
			var rows = new List<List<string>>();

			if (records == null) return rows;

			if (typeof(T).IsValueType || typeof(T) == typeof(string))
			{
				rows.Add(GetSingleRow(records, typeof(T)));
				return rows;
			}

			foreach (var record in records)
			{
				var row = new List<string>();
				foreach (var propertyGetter in PropertyGetters)
				{
					var value = propertyGetter(record) ?? "";

					var strValue = value.GetType() == typeof(string)
						? (string)value
						: TypeSerializer.SerializeToString(value);

					row.Add(strValue);
				}
				rows.Add(row);
			}

			return rows;
		}

		public static void WriteObject(TextWriter writer, object records, bool includeType)
		{
			Write(writer, (IEnumerable<T>)records);
		}

		public static void WriteObjectRow(TextWriter writer, object record, bool includeType)
		{
			WriteRow(writer, (T)record);
		}

		public static void Write(TextWriter writer, IEnumerable<T> records)
		{
			if (records == null) return; //AOT

			if (OptimizedWriter != null)
			{
				OptimizedWriter(writer, records, false);
				return;
			}

			if (!CsvConfig<T>.OmitHeaders && Headers.Count > 0)
			{
				var ranOnce = false;
				foreach (var header in Headers)
				{
					JsWriter.WriteItemSeperatorIfRanOnce(writer, ref ranOnce);

					writer.Write(header);
				}
				writer.WriteLine();
			}

			if (records == null) return;

			if (typeof(T).IsValueType || typeof(T) == typeof(string))
			{
				var singleRow = GetSingleRow(records, typeof(T));
				WriteRow(writer, singleRow, false);
				return;
			}

			var row = new string[Headers.Count];
			foreach (var record in records)
			{
				for (var i = 0; i < PropertyGetters.Count; i++)
				{
					var propertyGetter = PropertyGetters[i];
					var value = propertyGetter(record) ?? "";

					var strValue = value.GetType() == typeof(string)
					   ? (string)value
					   : TypeSerializer.SerializeToString(value);

					row[i] = strValue;
				}
				WriteRow(writer, row, false);
			}
		}

		public static void WriteRow(TextWriter writer, T row)
		{
			if (row == null) return; //AOT

			Write(writer, new[] { row });
		}

		public static void WriteRow(TextWriter writer, IEnumerable<string> row, bool includeType)
		{
			var ranOnce = false;
			foreach (var field in row)
			{
				JsWriter.WriteItemSeperatorIfRanOnce(writer, ref ranOnce);

				writer.Write(field.ToCsvField());
			}
			writer.WriteLine();
		}

		public static void Write(TextWriter writer, IEnumerable<List<string>> rows)
		{
			if (Headers.Count > 0)
			{
				var ranOnce = false;
				foreach (var header in Headers)
				{
					JsWriter.WriteItemSeperatorIfRanOnce(writer, ref ranOnce);

					writer.Write(header);
				}
				writer.WriteLine();
			}

			foreach (var row in rows)
			{
				WriteRow(writer, row, false);
			}
		}
	}

}