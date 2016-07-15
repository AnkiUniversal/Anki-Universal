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

namespace AnkiU.AnkiCore
{
    public class Stats
    {
        [SQLite.Net.Attributes.Table("cards")]
        public class DueForeCast
        {
            [SQLite.Net.Attributes.Column("day")]
            public int Day { get; set; }
            [SQLite.Net.Attributes.Column("mature")]
            public int Mature { get; set; }
            [SQLite.Net.Attributes.Column("young")]
            public int Young { get; set; }
        }

        [SQLite.Net.Attributes.Table("revlog")]
        public class TodayStats
        {
            [SQLite.Net.Attributes.Column("cardsToday")]
            public int CardsToday { get; set; }
            [SQLite.Net.Attributes.Column("time")]
            public int Time { get; set; }
            [SQLite.Net.Attributes.Column("failed")]
            public int Failed { get; set; }
            [SQLite.Net.Attributes.Column("learning")]
            public int Learning { get; set; }
            [SQLite.Net.Attributes.Column("review")]
            public int Review { get; set; }
            [SQLite.Net.Attributes.Column("relearn")]
            public int Relearn { get; set; }
            [SQLite.Net.Attributes.Column("filter")]
            public int Filter { get; set; }
        }

        [SQLite.Net.Attributes.Table("revlog")]
        public class ReviewData
        {
            [SQLite.Net.Attributes.Column("day")]
            public int Day { get; set; }
            [SQLite.Net.Attributes.Column("learn")]
            public int Learn { get; set; }
            [SQLite.Net.Attributes.Column("young")]
            public int Young { get; set; }
            [SQLite.Net.Attributes.Column("mature")]
            public int Mature { get; set; }
            [SQLite.Net.Attributes.Column("lapse")]
            public int Relearn { get; set; }
            [SQLite.Net.Attributes.Column("cram")]
            public int Cram { get; set; }

            [SQLite.Net.Attributes.Column("learnTime")]
            public int LearnTime { get; set; }
            [SQLite.Net.Attributes.Column("youngTime")]
            public int YoungTime { get; set; }
            [SQLite.Net.Attributes.Column("matureTime")]
            public int MatureTime { get; set; }
            [SQLite.Net.Attributes.Column("lapseTime")]
            public int RelearnTime { get; set; }
            [SQLite.Net.Attributes.Column("cramTime")]
            public int CramTime { get; set; }
        }

        [SQLite.Net.Attributes.Table("cards")]
        public class CardStatesData
        {
            [SQLite.Net.Attributes.Column("totalCards")]
            public long TotalCards { get; set; }

            [SQLite.Net.Attributes.Column("totalNotes")]
            public long TotalNotes { get; set; }

            [SQLite.Net.Attributes.Column("young")]
            public long TotalYoung { get; set; }

            [SQLite.Net.Attributes.Column("mature")]
            public long TotalMature { get; set; }

            [SQLite.Net.Attributes.Column("new")]
            public long TotalNew { get; set; }

            [SQLite.Net.Attributes.Column("suspend")]
            public long TotalSuspendAndBury { get; set; }
        }

        public enum TimeRangeType
        {
            MONTH,
            YEAR,
            LIFE
        }

        public struct AxisDataType
        {
            public int? Start { get; set; }
            public int? End { get; set; }
            public int Chunk { get; set; }
        }

        private static Dictionary<TimeRangeType, AxisDataType> axisDict = new Dictionary<TimeRangeType, AxisDataType>();
        private static Dictionary<TimeRangeType, string> affixDict = new Dictionary<TimeRangeType, string>();
        private static Dictionary<TimeRangeType, string> timeNameDict = new Dictionary<TimeRangeType, string>();
        public static Dictionary<TimeRangeType, AxisDataType> AxisDict { get { return axisDict; } }
        public static Dictionary<TimeRangeType, string> AffixDict { get { return affixDict; } }
        public static Dictionary<TimeRangeType, string> TimeNameDict { get { return timeNameDict; } }
        public static TimeRangeType TimeType { get; set; }

        public static bool IsWholeCollection { get; set; }

        static Stats()
        {
            axisDict.Add(TimeRangeType.MONTH, new AxisDataType() { Start = 0, End = 31, Chunk = 1 });
            axisDict.Add(TimeRangeType.YEAR, new AxisDataType() { Start = 0, End = 52, Chunk = 7 });
            axisDict.Add(TimeRangeType.LIFE, new AxisDataType() { Start = 0, End = null, Chunk = 30 });

            affixDict.Add(TimeRangeType.MONTH, "d");
            affixDict.Add(TimeRangeType.YEAR, "w");
            affixDict.Add(TimeRangeType.LIFE, "mo");

            timeNameDict.Add(TimeRangeType.MONTH, "Days");
            timeNameDict.Add(TimeRangeType.YEAR, "Weeks");
            timeNameDict.Add(TimeRangeType.LIFE, "Months");

            IsWholeCollection = false;
            TimeType = TimeRangeType.MONTH;
        }        

        public static TodayStats GetTodayStats(Collection collection)
        {
            StringBuilder lim = new StringBuilder(RevlogLimit(collection));
            if (lim.Length > 0)
                lim.Insert(0, " and ");

            string query = "select count() as cardsToday, " +
                           "sum(time)/1000 as time, " +
                           "sum(case when ease = 1 then 1 else 0 end) as failed, " + 
                           "sum(case when type = 0 then 1 else 0 end) as learning, " + 
                           "sum(case when type = 1 then 1 else 0 end) as review, " + 
                           "sum(case when type = 2 then 1 else 0 end) as relearn, " + 
                           "sum(case when type = 3 then 1 else 0 end) as filter " + 
                           "from revlog where id > " + ((collection.Sched.DayCutoff - 86400) * 1000) + " " + lim.ToString();

            var todayStats = collection.Database.QueryColumn<TodayStats>(query);
            return todayStats[0];
        }

        public static List<DueForeCast> GetDueForeCast(Collection collection)
        {
            StringBuilder lim = new StringBuilder();
            var axis = axisDict[TimeType];
            if (axis.Start != null)
            {
                lim.Append(" and due - " + collection.Sched.Today + " >= ");
                lim.Append(axis.Start);
            }

            if (axis.End != null)
            {
                lim.Append(" and day < ");
                lim.Append(axis.End);
            }

            string query = String.Format("select(due - {0})/{1} as day," +
                " sum(case when ivl < 21 then 1 else 0 end) as young, " +
                "sum(case when ivl >= 21 then 1 else 0 end) as mature " +
                "from cards where did in {2} and queue in (2,3) {3} group by day order by day", 
                collection.Sched.Today, axis.Chunk, Limit(collection), lim.ToString());

            return collection.Database.QueryColumn<DueForeCast>(query);            
        }

        public static List<ReviewData> GetReviewCountAndTime(Collection collection)
        {
            var axis = axisDict[TimeType];

            List<string> limitList = new List<string>();
            if (axis.End != null)            
                limitList.Add("id > " + ((collection.Sched.DayCutoff - ((axis.End + 1) * axis.Chunk * 86400)) * 1000));
            
            string lim = RevlogLimit(collection);
            if (lim.Length > 0)            
                limitList.Add(lim);            
            if (limitList.Count > 0)            
                lim = "WHERE " + String.Join(" AND ", limitList);                            
            else            
                lim = "";

            List<double[]> list = new List<double[]>();
            
            String query = "SELECT (cast((id/1000 - " + collection.Sched.DayCutoff + ") / 86400.0 AS INT))/"
                    + axis.Chunk + " as day, " 
                    + "sum(CASE WHEN type = 0 THEN 1 ELSE 0 END) as learn, " 
                    + "sum(CASE WHEN type = 1 AND lastIvl < 21 THEN 1 ELSE 0 END) as young, " 
                    + "sum(CASE WHEN type = 1 AND lastIvl >= 21 THEN 1 ELSE 0 END) as mature, "
                    + "sum(CASE WHEN type = 2 THEN 1 ELSE 0 END) as lapse, " 
                    + "sum(CASE WHEN type = 3 THEN 1 ELSE 0 END) as cram, " 
                    + "sum(case when type = 0 then time / 1000.0 else 0 end) / 3600.0 as learnTime, "
                    + "sum(case when type = 1 and lastIvl < 21 then time / 1000.0 else 0 end) / 3600.0 as youngTime, "
                    + "sum(case when type = 1 and lastIvl >= 21 then time / 1000.0 else 0 end) / 3600.0 as matureTime, "
                    + "sum(case when type = 2 then time / 1000.0 else 0 end) / 3600.0 as lapseTime, " 
                    + "sum(case when type = 3 then time / 1000.0 else 0 end) / 3600.0 as cramTime "
                    + " FROM revlog " + lim + " GROUP BY day ORDER BY day";
            var results = collection.Database.QueryColumn<ReviewData>(query);
            return results;
        }

        public static CardStatesData GetCardStates(Collection collection)
        {
            string limit = Limit(collection);
            var totalCardsAndNotes = collection.Database.QueryColumn<CardStatesData>
                                    ("select count(id) as totalCards, count(distinct nid) as totalNotes from cards where did in " + limit)[0];

            var allCardStates = collection.Database.QueryColumn<CardStatesData>
                                ("select sum(case when queue = 2 and ivl >= 21 then 1 else 0 end) as mature, " 
                                + "sum(case when queue in (1, 3) or (queue = 2 and ivl < 21) then 1 else 0 end) as young, " 
                                + "sum(case when queue = 0 then 1 else 0 end) as new, " 
                                + "sum(case when queue < 0 then 1 else 0 end) as suspend "
                                + "from cards where did in " + limit)[0];

            allCardStates.TotalCards = totalCardsAndNotes.TotalCards;
            allCardStates.TotalNotes = totalCardsAndNotes.TotalNotes;
            return allCardStates;
        }


        public static string RevlogLimit(Collection collection)
        {
            if (IsWholeCollection)
                return "";
            return String.Format("cid in (select id from cards where did in {0})",
                                    Utils.Ids2str(collection.Deck.Active().ToArray()));
        }

        private static string Limit(Collection collection)
        {
            if (IsWholeCollection)
            {
                List<long> ids = new List<long>();
                foreach (JsonObject d in collection.Deck.All())                
                   ids.Add((long)d.GetNamedNumber("id"));                    
                
                return Utils.Ids2str(ids);
            }
            else
            {
                return collection.Sched.DeckLimit();
            }
        }

    }
}
