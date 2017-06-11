/*
Copyright (C) 2016 Anki Universal Team <ankiuniversal@outlook.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Windows.Storage;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.Data.Json;
using System.Text.RegularExpressions;
using Windows.System.Profile;
using Windows.ApplicationModel;
using Windows.Security.ExchangeActiveSyncProvisioning;
using System.Globalization;
using System.Linq;
using System.IO.Compression;

namespace Shared.AnkiCore
{
    public static class Utils
    {
        public static readonly uint APP_VERSION = ApplicationData.Current.Version;
        public const int CHUNK_SIZE = 32768;

        private static readonly int CHECKSUM_BUFFER;

        private const string ALL_CHARACTERS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private const string BASE91_EXTRA_CHARS = "!#$%&()*+,-./:;<=>?@[]^_`{|}~";

        // Regex pattern used in removing tags from text before diff
        private static readonly Regex stylePattern = new Regex("(?s)<style.*?>.*?</style>", RegexOptions.Compiled);
        private static readonly Regex scriptPattern = new Regex("(?s)<script.*?>.*?</script>", RegexOptions.Compiled);
        private static readonly Regex tagPattern = new Regex(@"(?s)<.*?>", RegexOptions.Compiled);        
        private static readonly Regex imgPattern = new Regex("<img src=[\\\"']?([^\\\"'>]+)[\\\"']? ?/?>", RegexOptions.Compiled);
        private static readonly Regex htmlEntitiesPattern = new Regex("&#?\\w+;", RegexOptions.Compiled);

        public static readonly Regex BreakPattern = new Regex(@"(?s)<br[ /]*?>", RegexOptions.Compiled);
        public static readonly Regex StartDivPattern = new Regex(@"(?s)<div>", RegexOptions.Compiled);
        public static readonly Regex EndDivPattern = new Regex(@"(?s)</div>", RegexOptions.Compiled);

        static Utils()
        {
            switch (AnalyticsInfo.VersionInfo.DeviceFamily)
            {
                case "Windows.Mobile":
                    CHECKSUM_BUFFER = 1024;
                    break;
                case "Windows.Desktop":
                    CHECKSUM_BUFFER = 10 * 1024;
                    break;
                case "Windows.Universal":
                    CHECKSUM_BUFFER = 1024;
                    break;
                case "Windows.Team":
                    CHECKSUM_BUFFER = 1024;
                    break;
                default:
                    break;
            }
        }

        public static string[] SplitNameAndExtension(string fileName)
        {
            string name = fileName;
            string ext = "";
            int dotPosition = fileName.LastIndexOf('.');
            if(dotPosition != -1)
            {
                name = fileName.Substring(0, dotPosition);
                ext = fileName.Substring(dotPosition);
            }
            return new string[] { name, ext };
        }

        public static async Task<bool> CompareByteToByte(StorageFile comparer, StorageFile compared)
        {
            if ((await comparer.GetBasicPropertiesAsync()).Size != (await compared.GetBasicPropertiesAsync()).Size)
                return false;

            byte[] buffer1 = new byte[CHECKSUM_BUFFER];
            byte[] buffer2 = new byte[CHECKSUM_BUFFER];
            int readCount1;
            int readCount2;

            using (var firstStream = (await comparer.OpenReadAsync()).AsStreamForRead())
            using (var secondStream = (await compared.OpenReadAsync()).AsStreamForRead())
            {
                while (true)
                {
                    readCount1 = firstStream.Read(buffer1, 0, CHECKSUM_BUFFER);
                    readCount2 = secondStream.Read(buffer2, 0, CHECKSUM_BUFFER);

                    if (readCount1 != readCount2)
                        return false;

                    if (readCount1 == 0)
                        return true;

                    for(int i = 0; i < readCount1; i++)
                    {
                        if (buffer1[i] != buffer2[i])
                            return false;
                    }
                }
            }        
        }

        public static async Task<string> FileChecksum(StorageFile file)
        {
            try
            {
                using (var fstream = await file.OpenReadAsync())
                using (StreamReader reader = new StreamReader(fstream.AsStreamForRead()))
                {                    
                    
                    char[] buffer = new char[CHECKSUM_BUFFER];
                    var numberOfReadChar = await reader.ReadAsync(buffer, 0, CHECKSUM_BUFFER);
                    string text = new string(buffer, 0, numberOfReadChar);
                    return Checksum(text);
                }
            }
            catch (FileNotFoundException e)
            {
                throw new Exception("Utils.fileChecksum: File not found.", e);
            }
            catch (IOException e)
            {
                throw new Exception("Utils.fileChecksum: IO exception.", e);
            }
        }

        public static string Checksum(string text)
        {            
            IBuffer buffUtf8Msg = CryptographicBuffer.ConvertStringToBinary(text, BinaryStringEncoding.Utf8);
            HashAlgorithmProvider objAlgProv = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
            IBuffer buffHash = objAlgProv.HashData(buffUtf8Msg);
            
            // Verify that the hash length equals the length specified for the algorithm.
            if (buffHash.Length != objAlgProv.HashLength)
            {
                throw new Exception("Utils.fileChecksum: There was an error creating the hash");
            }

            string strHashBase64 = CryptographicBuffer.EncodeToHexString(buffHash);

            // pad with zeros to length of 40 - SHA1 is 160bit long
            if (strHashBase64.Length < 40)
            {
                string padString = new string('0', 40);
                strHashBase64 = padString.Substring(0, 40 - strHashBase64.Length) + strHashBase64;
            }
            return strHashBase64;
        }

        public static void AddAll<T>(ISet<T> set, T[] array)
        {
            for (int i = 0; i < array.Length; i++)
                set.Add(array[i]);
        }

        public static JsonArray StringNumberToJsonArray(string str)
        {
            var array = str.Split(new string[] { " ", ",", ".", ";" }, StringSplitOptions.RemoveEmptyEntries);
            JsonArray json = new JsonArray();

            if (array.Count() == 0)
            {
                json.Add(JsonValue.CreateNumberValue(0));
                return json;
            }

            foreach (var s in array)
                json.Add(JsonValue.CreateNumberValue(Convert.ToInt64(s)));
            return json;
        }

        public static string JsonNumberArrayToString(JsonArray json)
        {
            StringBuilder str = new StringBuilder();
            foreach (var j in json)
            {
                str.Append(j.GetNumber());
                str.Append(" ");
            }
            return str.ToString().Trim();
        }

        public static string JsonToString(JsonObject json)
        {
            return json.ToString().Replace("\\\\/", "/");
        }

        public static string JsonToString(JsonArray json)
        {
            return json.ToString().Replace("\\\\/", "/");
        }

        /// <summary>
        /// Given a list of integers, return a string '(int1,int2,...)'.
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public static string Ids2str(long[] ids)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            if (ids != null)
            {
                string s = string.Join<long>(",", ids);
                sb.Append(s);
            }
            sb.Append(")");
            return sb.ToString();
        }

        public static string Ids2str(List<long> ids)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            if (ids != null)
            {
                string s = string.Join<long>(",", ids);
                sb.Append(s);
            }
            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// Given a list of integers, return a string '(int1,int2,...)'.
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public static string Ids2str(int[] ids)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            if (ids != null)
            {
                string s = String.Join<int>(",", ids);
                sb.Append(s);
            }
            sb.Append(")");
            return sb.ToString();
        }

        public static string JoinFields(string[] list)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < list.Length - 1; i++)
            {
                result.Append(list[i]);
                result.Append("\x1f");
            }
            if (list.Length > 0)
            {
                result.Append(list[list.Length - 1]);
            }
            return result.ToString();
        }

        public static string JoinFields(List<string> list)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < list.Count - 1; i++)
            {
                result.Append(list[i]);
                result.Append("\x1f");
            }
            if (list.Count > 0)
            {
                result.Append(list[list.Count - 1]);
            }
            return result.ToString();
        }

        public static string[] SplitFields(string fields)
        {
            return fields.Split(new string[] { "\x1f" }, StringSplitOptions.None);
        }

        public static JsonArray ToJsonArray(this List<JsonObject> jList)
        {
            JsonArray jArray = new JsonArray();
            foreach (JsonObject o in jList)
                jArray.Add(o);
            return jArray;
        }

        /// <summary>
        /// Return a non-conflicting timestamp for table
        /// </summary>
        /// <param name="db"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static long TimestampID(DB db, String table)
        {
            // be careful not to create multiple objects without flushing them, or they
            // may share an ID.
            long t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            while (db.QueryScalar<long>("SELECT id FROM " + table + " WHERE id = " + t) != 0)
            {
                t += 1;
            }
            return t;
        }

        public static LinkedList<T> Shuffle<T>(LinkedList<T> linkedList, Random rand)
        {
            int n = linkedList.Count;
            var list = new List<T>(linkedList);
            while (n > 1)
            {
                n--;
                int k = rand.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            return new LinkedList<T>(list);
        }

        public static void Shuffle<T>(this IList<T> list, Random rand)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rand.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// All printable characters minus quotes, backslash and separators
        /// </summary>
        /// <param name="num"></param>
        /// <param name="extra"></param>
        /// <returns></returns>
        public static string Base62(int num, string extra)
        {
            string table = ALL_CHARACTERS + extra;
            int len = table.Length;
            StringBuilder buf = new StringBuilder();
            int mod = 0;
            while (num != 0)
            {
                mod = num % len;
                buf.Append (table[mod]);
                num = num / len;
            }
            return buf.ToString();
        }

        /// <summary>
        /// All printable characters minus quotes, backslash and separators
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string Base91(int num)
        {
            return Base62(num, BASE91_EXTRA_CHARS);
        }

        /// <summary>
        /// Return a base91-encoded 64bit random number
        /// </summary>
        /// <returns></returns>
        public static string Guid64()
        {
            Random r = new Random();
            var number = r.Next(0, (int)Math.Pow(2, 61) - 1);
            return Base91(number);
        }

        /// <summary>
        /// Strip HTML but keep media filenames
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static String StripHTMLMedia(String s)
        {
            return StripHTML(imgPattern.Replace(s, " $1 "));
        }

        /// <summary>
        /// Strips a text from <style>...</style>, <script>...</script> and <_any_tag_> HTML tags
        /// </summary>
        /// <param name="s">The HTML text to be cleaned</param>
        /// <returns>The text without the aforementioned tags</returns>
        public static string StripHTML(String s)
        {
            s = stylePattern.Replace(s, "");
            s = scriptPattern.Replace(s, "");
            s = tagPattern.Replace(s, "");
            return EntsToTxt(s);
        }

        /// <summary>
        /// Takes a string and replaces all the HTML symbols in it with their unescaped representation.
        /// This should only affect substrings of the form &something; and not tags.
        /// TODO: Need testing
        /// </summary>
        /// <param name="html">The HTML escaped text</param>
        /// <returns>The text with its HTML entities unescaped</returns>
        private static String EntsToTxt(string html)
        {
            MatchCollection htmlEntities = htmlEntitiesPattern.Matches(html);

            if (htmlEntities.Count != 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Match entry in htmlEntities)
                {
                    sb.AppendAndReplace(System.Net.WebUtility.HtmlDecode(entry.ToString()), html, entry);
                }
                return sb.ToString();
            }
            else
                return html;
            
        }

        public static long FieldChecksum(string data)
        {
            return Convert.ToInt64(Checksum(StripHTMLMedia(data)).Substring(0, 8), 16);
        }

        /// <summary>
        /// Return the first safe ID to use
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static long MaxID(DB db)
        {
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            now = Math.Max(now, db.QueryScalar<long>("SELECT MAX(id) FROM cards"));
            now = Math.Max(now, db.QueryScalar<long>("SELECT MAX(id) FROM notes"));
            return now + 1;
        }

        public static long[] JsonArrayToLongArray(JsonArray json)
        {
            long[] ar = new long[json.Count];
            for (uint i = 0; i < json.Count; i++)
            {
                ar[i] = (long)json.GetNumberAt(i);
            }
            return ar;
        }

        public static object[] JsonArray2Objects(JsonArray json)
        {
            object[] o = new object[json.Count];
            for (int i = 0; i < json.Count; i++)
            {
                switch (json[i].ValueType)
                {
                    case JsonValueType.Number:
                        o[i] = json[i].GetNumber();
                        break;
                    case JsonValueType.String:
                        o[i] = json[i].GetString();
                        break;
                    case JsonValueType.Boolean:
                        o[i] = json[i].GetBoolean();
                        break;
                    default:
                        throw new Exception("JsonArray2Objects: Not supported type!");
                }
            }
            return o;
        }

        /// <summary>
        /// Increment a guid by one, for note type conflicts
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static string GetGuidIncrease(string guid)
        {
            string s = guid.ReverseStringHelper();
            return IncreaseGuid(s).ReverseStringHelper();
        }

        private static string IncreaseGuid(string guid)
        {
            string table = ALL_CHARACTERS + BASE91_EXTRA_CHARS;
            int idx = table.IndexOf(guid.Substring(0, 1));
            if (idx + 1 == table.Length)
            {
                // overflow
                guid = table[0] + IncreaseGuid(guid.Substring(1, guid.Length-1));
            }
            else
            {
                guid = table[idx + 1] + guid.Substring(1, guid.Length-1);
            }
            return guid;
        }

        private static IEnumerable<string> StringClusters(this string s)
        {
            var enumerator = StringInfo.GetTextElementEnumerator(s);
            while (enumerator.MoveNext())
            {
                yield return (string)enumerator.Current;
            }
        }
        private static string ReverseStringHelper(this string s)
        {
            return string.Join("", s.StringClusters().Reverse().ToArray());
        }

        public static void UnZipNotFolderEntries(ZipArchive zipFile, string absolutePathTargetDirectory, 
                                                string[] zipEntries, Dictionary<string, string> zipEntryToFilenameMap)
        {
            if (zipEntryToFilenameMap == null)
            {
                zipEntryToFilenameMap = new Dictionary<string, string>();
            }

            foreach(string requestedEntry in zipEntries)
            {
                ZipArchiveEntry entry = zipFile.GetEntry(requestedEntry);
                if(entry != null)
                {
                    string name = entry.Name;
                    if(zipEntryToFilenameMap.ContainsKey(name))
                    {
                        name = zipEntryToFilenameMap[name];
                    }
                    if(!IsZipEntryFolder(entry))
                        entry.ExtractToFile(absolutePathTargetDirectory + "\\" + name);
                }
            }

        }

        private static bool IsZipEntryFolder(ZipArchiveEntry entry)
        {
            if (entry.FullName.EndsWith(@"/") || entry.FullName.EndsWith(@"\"))
                return true;
            else return false;
        }

        public static string TrimAndJoinStringArray(IEnumerable<string> str)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var s in str)
            {
                var t = s.Trim();
                if (!String.IsNullOrWhiteSpace(t))
                {
                    if (builder.Length != 0)
                        builder.Append("\n");

                    builder.Append(t);
                }
            }
            return builder.ToString();
        }

        public static string GetValidName(string name)
        {
            var filterSplit = name.Trim().Split(Constant.ILLEGAL_CHAR, StringSplitOptions.RemoveEmptyEntries);
            var filterText = String.Join("", filterSplit);

            var stringSplit = filterText.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            return String.Join(" ", stringSplit);
        }

    }

    public static class DeviceInfoHelper
    {
        public static string SystemFamily { get; }
        public static string SystemVersion { get; }
        public static string SystemArchitecture { get; }
        public static string ApplicationName { get; }
        public static string ApplicationVersion { get; }
        public static string DeviceManufacturer { get; }
        public static string DeviceModel { get; }

        static DeviceInfoHelper()
        {
            // get the system family name
            AnalyticsVersionInfo ai = AnalyticsInfo.VersionInfo;
            SystemFamily = ai.DeviceFamily;

            // get the system version number
            string sv = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong v = ulong.Parse(sv);
            ulong v1 = (v & 0xFFFF000000000000L) >> 48;
            ulong v2 = (v & 0x0000FFFF00000000L) >> 32;
            ulong v3 = (v & 0x00000000FFFF0000L) >> 16;
            ulong v4 = (v & 0x000000000000FFFFL);
            SystemVersion = $"{v1}.{v2}.{v3}.{v4}";

            // get the package architecure
            Package package = Package.Current;
            SystemArchitecture = package.Id.Architecture.ToString();

            // get the user friendly app name
            ApplicationName = package.DisplayName;

            // get the app version
            PackageVersion pv = package.Id.Version;
            ApplicationVersion = $"{pv.Major}.{pv.Minor}.{pv.Build}.{pv.Revision}";

            // get the device manufacturer and model name
            EasClientDeviceInformation eas = new EasClientDeviceInformation();
            DeviceManufacturer = eas.SystemManufacturer;
            DeviceModel = eas.SystemProductName;
        }
    }

    public sealed class TempFile : IDisposable
    {
        string path;
        public TempFile() : this(System.IO.Path.GetTempFileName()) { }

        public TempFile(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            this.path = path;
        }
        public string Path
        {
            get
            {
                if (path == null) throw new ObjectDisposedException(GetType().Name);
                return path;
            }
        }
        ~TempFile() { Dispose(false); }
        public void Dispose() { Dispose(true); }
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
            if (path != null)
            {
                try { File.Delete(path); }
                catch { } // best effort
                path = null;
            }
        }
    }
}
