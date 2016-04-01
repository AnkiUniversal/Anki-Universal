using System;
using System.Collections.Generic;
using System.Text;

namespace AnkiU.AnkiCore
{
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
        DUEPRIORITY,
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

    public enum DeckSchema
    {
        VERSION = 11
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
}
