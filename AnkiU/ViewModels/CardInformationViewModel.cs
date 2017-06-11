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
using AnkiU.Models;
using AnkiU.Pages;
using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AnkiU.ViewModels
{
    public class CardInformationViewModel
    {
        public static readonly Regex ClozeRegex = new Regex(@"(?i)<span class=cloze>(.*?)</span>", RegexOptions.Compiled);
        public static readonly Regex AnswerRegex = new Regex(@"(?is)(<hr.*?id=""?answer""?.*?/?>)", RegexOptions.Compiled);

        private Collection collection;

        public ObservableCollection<CardInformation> Cards { get; set; }

        public List<CardInformation> PreviousCards { get; set; }
        public List<CardInformation> NextCards { get; set; }
        
        private Comparison<CardInformation> CurrentSortMethod;
        private int sign = 1;

        public CardInformationViewModel(Collection collection, IEnumerable<long> cardId)
        {
            this.collection = collection;
            CurrentSortMethod = CompareQuestion;

            var temp = InitCardList(cardId);
            Cards = new ObservableCollection<CardInformation>(temp);
            PreviousCards = new List<CardInformation>();
            NextCards = new List<CardInformation>();
        }

        public void UpdateCardContentWithSameNoteId(long noteId)
        {
            UpdateCardContent(noteId, Cards);
            UpdateCardContent(noteId, PreviousCards);
            UpdateCardContent(noteId, NextCards);
        }

        private void UpdateCardContent(long noteId, IEnumerable<CardInformation> cards)
        {
            foreach(var card in cards)
            {
                if(card.NoteId == noteId)
                {
                    var toUpdate = InitCardInformationBlandText(collection, card.Id);
                    card.Question = toUpdate.Question;
                    card.Answer = toUpdate.Answer;
                    card.SortField = toUpdate.SortField;
                    card.TimeModified = toUpdate.TimeModified;                    
                }
            }
        }

        public void MoveToNextPage()
        {
            PreviousCards = new List<CardInformation>(Cards);
            PopulateCards(NextCards);
        }

        public void MoveToPreviousPage()
        {
            NextCards = new List<CardInformation>(Cards);
            PopulateCards(PreviousCards);
        }

        private void PopulateCards(IEnumerable<CardInformation> cards)
        {
            Cards.Clear();
            foreach (var c in cards)
                Cards.Add(c);
        }

        public void GetNextCards(IEnumerable<long> cardId)
        {
            NextCards = InitCardList(cardId);
            SortByViewColumn(NextCards);
        }

        public void GetPreviousCards(IEnumerable<long> cardId)
        {
            PreviousCards = InitCardList(cardId);
            SortByViewColumn(PreviousCards);
        }

        private void SortByViewColumn(List<CardInformation> cards)
        {
            cards.Sort(CurrentSortMethod);
        }

        private int CompareSortField(CardInformation compared, CardInformation comparer)
        {
            long comparedNumber;
            var isNumber = long.TryParse(compared.SortField, out comparedNumber);
            if (isNumber)
            {
                long comparerNumber;
                isNumber = long.TryParse(comparer.SortField, out comparerNumber);
                if(isNumber)
                    return sign * comparedNumber.CompareTo(comparerNumber);
            }
            return sign * compared.SortField.CompareTo(comparer.SortField);
        }

        private int CompareQuestion(CardInformation compared, CardInformation comparer)
        {
            return sign * compared.Question.CompareTo(comparer.Question);
        }

        private int CompareAnswer(CardInformation compared, CardInformation comparer)
        {
            return sign * compared.Answer.CompareTo(comparer.Answer);
        }

        private int CompareDue(CardInformation compared, CardInformation comparer)
        {
            return sign * compared.Due.CompareTo(comparer.Due);
        }

        private int CompareLapse(CardInformation compared, CardInformation comparer)
        {
            return sign * compared.Lapses.CompareTo(comparer.Lapses);
        }

        private int CompareTimeCreated(CardInformation compared, CardInformation comparer)
        {
            return sign * compared.Id.CompareTo(comparer.Id);
        }

        private int CompareTimeModified(CardInformation compared, CardInformation comparer)
        {
            return sign * compared.TimeModified.CompareTo(comparer.TimeModified);
        }

        private List<CardInformation> InitCardList(IEnumerable<long> cardId)
        {
            List<CardInformation> temp = new List<CardInformation>();
            foreach (var id in cardId)
            {
                CardInformation card = InitCardInformationBlandText(collection, id);
                temp.Add(card);
            }
            return temp;
        }

        private CardInformation InitCardInformationBlandText(Collection collection, long id)
        {
            Card c = collection.GetCard(id);
            Note n = c.LoadNote(false);

            var qA = c.GetQuestionAndAnswer(false, true);
            var question = ReviewPage.TypeAnswerRegex.Replace(qA["q"], "");
            question = CleanMakeUp(question);
            
            StringBuilder answerBuilder = new StringBuilder();
            var answer = GetPureAnswer(qA["a"]);
            //Question is usually much shorter than answer so we decide
            //to search for cloze/type or not based on it.
            //Only invalid cards would not have cloze/type on question side anyway.           
            if (ClozeRegex.IsMatch(qA["q"]))
                ExpandCloze(qA["a"], answerBuilder);
            if (ReviewPage.TypeAnswerRegex.IsMatch(qA["q"]))
            {
                ExpandType(n, qA["a"], answerBuilder);
                answer = ReviewPage.TypeAnswerRegex.Replace(answer, "");
            }
            answerBuilder.Append(answer);
            answer = CleanMakeUp(answerBuilder.ToString());            

            var dueStr = FindRealDueAndDueInString(c);            
            CardInformation card = new CardInformation(c, dueStr, question, answer, n.GetSFld(), n.TimeModified);
            return card;
        }

        public static string GetPureAnswer(string answer)
        {          
            var match = AnswerRegex.Match(answer);
            if (!match.Success)
                return answer;

            return answer.Substring(match.Index + match.Length).Trim();
        }

        private static void ExpandCloze(string ans, StringBuilder answer)
        {
            var matches = ClozeRegex.Matches(ans);            
            foreach (Match match in matches)
            {
                answer.Append(match.Groups[1].ToString());
                answer.Append("\n");
            }
        }

        private static void ExpandType(Note n, string ans, StringBuilder answer)
        {
            var matches = ReviewPage.TypeAnswerRegex.Matches(ans);
            foreach (Match match in matches)
            {
                //Diferent with ReviewPage our method here is much simpler
                //if there is cloze embedded in type then it won't match any fields,
                //and it should have already been catched by ExpandCloze
                string field = match.Groups[1].ToString();                
                foreach (var f in n.Model.GetNamedArray("flds"))
                {
                    string name = f.GetObject().GetNamedString("name");
                    if (name == field)
                    {
                        answer.Append(n.GetItem(name));
                        answer.Append("\n");
                        break;
                    }
                }
            }
        }

        private string CleanMakeUp(string htmlString)
        {
            //Different with python and java ver, we convert break and end div tags 
            //to \n before erasing all html tags
            htmlString = Utils.BreakPattern.Replace(htmlString, "\n");
            htmlString = Utils.StartDivPattern.Replace(htmlString, "\n");
            htmlString = Utils.EndDivPattern.Replace(htmlString, "\n");
            htmlString = Utils.StripHTMLKeepMediaName(htmlString);
            var temp = htmlString.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            htmlString = Utils.TrimAndJoinStringArray(temp);            
            return htmlString;
        }

        /// <summary>
        /// This function will change card c due to real due in long
        /// and return a string due in date-time format
        /// </summary>
        /// <param name="c">Card to change its due to real due</param>
        /// <returns>Due date in string</returns>
        private string FindRealDueAndDueInString(Card c)
        {
            long date;            
            if (c.OriginalDeckId != 0)
                return "Filtered";
            if (c.Queue == 1)
                date = c.Due;
            else if (c.Queue == 0 || c.Type == CardType.New)
            {
                if(c.Queue < 0)
                    return "(" + c.Due.ToString() + ")";
                else
                    return c.Due.ToString();
            }                
            else if (c.Queue == 2 || c.Queue == 3 || ((c.Type == CardType.Review) && (c.Queue < 0)))
                date = DateTimeOffset.Now.ToUnixTimeSeconds() + ((c.Due - collection.Sched.Today) * 86400);
            else
                return "";
            var temp = DateTimeOffset.FromUnixTimeSeconds(date).LocalDateTime.ToString().Split(' ')[0];
            if (c.Queue < 0)
                temp = "(" + temp + ")";
            c.Due = date;
            return temp;
        }

        public void SortWithDue(bool isReverse)
        {
            CurrentSortMethod = CompareDue;
            ReSortCurrentCards(isReverse);
        }

        public void SortWithLapse(bool isReverse)
        {
            CurrentSortMethod = CompareLapse;
            ReSortCurrentCards(isReverse);
        }

        public void SortWithSortField(bool isReverse)
        {
            CurrentSortMethod = CompareSortField;
            ReSortCurrentCards(isReverse);
        }

        public void SortWithQuestion(bool isReverse)
        {
            CurrentSortMethod = CompareQuestion;
            ReSortCurrentCards(isReverse);
        }

        public void SortWithAnswer(bool isReverse)
        {
            CurrentSortMethod = CompareAnswer;
            ReSortCurrentCards(isReverse);
        }

        public void SortWithTimeCreated(bool isReverse)
        {
            CurrentSortMethod = CompareTimeCreated;
            ReSortCurrentCards(isReverse);
        }

        public void SortWithTimeModified(bool isReverse)
        {
            CurrentSortMethod = CompareTimeModified;
            ReSortCurrentCards(isReverse);
        }

        private void ReSortCurrentCards(bool isReverse)
        {
            var temp = new List<CardInformation>(Cards);
            sign = isReverse ? -1 : 1;
            temp.Sort(CurrentSortMethod);
            PopulateCards(temp);
            NextCards.Sort(CurrentSortMethod);
            PreviousCards.Sort(CurrentSortMethod);
        }

        public bool CheckIfAllIsReviewCards(IEnumerable<CardInformation> cards)
        {
            foreach(var card in cards)
            {
                if (card.Type != CardType.Review || card.Queue != 2)
                    return false;
            }
            return true;
        }

        public void UpdateCardDueAfterReschedule(Collection collection, KeyValuePair<List<CardInformation>, List<long>> cards)
        {
            var cardList = collection.Database.QueryColumn<CardTable>
                           ("select due from cards where id in " + Utils.Ids2str(cards.Value.ToArray()));
            for (int i = 0; i < cardList.Count; i++)
            {
                cards.Key[i].Due = cardList[i].Due;
                var date = DateTimeOffset.Now.ToUnixTimeSeconds() + ((cardList[i].Due - collection.Sched.Today) * 86400);
                cards.Key[i].DueStr = DateTimeOffset.FromUnixTimeSeconds(date).LocalDateTime.ToString().Split(' ')[0];                     
            }
        }

        public void UpdateCardInformationDueAfterReset(Collection collection, KeyValuePair<List<CardInformation>, List<long>> cards)
        {
            var cardList = collection.Database.QueryColumn<CardTable>
                           ("select due from cards where id in " + Utils.Ids2str(cards.Value.ToArray()));
            for (int i = 0; i < cardList.Count; i++)
            {
                cards.Key[i].Due = cardList[i].Due;
                cards.Key[i].DueStr = cardList[i].Due.ToString();
                cards.Key[i].Queue = 0;
                cards.Key[i].Type = CardType.New;
                cards.Key[i].Interval = 0;
            }
        }

    }
}
