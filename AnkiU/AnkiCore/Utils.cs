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

namespace AnkiU.AnkiCore
{
    static class Utils
    {
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

        public static async Task<string> FileChecksum(StorageFile file)
        {
            try
            {
                var text = await FileIO.ReadTextAsync(file);

                IBuffer buffUtf8Msg = CryptographicBuffer.ConvertStringToBinary(text, BinaryStringEncoding.Utf8);
                HashAlgorithmProvider objAlgProv = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
                IBuffer buffHash = objAlgProv.HashData(buffUtf8Msg);

                // Verify that the hash length equals the length specified for the algorithm.
                if (buffHash.Length != objAlgProv.HashLength)
                {
                    throw new Exception("Utils.fileChecksum: There was an error creating the hash");
                }

                string strHashBase64 = CryptographicBuffer.EncodeToBase64String(buffHash);

                // pad with zeros to length of 40 - SHA1 is 160bit long
                if (strHashBase64.Length < 40)
                {
                    String padString = new String('0', 40);
                    strHashBase64 = padString.Substring(0, 40 - strHashBase64.Length) + strHashBase64;
                }
                return strHashBase64;
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

        public static void AddAll<T>(ISet<T> set, List<T> array)
        {
            foreach(T a in array)
                set.Add(a);
        }

        public static void AddAll<T>(ISet<T> set, T[] array)
        {
            for (int i = 0; i < array.Length; i++)
                set.Add(array[i]);
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
                string s = String.Join<long>(",", ids);
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
                result.Append("\u001f");
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
                result.Append("\u001f");
            }
            if (list.Count > 0)
            {
                result.Append(list[list.Count - 1]);
            }
            return result.ToString();
        }

        public static string[] SplitFields(string fields)
        {
            return fields.Split(new string[] { "\\x1f" }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static JsonArray ToJsonArray(this List<JsonObject> jList)
        {
            JsonArray jArray = new JsonArray();
            foreach (JsonObject o in jList)
                jArray.Add(o);
            return jArray;
        }
    }
}
