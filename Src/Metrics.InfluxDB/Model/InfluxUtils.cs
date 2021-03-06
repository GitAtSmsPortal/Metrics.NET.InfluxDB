﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Metrics.MetricData;

namespace Metrics.InfluxDB.Model
{
	/// <summary>
	/// Helper methods for InfluxDB.
	/// </summary>
	public static class InfluxUtils
	{
		/// <summary>
		/// Regex expression that matches the unescaped version of the "space" character for the line protocol.
		/// </summary>
		public static String RegexUnescSpace = @"(?<!\\)[ ]";

		/// <summary>
		/// Regex expression that matches the unescaped version of the "equals" character for the line protocol.
		/// </summary>
		public static String RegexUnescEqual = @"(?<!\\)[=]";

		/// <summary>
		/// Regex expression that matches the unescaped version of the "comma" character for the line protocol.
		/// </summary>
		public static String RegexUnescComma = @"(?<!\\)[,]";


		#region Format Values

		/// <summary>
		/// Converts the string to lowercase and replaces all spaces with the specified character (underscore by default).
		/// </summary>
		/// <param name="value">The string value to lowercase and replace spaces on.</param>
		/// <param name="lowercase">If true, converts the string to lowercase.</param>
		/// <param name="replaceChars">The character(s) to replace all space characters with (underscore by default). If <see cref="String.Empty"/>, removes all spaces. If null, spaces are not replaced.</param>
		/// <returns>A copy of the string converted to lowercase with all spaces replaced with the specified character.</returns>
		public static String LowerAndReplaceSpaces(String value, Boolean lowercase = true, String replaceChars = "_")
		{
			if (value == null) throw new ArgumentNullException(nameof(value));
			if (lowercase) value = value.ToLowerInvariant();
			if (replaceChars != null) value = Regex.Replace(value, RegexUnescSpace, replaceChars); // doesn't replace spaces preceded by a '\' (ie. escaped spaces like\ this)
			return value;
		}

		/// <summary>
		/// Gets the short name (n,u,ms,s,m,h) for the InfluxDB precision specifier to be used in the URI query string.
		/// </summary>
		/// <param name="precision">The precision to get the short name for.</param>
		/// <returns>The short name for the <see cref="InfluxPrecision"/> value.</returns>
		public static String ToShortName(this InfluxPrecision precision)
		{
			switch (precision)
			{
				case InfluxPrecision.Nanoseconds: return "n";
				case InfluxPrecision.Microseconds: return "u";
				case InfluxPrecision.Milliseconds: return "ms";
				case InfluxPrecision.Seconds: return "s";
				case InfluxPrecision.Minutes: return "m";
				case InfluxPrecision.Hours: return "h";
				default: throw new ArgumentException(nameof(precision), $"Invalid timestamp precision: {precision}");
			}
		}

		/// <summary>
		/// Gets the <see cref="InfluxPrecision"/> from the short name (n,u,ms,s,m,h) retrieved using <see cref="ToShortName(InfluxPrecision)"/>.
		/// </summary>
		/// <param name="precision">The short name of the precision specifier (n,u,ms,s,m,h).</param>
		/// <returns>The <see cref="InfluxPrecision"/> for the specified short name.</returns>
		public static InfluxPrecision FromShortName(String precision)
		{
			switch (precision)
			{
				case "n": return InfluxPrecision.Nanoseconds;
				case "u": return InfluxPrecision.Microseconds;
				case "ms": return InfluxPrecision.Milliseconds;
				case "s": return InfluxPrecision.Seconds;
				case "m": return InfluxPrecision.Minutes;
				case "h": return InfluxPrecision.Hours;
				default: throw new ArgumentException(nameof(precision), $"Invalid precision specifier: {precision}");
			}
		}

		#endregion

		#region Parse InfluxTags

		/// <summary>
		/// Parses the MetricTags into <see cref="InfluxTag"/>s. Returns an <see cref="InfluxTag"/> for each tag that is in the format: {key}={value}.
		/// </summary>
		/// <param name="tags">The tags to parse into <see cref="InfluxTag"/>s objects.</param>
		/// <returns>A sequence of <see cref="InfluxTag"/>s parsed from the specified <see cref="MetricTags"/>.</returns>
		public static IEnumerable<InfluxTag> ToInfluxTags(params MetricTags[] tags)
		{
			return tags.SelectMany(t => t.Tags).Select(ToInfluxTag).Where(t => !t.IsEmpty);
		}

		/// <summary>
		/// Splits the tag into a key/value pair using the equals sign. The tag should be in the format: {key}={value}.
		/// If the tag is in an invalid format or cannot be parsed, this returns <see cref="InfluxTag.Empty"/>.
		/// </summary>
		/// <param name="keyValuePair">The tag to parse into an <see cref="InfluxTag"/>.</param>
		/// <returns>The tag parsed into an <see cref="InfluxTag"/>, or <see cref="InfluxTag.Empty"/> if the input string is in an invalid format or could not be parsed.</returns>
		public static InfluxTag ToInfluxTag(KeyValuePair<string, string> keyValuePair)
		{
			if (string.IsNullOrWhiteSpace(keyValuePair.Key) || string.IsNullOrWhiteSpace(keyValuePair.Value))
			{
				return InfluxTag.Empty;
			}
			return new InfluxTag(keyValuePair.Key.Trim(), keyValuePair.Value.Trim());
		}

		/// <summary>
		/// Parses any tags from the <paramref name="itemName"/> and concatenates them to the end of the specified <see cref="MetricTags"/> list.
		/// If there are multiple tags with the same key in the resulting list, tags that occur later in the sequence override earlier tags.
		/// The <paramref name="itemName"/> can be a single tag value or a comma-separated list of values. Any values that are not in the
		/// key/value pair format ({key}={value}) are ignored. One exception to this is if the <paramref name="itemName"/> only has a single value
		/// and that value is not a key/value pair, an <see cref="InfluxTag"/> will be created for it using "Name" as the key and itself as the value.
		/// </summary>
		/// <param name="itemName">The item set name, this is a comma-separated list of key/value pairs.</param>
		/// <param name="tags">The tags to add in addition to any tags in the item set name.</param>
		/// <returns>A sequence of InfluxTags that contain the tags in <paramref name="tags"/> followed by any valid tags from the item name.</returns>
		public static IEnumerable<InfluxTag> JoinTags(string itemName, params MetricTags[] tags)
		{
			// if there's only one item and it's not a key/value pair, alter it to use "Name" as the key and itself as the value
			var name = itemName ?? string.Empty;
			var nameTag = new Dictionary<string,string>();
			nameTag.Add("Name", name);
			var retTags = ToInfluxTags(tags).Concat(ToInfluxTags(nameTag));
			return retTags.GroupBy(t => t.Key).Select(g => g.Last()); // this is similar to: retTags.DistinctBy(t => t.Key), but takes the last value instead so global tags get overriden by later tags
		}

		/// <summary>
		/// Concatenates all tags into one list of tags.
		/// If there are multiple tags with the same key in the resulting list, tags that occur later in the sequence override earlier tags.
		/// </summary>
		/// <param name="tags">The tags to add in addition to any tags in the item set name.</param>
		/// <returns>A sequence of InfluxTags that contain the tags in <paramref name="tags"/> followed by any valid tags from the item name.</returns>
		public static IEnumerable<InfluxTag> JoinTags(params MetricTags[] tags)
		{
			var retTags = ToInfluxTags(tags);
			return retTags.GroupBy(t => t.Key).Select(g => g.Last()); // this is similar to: retTags.DistinctBy(t => t.Key), but takes the last value instead so global tags get overriden by later tags
		}

		#endregion

		#region Type Validation

		/// <summary>
		/// The supported types that can be used for an <see cref="InfluxTag"/> or <see cref="InfluxField"/> value.
		/// </summary>
		public static readonly Type[] ValidValueTypes = new Type[] {
			typeof(Byte),   typeof(SByte),  typeof(Int16),   typeof(UInt16),  typeof(Int32), typeof(UInt32), typeof(Int64), typeof(UInt64),
			typeof(Single), typeof(Double), typeof(Decimal), typeof(Boolean), typeof(Char),  typeof(String)
		};

		/// <summary>
		/// Determines if the specified type is a valid InfluxDB value type.
		/// The valid types are String, Boolean, integral or floating-point type.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>true if the type is a valid InfluxDB value type; false otherwise.</returns>
		public static Boolean IsValidValueType(Type type)
		{
			return
				type == typeof(Char) || type == typeof(String) ||
				type == typeof(Byte) || type == typeof(SByte) ||
				type == typeof(Int16) || type == typeof(UInt16) ||
				type == typeof(Int32) || type == typeof(UInt32) ||
				type == typeof(Int64) || type == typeof(UInt64) ||
				type == typeof(Single) || type == typeof(Double) ||
				type == typeof(Decimal) || type == typeof(Boolean);
		}

		/// <summary>
		/// Determines if the specified type is an integral type.
		/// The valid integral types are Byte, Int16, Int32, Int64, and their (un)signed counterparts.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>true if the type is an integral type; false otherwise.</returns>
		public static Boolean IsIntegralType(Type type)
		{
			return
				type == typeof(Byte) || type == typeof(SByte) ||
				type == typeof(Int16) || type == typeof(UInt16) ||
				type == typeof(Int32) || type == typeof(UInt32) ||
				type == typeof(Int64) || type == typeof(UInt64);
		}

		/// <summary>
		/// Determines if the specified type is a floating-point type.
		/// The valid floating-point types are Single, Double, and Decimal.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>true if the type is a floating-point type; false otherwise.</returns>
		public static Boolean IsFloatingPointType(Type type)
		{
			return
				type == typeof(Single) ||
				type == typeof(Double) ||
				type == typeof(Decimal);
		}

		#endregion

		#region URI Helper Methods

		/// <summary>The JSON URI scheme.</summary>
		public const String SchemeJson = "http";

		/// <summary>The HTTP URI scheme.</summary>
		public const String SchemeHttp = "http";

		/// <summary>The HTTPS URI scheme.</summary>
		public const String SchemeHttps = "https";

		/// <summary>The UDP URI scheme.</summary>
		public const String SchemeUdp = "udp";

		/// <summary>
		/// Creates a URI for InfluxDB using the values specified in the <see cref="InfluxConfig"/> object.
		/// </summary>
		/// <param name="config">The configuration object to get the relevant fields to build the URI from.</param>
		/// <returns>A new InfluxDB URI using the configuration specified in the <paramref name="config"/> parameter.</returns>
		public static Uri FormatInfluxUri(this InfluxConfig config)
		{
			return FormatInfluxUri(config.Uri, config.Database, config.Username, config.Password, config.RetentionPolicy, config.Precision);
		}

		/// <summary>
		/// Creates a URI for the specified hostname and database. Uses no authentication, and optionally uses the default retention policy (DEFAULT) and time precision (s).
		/// </summary>
		/// <param name="uri">The URI of the InfluxDB server, including any query string parameters.</param>
		/// <param name="database">The name of the database to write records to.</param>
		/// <param name="retentionPolicy">The retention policy to use. Leave blank to use the server default of "DEFAULT".</param>
		/// <param name="precision">The timestamp precision specifier used in the line protocol writes. Leave blank to use the default of <see cref="InfluxConfig.Default.Precision"/>.</param>
		/// <returns>A new InfluxDB URI using the specified parameters.</returns>
		public static Uri FormatInfluxUri(Uri uri, String database, String retentionPolicy = null, InfluxPrecision? precision = null)
		{
			return FormatInfluxUri(uri, database, null, null, retentionPolicy, precision);
		}

		/// <summary>
		/// Creates a URI for the specified hostname and database using authentication. Optionally uses the default retention policy (DEFAULT) and time precision (s).
		/// </summary>
		/// <param name="uri">The URI of the InfluxDB server, including any query string parameters.</param>
		/// <param name="database">The name of the database to write records to.</param>
		/// <param name="username">The username to use to authenticate to the InfluxDB server. Leave blank to skip authentication.</param>
		/// <param name="password">The password to use to authenticate to the InfluxDB server. Leave blank to skip authentication.</param>
		/// <param name="retentionPolicy">The retention policy to use. Leave blank to use the server default of "DEFAULT".</param>
		/// <param name="precision">The timestamp precision specifier used in the line protocol writes. Leave blank to use the default of <see cref="InfluxConfig.Default.Precision"/>.</param>
		/// <returns>A new InfluxDB URI using the specified parameters.</returns>
		public static Uri FormatInfluxUri(Uri uri, String database, String username, String password, String retentionPolicy = null, InfluxPrecision? precision = null)
		{
			var absUri = uri.AbsoluteUri;
			var appendForwardSlash = absUri[absUri.Length - 1] != '/';
			if (appendForwardSlash)
			{
				absUri += "/";
			}
			var prec = precision ?? InfluxConfig.Default.Precision;
			var uriString = $@"{absUri}write?db={database}";
			if (!string.IsNullOrWhiteSpace(username)) uriString += $@"&u={username}";
			if (!string.IsNullOrWhiteSpace(password)) uriString += $@"&p={password}";
			if (!string.IsNullOrWhiteSpace(retentionPolicy)) uriString += $@"&rp={retentionPolicy}";
			if (prec != InfluxPrecision.Nanoseconds) uriString += $@"&precision={prec.ToShortName()}"; // only need to specify precision if it's not nanoseconds (the InfluxDB default)
			return new Uri(uriString);
			//return new Uri($@"{scheme}://{host}:{port}/write?db={database}&u={username}&p={password}&rp={retentionPolicy}&precision={prec.ToShortName()}");
		}


		private static readonly Regex _regex = new Regex(@"[?|&]([\w\.]+)=([^?|^&]+)");

		/// <summary>
		/// Parses the URI query string into a key/value collection.
		/// </summary>
		/// <param name="uri">The URI to parse</param>
		/// <returns>A key/value collection that contains the query parameters.</returns>
		public static IReadOnlyDictionary<String, String> ParseQueryString(this Uri uri)
		{
			var match = _regex.Match(uri.PathAndQuery);
			var paramaters = new Dictionary<String, String>();
			while (match.Success)
			{
				paramaters.Add(match.Groups[1].Value, match.Groups[2].Value);
				match = match.NextMatch();
			}
			return paramaters;
		}

		#endregion

	}
}
