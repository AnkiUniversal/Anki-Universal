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

namespace AnkiU.AnkiCore
{
    public static class Constant
    {
        public const int SCHEMA_VERSION = 11;
        public const int DEFAULTDECK_ID = 1;
        public const string DEFAULT_DECKNAME = "Default";
        public const string BACKUP_FOLDER_NAME = "Backup";
        public const string BACKUP_AFFIX = ".au.backup";        

        public const string ANKIROOT_SYNC_FOLDER = "Anki Universal";

        public const string COLLECTION_NAME = "collection.anki2";
        public const string COLLECTION_NAME_ZIP = COLLECTION_NAME + ".zip";
        public const string ANKI_COL_SYNC_PATH = ANKIROOT_SYNC_FOLDER + "/" + COLLECTION_NAME;

        public const string MEDIA_DB_NAME = "collection.media.au.db2";
        public const string MEDIA_DB_SYNC_PATH = ANKIROOT_SYNC_FOLDER + "/" + MEDIA_DB_NAME;

        public const string USER_PREF = "Prefs.db";        
        public const string USER_PREF_SYNC_PATH = ANKIROOT_SYNC_FOLDER + "/" + USER_PREF;

        public const string DEFAULT_DECK_IMAGE_FOLDER_NAME = "DeckImages";
        public const string DEFAULT_DECK_IMAGE_FOLDER_SYNC_PATH = ANKIROOT_SYNC_FOLDER + "/" + DEFAULT_DECK_IMAGE_FOLDER_NAME;
    }

    public enum ReviewType
    {
        DISTRIBUTE,
        LAST,
        FIRST
    }

    public enum NewCardInsertOrder
    {
        RANDOM,
        DUE
    }

    public enum RemovalType
    {
        CARD,
        NOTE,
        DECK
    }

    public enum CountDisplay
    {
        ANSWERED,
        REMAINING
    }

    public enum MediaLog
    {
        ADD,
        REMOVE
    }

    public enum DynamicDeckOrder
    {
        OLDEST,
        RANDOM,
        SMALLINT,
        BIGINT,
        LAPSES,
        ADDED,
        DUE,
        REVADDED,
        DUEPRIORITY
    }

    public enum DynamicSize
    { 
        MAX = 99999
    }

    public enum ModelType
    {
        STD,
        CLOZE
    }

    public static class Syncing
    {
        public const int ZIP_SIZE = (int)(2.5 * 1024 * 1024);
        public const int ZIP_COUNT = 25;
        public const string BASE = "https://ankiweb.net/";
        public const string MEDIA_BASE = "https://msync.ankiweb.net/";
        public const int VERSION = 8;
    }

    public static class HttpSite
    {
        public const string HELP = "http://ankisrs.net/docs/manual.html";
    }

    public enum ConfigPresets
    {
        Default = 1,
        TagOnLeech = 2,
        ShortInterval = 3,
        LongInterval = 4,
        //This always have to be the last member
        //as it's used to determine the range of ConfigPresets
        DueOnly = 5
    }
}
