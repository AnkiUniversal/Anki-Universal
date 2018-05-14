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
using System.Net;
using System.Net.Http;
using Windows.Data.Json;
using Windows.Storage;
using System.IO.Compression;

namespace AnkiU.AnkiCore.Sync
{
    [Obsolete]
    public class MediaSyncer
    {
        private Collection collection;
        private RemoteMediaServer server;
        private int downloadCount;

        public MediaSyncer(Collection collection, RemoteMediaServer server)
        {
            this.collection = collection;
            this.server = server;
        }

        public async Task<string> Sync()
        {
            // check if there have been any changes
            // If we haven't built the media db yet, do so on this sync. See note at the top
            // of this class about this difference to the original.
            if (collection.Media.NeedScan())
            {
                collection.Log(args: "findChanges");
                await collection.Media.ScanForChangesAsync();
            }

            // begin session and check if in sync
            long lastUsn = collection.Media.GetLastUnixTimeSync();
            JsonObject ret = await server.Begin();
            int srvUsn = (int)JsonHelper.GetNameNumber(ret,"usn");
            if ((lastUsn == srvUsn) && !(collection.Media.HaveDirty()))
            {
                return "noChanges";
            }
            // loop through and process changes from server
            collection.Log(args: "last local usn is " + lastUsn);
            downloadCount = 0;
            while (true)
            {
                JsonArray data = await server.MediaChanges(lastUsn);
                collection.Log(args: new object[] { "mediaChanges resp count: ", data.Count });
                if (data.Count == 0)
                    break;


                List<string> need = new List<string>();
                lastUsn = (int)data.GetArrayAt((uint)data.Count - 1).GetNumberAt(1);
                for (uint i = 0; i < data.Count; i++)
                {
                    JsonArray array = data.GetArrayAt(i);
                    string fname = array.GetStringAt(0);
                    int rusn = (int)array.GetNumberAt(1);
                    string rsum = null;
                    if (array.Count >= 3)
                        rsum = array.GetStringAt(2);
                    KeyValuePair<string, int> info = collection.Media.GetSyncInfo(fname);
                    string lsum = info.Key;
                    int ldirty = info.Value;
                    collection.Log(args: String.Format(Media.locale,
                            "check: lsum={0} rsum={1} ldirty={2} rusn={3} fname={4}",
                            String.IsNullOrEmpty(lsum) ? "" : lsum.Substring(0, 5),
                            String.IsNullOrEmpty(rsum) ? "" : lsum.Substring(0, 5),
                            ldirty,
                            rusn,
                            fname));

                    if (!String.IsNullOrEmpty(rsum))
                    {
                        // added/changed remotely
                        if (String.IsNullOrEmpty(lsum) || !lsum.Equals(rsum))
                        {
                            collection.Log(args: "will fetch");
                            need.Add(fname);
                        }
                        else
                        {
                            collection.Log(args: "have same already");
                        }
                        List<string> newList = new List<string>();
                        newList.Add(fname);
                        collection.Media.MarkClean(newList);

                    }
                    else if (!String.IsNullOrEmpty(lsum))
                    {
                        // deleted remotely
                        if (ldirty == 0)
                        {
                            collection.Log(args: "delete local");
                            await collection.Media.SyncDelete(fname);
                        }
                        else
                        {
                            // conflict: local add overrides remote delete
                            collection.Log(args: "conflict; will send");
                        }
                    }
                    else
                    {
                        // deleted both sides
                        collection.Log(args: "both sides deleted");
                        List<string> newList = new List<string>();
                        newList.Add(fname);
                        collection.Media.MarkClean(newList);
                    }
                }
                await DownloadFiles(need);

                collection.Log(args: "update last usn to " + lastUsn);
                collection.Media.SetLastUnixTimeSync(lastUsn); // commits
            }

            // at this point, we're all up to date with the server's changes,
            // and we need to send our own

            bool updateConflict = false;
            int toSend = collection.Media.DirtyCount();
            while (true)
            {
                KeyValuePair<StorageFile, List<string>> changesZip = await collection.Media.MediaChangesZip();
                StorageFile zip = changesZip.Key;
                try
                {
                    List<string> fnames = changesZip.Value;
                    if (fnames.Count == 0)
                    {
                        break;
                    }

                    JsonArray changes = await server.UploadChanges(zip);
                    int processedCnt = (int)changes.GetNumberAt(0);
                    int serverLastUsn = (int)changes.GetNumberAt(1);
                    collection.Media.MarkClean(fnames.GetRange(0, processedCnt));
                    collection.Log(args: String.Format(Media.locale,
                            "processed {0}, serverUsn {1}, clientUsn {2}",
                            processedCnt, serverLastUsn, lastUsn));

                    if (serverLastUsn - processedCnt == lastUsn)
                    {
                        collection.Log(args: "lastUsn in sync, updating local");
                        lastUsn = serverLastUsn;
                        collection.Media.SetLastUnixTimeSync(serverLastUsn); // commits
                    }
                    else
                    {
                        collection.Log(args: "concurrent update, skipping usn update");
                        updateConflict = true;
                    }

                    toSend -= processedCnt;
                }
                finally
                {
                    System.IO.File.Delete(zip.Path);
                }
            }
            if (updateConflict)
            {
                collection.Log(args: "restart sync due to concurrent update");
                return await Sync();
            }

            int lcnt = collection.Media.MediaCount();
            string sRet = await server.MediaSanity(lcnt);
            if (sRet.Equals("OK"))
            {
                return "OK";
            }
            else
            {
                collection.Media.ForceReSync();
                return sRet;
            }
        }

        private async Task DownloadFiles(List<string> fnames)
        {
            collection.Log(args: fnames.Count + " files to fetch");
            while (fnames.Count > 0)
            {
                List<string> top = fnames.GetRange(0, Math.Min(fnames.Count, Syncing.ZIP_COUNT));
                collection.Log(args: "fetch " + top);
                using (ZipArchive archive = await server.DownloadFiles(top))
                {
                    int cnt = await collection.Media.AddFilesFromZip(archive);
                    downloadCount += cnt;
                    collection.Log(args: "received " + cnt + " files");
                    // NOTE: The python version uses slices which return an empty list when indexed beyond what
                    // the list contains. Since we can't slice out an empty sublist in Java and C#, we must check
                    // if we've reached the end and clear the fnames list manually.
                    if (cnt == fnames.Count)
                    {
                        fnames.Clear();
                    }
                    else
                    {
                        fnames = fnames.GetRange(cnt, fnames.Count);
                    }

                }
            }

        }
    }
}
