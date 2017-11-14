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

document.addEventListener('keydown', KeyDownHandler);
document.addEventListener('select', TextSelectHandler);

var isTouchInput = false;

function SetTouchInput(value) {
    if (value == 'false')
        isTouchInput = false;
    else
        isTouchInput = true;
}

function ChangeDeckMediaFolder(address) {
    var cardstyle = document.getElementById('deckmediafolder');
    cardstyle.href = address;
}

function ChangeCardStyle(htmlText) {
    var cardstyle = document.getElementById('cardstyle');
    cardstyle.innerHTML = htmlText;
}

var textBox;

function ChangeCardContent(htmlText, cardClass) {
    //Reset user answer whether it has value or not
    userAnswer = '';
    //Reset selected text to avoid re-synth previous text
    selectedText = '';

    //Blur textBox if has to avoid cursor remain in answer side
    if (textBox != null && textBox != undefined) {
        //Keep the state of isFocusOnTextBox
        var state = isFocusOnTextBox;
        textBox.blur();
        isFocusOnTextBox = state;
    }

    var cardcontent = document.getElementById('cardcontent');

    while (cardcontent.firstChild) {
        cardcontent.removeChild(cardcontent.firstChild);
    }
    cardcontent.insertAdjacentHTML('afterbegin', htmlText); 

    if (cardClass != null)
        cardcontent.className = cardClass;
    
    if (isFocusOnTextBox && !isTouchInput) {
        textBox = document.getElementById('typeAns');
        if (textBox != null) {
            textBox.focus();
            textBox.click();
            textBox.value = "";
        }
    }
    else {
        cardcontent.focus();
        cardcontent.click();
    }
}

function ChangeReadMode(readMode) {
    if (readMode == 'day')
        document.body.className = 'day';
    else
        document.body.className = 'night';
}

function KeyDownHandler(event) {
    //notifyAppObj is injected into javascript from C# at runtime
    notifyAppObj.keyDownEventFire(event.keyCode);    
}

var audios;
var currentAudio = 0;
var videos;
var currentVideo = 0;

function PlayAllMedia() {
    GetAllMedia();
    ReplayAllMedia();
}

function GetAllMedia() {
    audios = document.getElementsByTagName('audio');
    videos = document.getElementsByTagName('video');
}

function ReplayAllMedia() {
    currentAudio = 0;
    currentVideo = 0;

    if (audios.length != 0) {
        PlayNextAudio();
        if (videos.length != 0)
            audios[videos.length - 1].addEventListener('ended', PlayNextVideo, false);
    }
    else if (videos.length != 0)
        PlayNextVideo();
}

function PlayNextVideo() {
    videos[currentVideo].removeEventListener('ended', PlayNextVideo, false);
    videos[currentVideo].play()
    currentVideo = currentVideo + 1;
    if (currentVideo < videos.length)
        videos[currentVideo - 1].addEventListener('ended', PlayNextVideo, false);
}

function PlayNextAudio(media, index) {
    audios[currentAudio].removeEventListener('ended', PlayNextAudio, false);
    audios[currentAudio].play()
    currentAudio = currentAudio + 1;
    if (currentAudio < audios.length)
        audios[currentAudio - 1].addEventListener('ended', PlayNextAudio, false);
}

var userAnswer = '';
var isFocusOnTextBox = false;

function OnTextBoxClick() {
    //Remove hooking event to C# to avoid conflicting with other 
    //keyboard commands
    RemoveKeyHandlerEvent();
    isFocusOnTextBox = true;
}


function RemoveKeyHandlerEvent() {
    document.removeEventListener('keydown', KeyDownHandler);
}

function TypeAnsKeyDown(event)
{
    //We still allow user to use enter keys to show answer 
    if (event.keyCode == 13) {
        AddKeyHandlerEvent();
        notifyAppObj.keyDownEventFire(event.keyCode);
    }
}

function OnTextBoxFocusOut() {
    isFocusOnTextBox = false;
    AddKeyHandlerEvent();
}

function AddKeyHandlerEvent() {    
    document.addEventListener('keydown', KeyDownHandler);
}

function GetUserInputString() {
    return document.getElementById('typeAns').value;
}

function ChangeBodyZoom(value) {
    document.body.style.zoom = value;
}

var selectedText = '';
function TextSelectHandler(event) {
    try {
        selectedText = window.getSelection().toString();
    }
    catch (err) {
        selectedText = '';
    }    
}

function GetSelectionText() {    
    return selectedText;    
}
