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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using System.IO;
using System.IO.Compression;
using Windows.Web.Http;
using Windows.Web;

namespace AnkiU.AnkiCore.Sync
{
    /// <summary>
    /// Calling code should catch the following codes:
    /// 501: client needs upgrade
    /// 502: ankiweb down
    /// 503/504: server too busy
    /// </summary>
    public abstract class HttpSyncer : ISyncer
    {
        private const string BOUNDARY = "Anki-sync-boundary";
        public const string ANKIWEB_STATUS_OK = "OK";

        protected string hKey;
        protected string sKey;
        protected Dictionary<string, object> postVars;

        public HttpSyncer(string hkey)
        {
            hKey = hkey;
            sKey = Utils.Checksum((new Random().NextDouble().ToString())).Substring(0, 8);
            postVars = new Dictionary<string, object>();
        }
        
        public void AssertOk(HttpResponseMessage resp)
        {
            // Throw RuntimeException if HTTP error
            if (resp == null)
            {
                throw new UnknownHttpResponseException("Null HttpResponse", HttpStatusCode.NoContent);
            }

            HttpStatusCode resultCode = resp.StatusCode;
            if (!(resultCode == HttpStatusCode.Ok || resultCode == HttpStatusCode.Forbidden))
            {
                string reason = resp.ReasonPhrase;
                throw new UnknownHttpResponseException(reason, resultCode);
            }
        }

        /// <summary>
        /// Posting data as a file
        /// We don't want to post the payload as a form var, as the percent-encoding is
        /// costly. We could send it as a raw post, but more HTTP clients seem to
        /// support file uploading, so this is the more compatible choice.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="inputStream"></param>
        /// <param name="comp"></param>
        /// <param name="registerData"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> Request(string method, Stream inputStream = null, int comp = 6, JsonObject registerData = null)
        {
            try
            {
                using (StringWriter buf = new StringWriter())
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    string bdry = "--" + BOUNDARY;
                    // post vars
                    postVars["c"] = comp != 0 ? 1 : 0;
                    foreach (string key in postVars.Keys)
                    {
                        buf.Write(bdry + "\r\n");
                        buf.Write(String.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}\r\n",
                                    key, postVars[key]));
                    }


                    // payload as raw data or json
                    if (inputStream != null)
                    {
                        // header
                        buf.Write(bdry + "\r\n");
                        buf.Write("Content-Disposition: form-data; name=\"data\"; filename=\"data\"\r\nContent-Type: application/octet-stream\r\n\r\n");
                        byte[] buffer = Encoding.UTF8.GetBytes(buf.ToString());
                        memoryStream.Write(buffer, 0, buffer.Length);

                        // write file into buffer, optionally compressing
                        int len = 0;
                        byte[] chunk = new byte[65536];
                        if (comp != 0)
                        {
                            using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                            {
                                while ((len = inputStream.Read(chunk, 0, chunk.Length)) > 0)
                                {
                                    gzipStream.Write(chunk, 0, len);
                                }
                            }
                        }
                        else
                        {
                            while ((len = inputStream.Read(chunk, 0, chunk.Length)) > 0)
                            {
                                memoryStream.Write(chunk, 0, len);
                            }
                        }
                        buffer = Encoding.UTF8.GetBytes("\r\n" + bdry + "--\r\n");
                        memoryStream.Write(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(buf.ToString());
                        memoryStream.Write(buffer, 0, buffer.Length);
                    }

                    // connection headers
                    string url = Syncing.BASE;
                    if (method.Equals("register"))
                    {
                        url = url + "account/signup" + "?username=" + registerData.GetNamedString("u") + "&password="
                                + registerData.GetNamedString("p");
                    }
                    else if (method.StartsWith("upgrade"))
                    {
                        url = url + method;
                    }
                    else
                    {
                        url = SyncURL() + method;
                    }

                    memoryStream.Position = 0;
                    Uri uri = new Uri(url);
                    HttpRequestMessage httpPost = new HttpRequestMessage(HttpMethod.Post, uri);
                    HttpStreamContent streamContent = new HttpStreamContent(memoryStream.AsInputStream());
                    httpPost.Content = streamContent;

                    httpPost.Content.Headers.Add("Content-type", "multipart/form-data; boundary=" + BOUNDARY);
                    using (HttpClient httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Add("user-agent", "Windows 10 (Anki Universal)");
                        HttpResponseMessage httpResponse = await httpClient.SendRequestAsync(httpPost);
                        // we assume badAuthRaises flag from Anki Desktop always False
                        // so just throw new RuntimeException if response code not 200 or 403
                        AssertOk(httpResponse);
                        return httpResponse;
                    }
                }
            }
            catch (UnknownHttpResponseException ex)
            {
                string message = HttpRequestExceptionHandler(ex);
                throw new HttpSyncerException(message);
            }            
            catch (Exception ex)
            {
                string message = HttpClientExceptionHandler(ex.HResult, ex);
                throw new HttpSyncerException(message);
            }
        }

        public static string HttpRequestExceptionHandler(UnknownHttpResponseException ex)
        {
            var error = ex.ResponseCode;            
            if (error == HttpStatusCode.RequestTimeout)
                return "The connection to AnkiWeb timed out. Please check your network connection and try again.";
            else if (error == HttpStatusCode.InternalServerError)
                return "AnkiWeb encountered an error. Please try again in a few minutes, and if the problem persists, please file a bug report.";
            else if (error == HttpStatusCode.NotImplemented)
                return "Please upgrade to the latest version of Anki.";
            else if (error == HttpStatusCode.BadGateway)
                return "AnkiWeb is under maintenance. Please try again in a few minutes.";
            else if (error == HttpStatusCode.ServiceUnavailable)
                return "AnkiWeb is too busy at the moment. Please try again in a few minutes.";
            else if (error == HttpStatusCode.GatewayTimeout)
                return "504 gateway timeout error received. Please try temporarily disabling your antivirus.";
            else if (error == HttpStatusCode.Conflict)
                return "Only one client can access AnkiWeb at a time. If a previous sync failed, please try again in a few minutes.";
            else if (error == HttpStatusCode.ProxyAuthenticationRequired)
                return "Proxy authentication required.";
            else if (error == HttpStatusCode.RequestEntityTooLarge)
                return "Your collection or a media file is too large to sync.";
            else
                return String.Format("Unknown Response Code: {0:X}! {1}", ex.ResponseCode, ex.Message);
        }

        public static string HttpClientExceptionHandler(int code, Exception ex)
        {
            WebErrorStatus error = WebError.GetStatus(code);
            if (error == WebErrorStatus.CannotConnect
                  || error == WebErrorStatus.CertificateCommonNameIsIncorrect
                  || error == WebErrorStatus.CertificateContainsErrors
                  || error == WebErrorStatus.CertificateExpired
                  || error == WebErrorStatus.CertificateIsInvalid
                  || error == WebErrorStatus.CertificateRevoked)
                return "Error establishing a secure connection. This is usually caused by antivirus, firewall or VPN software, or problems with your ISP.";
            else if (error == WebErrorStatus.Timeout)
                return "The connection to AnkiWeb timed out. Please check your network connection and try again.";
            else if (error == WebErrorStatus.InternalServerError)
                return "AnkiWeb encountered an error. Please try again in a few minutes, and if the problem persists, please file a bug report.";
            else if (error == WebErrorStatus.NotImplemented)
                return "Please upgrade to the latest version of Anki.";
            else if (error == WebErrorStatus.BadGateway)
                return  "AnkiWeb is under maintenance. Please try again in a few minutes.";
            else if (error == WebErrorStatus.ServiceUnavailable)
                return "AnkiWeb is too busy at the moment. Please try again in a few minutes.";
            else if (error == WebErrorStatus.GatewayTimeout)
                return  "504 gateway timeout error received. Please try temporarily disabling your antivirus.";
            else if (error == WebErrorStatus.Conflict)
                return "Only one client can access AnkiWeb at a time. If a previous sync failed, please try again in a few minutes.";
            else if (error == WebErrorStatus.ProxyAuthenticationRequired)
                return "Proxy authentication required.";
            else if (error == WebErrorStatus.RequestEntityTooLarge)
                return "Your collection or a media file is too large to sync.";
            else
                return String.Format("Error Code: {0:X}! {1}", ex.HResult, ex.Message);
        }

        public void WriteToFile(Stream source, string destination)
        {
            string fullPath = Storage.AppLocalFolder.Path + "\\" + destination;
            try
            {
                using (FileStream file = new FileStream(fullPath, FileMode.Create))
                {
                    byte[] buf = new byte[Utils.CHUNK_SIZE];
                    int len;
                    while ((len = source.Read(buf, 0, buf.Length)) > 0)
                    {
                        file.Write(buf, 0, len);
                    }
                }
            }
            catch (IOException e)
            {
                // Don't keep the file if something went wrong. It'll be corrupt.
                File.Delete(fullPath);
                // Re-throw so we know what the error was.
                throw e;
            }
        }

        public static string Stream2String(FileStream stream, int maxSize = -1)
        {
            StringBuilder sb = new StringBuilder();

            int bufferSize = maxSize == -1 ? 4096 : Math.Min(4096, maxSize);

            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, bufferSize))
            {
                string line;

                while ((line = reader.ReadLine()) != null && (maxSize == -1 || sb.Length < maxSize))
                {
                    sb.Append(line);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Allow user to specify custom sync server
        /// </summary>
        /// <returns></returns>
        public virtual string SyncURL()
        {
            //TODO: User custom sync server:
            //Get user preferences
            //If verify valid custom sync preference is true
            //  return path + "sync/"
            //else
            return Syncing.BASE + "sync/";
        }

        public static MemoryStream GetInputStream(string str)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(str ?? ""));
        }

        public virtual Task<string> HostKey(string user, string password) { return null; }
        public virtual Task<JsonObject> ApplyChanges(JsonObject kw) { return null; }
        public virtual Task<JsonObject> Start(JsonObject kw) { return null; }
        public virtual Task<JsonObject> Chunk(JsonObject kw = null) { return null; }
        public virtual Task<long> Finish()
        {
            Task<long> task = Task<long>.Factory.StartNew( () => { return 0L; });
            task.Wait();
            return task;
        }
        public virtual Task<HttpResponseMessage> Meta() { return null; }
        public virtual Task<object[]> Download() { return null; }
        public virtual Task<object[]> Upload() { return null; }
        public virtual Task<JsonObject> SanityCheck2(JsonObject client) { return null; }
        public virtual Task ApplyChunk(JsonObject sech) { return null; }
        public virtual Task<HttpResponseMessage> Register(string user, string pw) { return null; }
    }

    public class UnknownHttpResponseException : Exception
    {
        
        private HttpStatusCode responseCode;
        public HttpStatusCode ResponseCode { get { return responseCode; } }

        public UnknownHttpResponseException(string message)
            : base(message)
        {
        }

        public UnknownHttpResponseException(string message, HttpStatusCode code)
            : base (message)
        {
            this.responseCode = code;
        }
    }

    public class HttpSyncerException : Exception
    {
        public HttpSyncerException(string message) : base(message)
        { }
    }

}
