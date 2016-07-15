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

using AnkiU.Interfaces;
using AnkiU.Models;
using AnkiU.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.UI.Input.Inking;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AnkiU.Anki
{
    class InkToTextRecognizer : IUserInputString
    {
        private const int DEFAULT_RESULT_INDEX = 2;
        private const int TIME_STEP_WAIT_FOR_USER = 200;

        private InkCanvas ink;
        private IInkToTextUIControl uiElement;
        private FrameworkElement placeToShowResult;
        private TextRecognizeResultBuilder recognizeResultBuilder;

        public const int DEFAULT_SKIP_INDEX = 1;

        public InkToTextRecognizer(IInkToTextUIControl uiElement, InkCanvas ink, FrameworkElement placeToShowResult = null)
        {
            this.ink = ink;
            this.uiElement = uiElement;
            this.placeToShowResult = placeToShowResult;
            recognizeResultBuilder = new TextRecognizeResultBuilder(DEFAULT_RESULT_INDEX, DEFAULT_SKIP_INDEX);
            uiElement.InkToTextSelectedItemChanged += InkToWordComboBoxSelectionChangedHandler;
            uiElement.InkToTextSelectorLoaded += InkToWordComboBoxLoadedHandler;
        }

        public void Close()
        {
            uiElement.InkToTextSelectedItemChanged -= InkToWordComboBoxSelectionChangedHandler;
            uiElement.InkToTextSelectorLoaded -= InkToWordComboBoxLoadedHandler;
            recognizeResultBuilder.Clear();
        }

        private void InkToWordComboBoxSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            recognizeResultBuilder.OnSelectionChanged(sender as ComboBox);
        }

        private void InkToWordComboBoxLoadedHandler(object sender, RoutedEventArgs e)
        {
            //Make sure we only add if user chose to manually build result
            if (!uiElement.IsChooseTextAutomatically)
                recognizeResultBuilder.AddComboBox(sender as ComboBox);
        }

        public async Task<string> GetInput()
        {
            string result;
            if (uiElement.IsChooseTextAutomatically)
                result = await GetFirstRecognizeHandWritingAsync(ink);
            else
                result = await LetUserBuildAnswerFromRecognizeResults(ink);

            return result;
        }

        public async Task<string> GetFirstRecognizeHandWritingAsync(InkCanvas inkCanvas)
        {
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            StringBuilder buidler = new StringBuilder();
            if (currentStrokes.Count > 0)
            {

                var recognitionResults = await uiElement.InkRecognizerContainer.
                                               RecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.All);
                if (recognitionResults.Count > 0)
                {
                    foreach (var r in recognitionResults)
                    {
                        buidler.Append(" ");
                        buidler.Append(r.GetTextCandidates()[0]);
                    }
                }
            }
            return buidler.ToString();
        }

        public async Task<string> LetUserBuildAnswerFromRecognizeResults(InkCanvas inkCanvas)
        {
            recognizeResultBuilder.Clear();
            var resultsDict = await TryGetAllPossibleResults(inkCanvas);
            if (resultsDict == null)
                return "";

            uiElement.TextRecognizeResultView.DataContext = resultsDict.InkToWordViewModel;
            uiElement.ShowInkToTextResultUI(placeToShowResult);            

            await WaitUntilUserFinishBuildingResults();            

            var result = recognizeResultBuilder.GetResult();
            return result;
        }

        public async Task<InkToWordListViewModel> TryGetAllPossibleResults(InkCanvas inkCanvas)
        {
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (currentStrokes.Count > 0)
            {
                var recognitionResults = await uiElement.InkRecognizerContainer.
                                               RecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.All);
                List<InkToWordList> resultList = new List<InkToWordList>();
                if (recognitionResults.Count > 0)
                {
                    foreach (var r in recognitionResults)
                    {
                        List<InkToWord> strList = new List<InkToWord>();

                        //We allow user to skip some error strokes as space between word
                        //AnkiU will automatically drop all redundant whitespaces when comparing answers
                        strList.Add(new InkToWord(" "));

                        //We also allow user to skip some error strokes without leaving whitespace between words
                        strList.Add(new InkToWord(InkToWord.SKIP_WORD));

                        foreach (var c in r.GetTextCandidates())
                            strList.Add(new InkToWord(c));
                        InkToWordList viewModel = new Models.InkToWordList(strList);
                        resultList.Add(viewModel);
                    }
                    return new InkToWordListViewModel(resultList);
                }
            }
            return null;
        }

        /// <summary>
        /// Wait until user has finised building results.
        /// 
        /// If InkToTextUI is closed without flag uiElement.IsUserFinishedChoosingResults being raised
        /// then we assume user made some incorrect inputs and re-open the InkToTextUI.
        /// </summary>
        private async Task WaitUntilUserFinishBuildingResults()
        {                   
            while (!uiElement.IsUserFinishedChoosingResults)
            {
                await Task.Delay(TIME_STEP_WAIT_FOR_USER);

                //User closed the flyout without building results -> reopen it.
                if (uiElement.IsUserClosedInkToTextResultUI)
                    uiElement.ShowInkToTextResultUI(placeToShowResult);
            }
            uiElement.IsUserFinishedChoosingResults = false;
            uiElement.CloseInkToTextResultUI();
        }
    }
}
