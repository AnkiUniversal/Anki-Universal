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

namespace AnkiU.AnkiCore
{
    class AnswerComparer
    {
        private string correctCardAnswer;
        private string userAnswerInput;

        private StringBuilder correctAnswer = new StringBuilder();
        private StringBuilder userAnswer = new StringBuilder();
        private StringBuilder correctAnswerType = new StringBuilder();
        private StringBuilder userAnswerType = new StringBuilder();

        private int correctIndex;
        private int userIndex;
        private bool isCorrect;
        private bool isCorrectOne;

        private delegate string AddType(string input);

        public AnswerComparer(string userAnswer, string correctAnswer)
        {
            this.correctCardAnswer =  correctAnswer;
            this.userAnswerInput = userAnswer;
        }

        public string GetResult()
        {
            isCorrectOne = false;
            bool isPreviousCorrect = false;
            for (correctIndex = 0, userIndex = 0; correctIndex < correctCardAnswer.Length && userIndex < userAnswerInput.Length; correctIndex++)
            {
                char user = Char.ToLower(correctCardAnswer[correctIndex]);
                char correct = Char.ToLower(userAnswerInput[userIndex]);
                isCorrect = user.Equals(correct);
                if (correctIndex == 0)
                    AppendToAnswerStyle();
                else                
                    AppendBody(isPreviousCorrect, isCorrect);                
                isPreviousCorrect = isCorrect;
                if (isCorrectOne)
                    userIndex++;
            }
            AppendTails(isPreviousCorrect);
            AppendMissedString();
            userAnswer.Append("<br>&darr;<br>");
            userAnswer.Append(correctAnswer);
            return "<div><code id=typeAns>" + userAnswer.ToString() + "</code></div>";
        }

        private void AppendBody(bool isPreviousCorrect, bool isCorrect)
        {
            if (isPreviousCorrect == isCorrect)
                AppendToAnswerStyle();
            else
            {
                if (isPreviousCorrect)
                    AppendStylesToResultAndClearAnswer(AddGoodType, AddGoodType);
                else
                    AppendStylesToResultAndClearAnswer(AddBadType, AddMissType);
                AppendToAnswerStyle();
            }
        }

        private void AppendMissedString()
        {
            AppendMissedCorrectAnswerIfNeeded();
            AppendMissedUserAnswerIfNeeded();
        }

        private void AppendTails(bool isPreviousCorrect)
        {
            if (isPreviousCorrect)
                AppendStyle(AddGoodType, AddGoodType);
            else
                AppendStyle(AddBadType, AddMissType);
        }

        private void AppendMissedUserAnswerIfNeeded()
        {
            if (!isCorrectOne)
            {
                userAnswer.Append(AddBadType(userAnswerInput));
                return;
            }

            if (userIndex < userAnswerInput.Length)
            {
                string missed = userAnswerInput.Substring(userIndex);
                userAnswer.Append(AddBadType(missed));
            }
        }

        private void AppendMissedCorrectAnswerIfNeeded()
        {
            if (correctIndex < correctCardAnswer.Length)
            {
                string missed = correctCardAnswer.Substring(correctIndex);
                correctAnswer.Append(AddMissType(missed));
            }
        }

        private void AppendStylesToResultAndClearAnswer(AddType funcUser, AddType funcCorrect)
        {
            AppendStyle(funcUser, funcCorrect);
            userAnswerType.Clear();
            correctAnswerType.Clear();
        }

        private void AppendStyle(AddType funcUser, AddType funcCorrect)
        {
            correctAnswer.Append(funcCorrect(correctAnswerType.ToString()));
            userAnswer.Append(funcUser(userAnswerType.ToString()));
        }

        private void AppendToAnswerStyle()
        {
            correctAnswerType.Append(correctCardAnswer[correctIndex]);
            if (isCorrect)
            {
                isCorrectOne = true;
                userAnswerType.Append(userAnswerInput[userIndex]);
            }
            else
            {
                if (!isCorrectOne)
                    userAnswerType.Append('-');
                else
                    userAnswerType.Append(userAnswerInput[userIndex]);
            }
        }

        private string AddGoodType(string str)
        {
            return "<span class=typeGood>" + System.Net.WebUtility.HtmlEncode(str) + "</span>";
        }
        private string AddBadType(string str)
        {
            return "<span class=typeBad>" + System.Net.WebUtility.HtmlEncode(str) + "</span>";
        }
        private string AddMissType(string str)
        {
            return "<span class=typeMissed>" + System.Net.WebUtility.HtmlEncode(str) + "</span>";
        }

    }
}
