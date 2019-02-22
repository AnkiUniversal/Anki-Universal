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

using AnkiU.AnkiCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Data.Json;

namespace AnkiU.Anki
{
    [SQLite.Net.Attributes.Table("Preference")]
    public class GeneralPreference
    {
        [SQLite.Net.Attributes.Ignore]
        public bool IsModified { get; set; }

        private int uniqueId;
        [SQLite.Net.Attributes.PrimaryKey, SQLite.Net.Attributes.Column("UniqueId")]
        public int UniqueId
        {
            get { return uniqueId; }
            set
            {
                if (uniqueId == value)
                    return;

                uniqueId = value;
                IsModified = true;
            }
        }

        private bool isFirstTimeOpenApp;
        [SQLite.Net.Attributes.Column("IsFirstTimeOpenApp")]
        public bool IsFirstTimeOpenApp
        {
            get { return isFirstTimeOpenApp; }
            set
            {
                if (isFirstTimeOpenApp == value)
                    return;

                isFirstTimeOpenApp = value;
                IsModified = true;
            }
        }

        private double zoomLevel;
        [SQLite.Net.Attributes.Column("ZoomLevel")]
        public double ZoomLevel
        {
            get { return zoomLevel; }
            set
            {
                if (zoomLevel == value)
                    return;
                
                zoomLevel = value;
                IsModified = true;
            }
        }

        private bool isBlackNightModeLearning;
        [SQLite.Net.Attributes.Column("IsBlackNightModeLearning")]
        public bool IsBlackNightModeLearning
        {
            get { return isBlackNightModeLearning; }
            set
            {
                if (isBlackNightModeLearning == value)
                    return;

                isBlackNightModeLearning = value;
                IsModified = true;
            }
        }


        private bool isReadNightMode;
        [SQLite.Net.Attributes.Column("IsReadNightMode")]
        public bool IsReadNightMode
        {
            get { return isReadNightMode; }
            set
            {
                if (isReadNightMode == value)
                    return;

                isReadNightMode = value;
                IsModified = true;
            }
        }

        private bool isOneHandMode;
        [SQLite.Net.Attributes.Column("IsOneHandMode")]
        public bool IsOneHandMode
        {
            get { return isOneHandMode; }
            set
            {
                if (isOneHandMode == value)
                    return;

                isOneHandMode = value;
                IsModified = true;
            }
        }

        private bool isLeftHand;
        [SQLite.Net.Attributes.Column("IsLeftHand")]
        public bool IsLeftHand
        {
            get { return isLeftHand; }
            set
            {
                if (isLeftHand == value)
                    return;

                isLeftHand = value;
                IsModified = true;
            }
        }

        private bool isDeckListView;
        [SQLite.Net.Attributes.Column("IsDeckListView")]
        public bool IsDeckListView
        {
            get { return isDeckListView; }
            set
            {
                if (isDeckListView == value)
                    return;

                isDeckListView = value;
                IsModified = true;
            }
        }

        private int sortDeckBy;
        [SQLite.Net.Attributes.Column("SortDeckBy")]
        public int SortDeckBy
        {
            get { return sortDeckBy; }
            set
            {
                if (sortDeckBy == value)
                    return;

                sortDeckBy = value;
                IsModified = true;
            }
        }

        private bool isHasInkDeckPreference;
        [SQLite.Net.Attributes.Column("IsHasInkDeck")]
        public bool IsHasInkDeckPreference
        {
            get { return isHasInkDeckPreference; }
            set
            {
                if (isHasInkDeckPreference == value)
                    return;

                isHasInkDeckPreference = value;
                IsModified = true;
            }
        }

        private bool isHasTextSynthDeckPreference;
        [SQLite.Net.Attributes.Column("IsHasTextSynthDeck")]
        public bool IsHasTextSynthDeckPreference
        {
            get { return isHasTextSynthDeckPreference; }
            set
            {
                if (isHasTextSynthDeckPreference == value)
                    return;

                isHasTextSynthDeckPreference = value;
                IsModified = true;
            }
        }

        private bool isAutoPlayTextSynth;
        [SQLite.Net.Attributes.Column("IsAutoPlayTextSynth")]
        public bool IsAutoPlayTextSynth
        {
            get { return isAutoPlayTextSynth; }
            set
            {
                if (isAutoPlayTextSynth == value)
                    return;

                isAutoPlayTextSynth = value;
                IsModified = true;
            }
        }

        //Currently we always do a full sync
        private bool isFullSyncRequire;
        [SQLite.Net.Attributes.Column("IsFullSyncRequire")]
        public bool IsFullSyncRequire
        {
            get { return isFullSyncRequire; }
            set
            {
                if (isFullSyncRequire == value)
                    return;

                isFullSyncRequire = value;
                IsModified = true;
            }
        }

        private long lastSyncTime;
        [SQLite.Net.Attributes.Column("LastSyncTime")]
        public long LastSyncTime
        {
            get { return lastSyncTime; }
            set
            {
                if (lastSyncTime == value)
                    return;

                lastSyncTime = value;
                IsModified = true;
            }
        }

        private bool isSyncMedia;
        [SQLite.Net.Attributes.Column("IsSyncMedia")]
        public bool IsSyncMedia
        {
            get { return isSyncMedia; }
            set
            {
                if (isSyncMedia == value)
                    return;

                isSyncMedia = value;
                IsModified = true;
            }
        }

        private bool isSyncOnOpen;
        [SQLite.Net.Attributes.Column("IsSyncOnOpen")]
        public bool IsSyncOnOpen
        {
            get { return isSyncOnOpen; }
            set
            {
                if (isSyncOnOpen == value)
                    return;

                isSyncOnOpen = value;
                IsModified = true;
            }
        }

        private bool isSyncOnClose;
        [SQLite.Net.Attributes.Column("IsSyncOnClose")]
        public bool IsSyncOnClose
        {
            get { return isSyncOnClose; }
            set
            {
                if (isSyncOnClose == value)
                    return;

                isSyncOnClose = value;
                IsModified = true;
            }
        }

        private bool isShowLeechActionOnce;
        [SQLite.Net.Attributes.Column("IsShowLeechActionOnce")]
        public bool IsShowLeechActionOnce
        {
            get { return isShowLeechActionOnce; }
            set
            {
                if (isShowLeechActionOnce == value)
                    return;

                isShowLeechActionOnce = value;
                IsModified = true;
            }
        }

        private int backupsMinTime;
        [SQLite.Net.Attributes.Column("BackupsMinTime")]
        public int BackupsMinTime
        {
            get { return backupsMinTime; }
            set
            {
                if (backupsMinTime == value)
                    return;

                backupsMinTime = value;
                IsModified = true;
            }
        }

        private int numberOfBackups;
        [SQLite.Net.Attributes.Column("NumberOfBackups")]
        public int NumberOfBackups
        {
            get { return numberOfBackups; }
            set
            {
                if (numberOfBackups == value)
                    return;

                numberOfBackups = value;
                IsModified = true;
            }
        }

        private bool isCompressBackup;
        [SQLite.Net.Attributes.Column("IsCompressBackup")]
        public bool IsCompressBackup
        {
            get { return isCompressBackup; }
            set
            {
                if (isCompressBackup == value)
                    return;

                isCompressBackup = value;
                IsModified = true;
            }
        }

        private long lastBackups;
        [SQLite.Net.Attributes.Column("LastBackups")]
        public long LastBackups
        {
            get { return lastBackups; }
            set
            {
                if (lastBackups == value)
                    return;

                lastBackups = value;
                IsModified = true;
            }
        }

        private int syncService;
        [SQLite.Net.Attributes.Column("SyncService")]
        public int SyncService
        {
            get { return syncService; }
            set
            {
                if (syncService == value)
                    return;

                syncService = value;
                IsModified = true;
            }
        }

        private JsonObject helpPreferencesJson;
        [SQLite.Net.Attributes.Column("Helps")]
        public string Helps
        {
            get
            {
                if (helpPreferencesJson != null)
                    return Utils.JsonToString(helpPreferencesJson);
                else
                    return "{}";
            }
            set
            {
                helpPreferencesJson = JsonObject.Parse(value);
                IsModified = true;                
            }
        }

        private int lastAppVer;
        [SQLite.Net.Attributes.Column("LastAppVer")]
        public int LastAppVer
        {
            get { return lastAppVer; }
            set
            {
                if (lastAppVer == value)
                    return;

                lastAppVer = value;
                IsModified = true;
            }
        }

        private int answerButtonPosition;
        [SQLite.Net.Attributes.Column("AnswerButtonPosition")]
        public int AnswerButtonPosition
        {
            get { return answerButtonPosition; }
            set
            {
                if (answerButtonPosition == value)
                    return;

                answerButtonPosition = value;
                IsModified = true;
            }
        }

        private bool isChangedSaveShortcutOpen;
        [SQLite.Net.Attributes.Column("IsChangedSaveShortcutOpen")]
        public bool IsChangedSaveShortcutOpen
        {
            get { return isChangedSaveShortcutOpen; }
            set
            {
                if (isChangedSaveShortcutOpen == value)
                    return;

                isChangedSaveShortcutOpen = value;
                IsModified = true;
            }
        }

        public static GeneralPreference GetDefaultPreference()
        {
            GeneralPreference userPrefs = new GeneralPreference();
            userPrefs.IsFirstTimeOpenApp = true;
            userPrefs.IsDeckListView = false;
            userPrefs.SortDeckBy = 0;
            userPrefs.IsReadNightMode = false;
            userPrefs.IsBlackNightModeLearning = false;
            userPrefs.ZoomLevel = MainPage.GetDefaultZoomLevel();
            userPrefs.IsHasInkDeckPreference = false;
            userPrefs.IsHasTextSynthDeckPreference = false;
            userPrefs.isAutoPlayTextSynth = false;
            userPrefs.IsFullSyncRequire = true;
            userPrefs.IsShowLeechActionOnce = false;
            userPrefs.IsCompressBackup = true;
            userPrefs.NumberOfBackups = 15;
            userPrefs.BackupsMinTime = 12;
            userPrefs.helpPreferencesJson = new JsonObject();

            userPrefs.syncService = 0;
            userPrefs.LastSyncTime = 0;
            userPrefs.IsSyncMedia = false;
            userPrefs.isSyncOnOpen = false;
            userPrefs.isSyncOnClose = true;

            userPrefs.isOneHandMode = false;
            userPrefs.isLeftHand = false;
            userPrefs.answerButtonPosition = 0;

            userPrefs.isChangedSaveShortcutOpen = false;

            userPrefs.lastAppVer = MainPage.APP_VER;

            return userPrefs;
        }

        public bool IsHelpAlreadyShown(string name)
        {
            return helpPreferencesJson.GetNamedBoolean(name, false);
        }

        public void SetHelpShown(string name, bool value)
        {
            helpPreferencesJson[name] = JsonValue.CreateBooleanValue(value);
            IsModified = true;
        }
    }
}
