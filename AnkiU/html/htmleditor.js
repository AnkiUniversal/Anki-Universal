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

var isTinyMceInit = false;
var isKeydownHandle = false;
var isTouchInput = false;

//Use a prefix to avoid fields start with number cause problem
const EDITABLE_FIELD_PREFIX = "Field_";        
const EDITABLE_FIELD_PREFIX_LENGTH = 6;

document.addEventListener("keydown", keyDownTextField, false);

function keyDownTextField(e) {

    if (!isKeydownHandle) {
        var evtobj = window.event ? event : e;

        if (evtobj.ctrlKey) {
            if (evtobj.keyCode == 83)
                NotifyButtonClick('saveS');
            else if (evtobj.keyCode == 13)
                NotifyButtonClick('saveE');
            return false;
        }
    }
    isKeydownHandle = false;
}

function InsertLineBreak() {
    var br = '<br>';
    tinymce.activeEditor.selection.setContent(br);
    var content = tinymce.activeEditor.getContent({ format: 'raw' });
    var count = content.split(br).length;
    if (count <= 2) {
        var bm = tinymce.activeEditor.selection.getBookmark(2, false);
        tinymce.activeEditor.selection.setContent(br);
        tinymce.activeEditor.selection.moveToBookmark(bm);
    }
}

function InsertCloze(count) {
    InsertTinyMceContent('{{c' + count + '::');    
    var bm = tinymce.activeEditor.selection.getBookmark(2, false);
    InsertTinyMceContent('}}');    
    tinymce.activeEditor.selection.moveToBookmark(bm);
    NotifyContentChanged(tinymce.activeEditor.id);
}

function InsertIntoTinymce(text) {
    InsertTinyMceContent(text);    
    NotifyContentChanged(tinymce.activeEditor.id);
}

function InsertTinyMceContent(text) {
    try {
        tinymce.activeEditor.execCommand('mceInsertContent', false, text);
    }
    catch (error) {
    }
}

function NotifyButtonClick(buttonName) {
    var id = tinymce.activeEditor.id;
    buttonNotify.clickEventFire(buttonName);
}

function InitRichTextEditor() {    
    tinymce.init(tinymceInit);

    editableFieldNotify.editableFieldStateChangedEventFire('EditorReady');
    isTinyMceInit = true;    
}

function PasteEventHandler() {
    editableFieldNotify.editableFieldPasteEventFire();
    return false;
}

function FocusOutHandler(obj) {
    var id = obj.id;
    NotifyContentChanged(id);
}

function ForceNotifyContentChanged() {
    if (tinymce.activeEditor == null)
        return;

    var id = tinymce.activeEditor.id;
    NotifyContentChanged(id);
}

function NotifyContentChanged(id) {
    var html = GetHtmlContent(id);
    var name = id.substring(EDITABLE_FIELD_PREFIX_LENGTH);
    editableFieldNotify.editableFieldTextChangedEventFire(name, html);
}

function GetHtmlContent(id) {
    if (isTinyMceInit)        
        return tinymce.get(id).getContent({ format: 'html' });
    else
        return "";
}

function PopulateAllEditableField() {
    var length = arguments.length,
        insertHtml = '',
        fieldName,
        fieldInput;

    for (var i = 0; i < length;) {
        fieldName = editableFieldName.replace('name', arguments[i]);
        fieldInput = editableFieldInputTinyMce.replace('fieldName', EDITABLE_FIELD_PREFIX + arguments[i]);
        fieldInput = fieldInput.replace('fieldcontent', arguments[i + 1]);
        insertHtml += '<div class="outline">' + fieldName + fieldInput + '</div> ';

        i = i + 2;
    }

    document.body.insertAdjacentHTML('beforeend', insertHtml);
}

function InsertNewEditableField(name, content) {

    var fieldName = editableFieldName.replace('name', name);
    var fieldInput = editableFieldInputTinyMce.replace('fieldName', EDITABLE_FIELD_PREFIX + name);
    fieldInput = fieldInput.replace('fieldcontent', content);

    document.body.insertAdjacentHTML('beforeend', '<div class="outline">' + fieldName + fieldInput + '</div> ');
}

function ClearBody() {
    document.body.innerHTML = '';
}

function ChangeAllEditableFieldContent() {
    for (var i = 0; i < tinymce.editors.length; i++) {
        tinymce.editors[i].setContent(arguments[i]);
    }
}

function ChangeMediaFolder(address) {
    folder = document.getElementById('mediafolder');
    folder.href = address;
}

function ChangeReadMode(readMode) {
    if (readMode == 'day')
        document.body.className = 'day';
    else
        document.body.className = 'night';
}

function ChangeTinymceToolBarWidth(size) {
    if (isTouchInput) {
        if (size === 'narrow')
            tinymceInit['toolbar'] = toolbarNarrowScreenWidthTouch;
        else if (size === 'medium')
            tinymceInit['toolbar'] = toolbarMediumScreenWidthTouch;
        else
            tinymceInit['toolbar'] = toolbarWideScreenWidthTouch;
    }
    else {
        if (size === 'narrow')
            tinymceInit['toolbar'] = toolbarNarrowScreenWidth;
        else if (size === 'medium')
            tinymceInit['toolbar'] = toolbarMediumScreenWidth;
        else
            tinymceInit['toolbar'] = toolbarWideScreenWidth;
    }
}

function LoadNewToolBarWidth(size) {
    ChangeTinymceToolBarWidth(size);
    tinymce.remove();
    tinymce.init(tinymceInit);
}

function ContextMenuHandler(event) {
    event.preventDefault();
    var m = GetMousePosition(event);
    var object = [m.x, m.y];
    editableFieldNotify.editableFieldContextMenuEventFire(object);
    return false;
}

function GetMousePosition(e) {
    e = e || window.event;
    var position = {
        'x': e.clientX,
        'y': e.clientY
    }
    return position;
}

function FocusOn(name) {    
    var id = EDITABLE_FIELD_PREFIX + name;    
    tinymce.get(id).focus();    
}

function HideEditor() {
    tinymce.activeEditor.hide();
}

function ShowEditor() {
    tinymce.activeEditor.show();
}

function IsTouchInput(value) {
    if (value == 'false')
        isTouchInput = false;
    else
        isTouchInput = true;
}

function RemoveAllEditor() {
    tinymce.remove();
}

var selectedRange;

function NotifyChangeForeColor() {
    selectedRange = tinymce.activeEditor.selection.getRng();
    NotifyButtonClick('forecolor');
}

function NotifyChangeBackColor() {
    selectedRange = tinymce.activeEditor.selection.getRng();
    NotifyButtonClick('backcolor');
}

function ChangeForeColor(color) {
    try {
        RestoreSelection();
        tinymce.activeEditor.execCommand('ForeColor', false, color);
    }
    catch (err) {
    }
}

function ChangeBackColor(color) {
    try {
        RestoreSelection();
        tinymce.activeEditor.execCommand('HiliteColor', false, color);
    }
    catch (err) {
    }
}

function RestoreSelection() {
        if (selectedRange != undefined && selectedRange != null)
            tinymce.activeEditor.selection.setRng(selectedRange);
}