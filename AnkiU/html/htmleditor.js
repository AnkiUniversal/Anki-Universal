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
        var evtobj = window.event ? event : e

        if (evtobj.ctrlKey && evtobj.keyCode == 83) {
            NotifyButtonClick('save');
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
    tinymce.activeEditor.execCommand('mceInsertContent', false, '{{c' + count + '::');
    var bm = tinymce.activeEditor.selection.getBookmark(2, false);
    tinymce.activeEditor.execCommand('mceInsertContent', false, '}}');
    tinymce.activeEditor.selection.moveToBookmark(bm);
    NotifyContentChanged(tinymce.activeEditor.id);
}

function InsertIntoTinymce(text) {
    tinymce.activeEditor.execCommand('mceInsertContent', false, text);
    NotifyContentChanged(tinymce.activeEditor.id);
}

function NotifyButtonClick(buttonName) {
    var id = tinymce.activeEditor.id;
    buttonNotify.clickEventFire(buttonName);
    //Immediately get focus again to avoid race event
    //when inserting html back to editor
    tinymce.get(id).focus();
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
    for (var i = 0; i < arguments.length;) {
        InsertNewEditableField(arguments[i], arguments[i + 1])
        i = i + 2;
    }
}

function InsertNewEditableField(name, content) {

    var fieldName = editableFieldName.replace('name', name);
    var fieldInput = editableFieldInputTinyMce.replace('fieldName', EDITABLE_FIELD_PREFIX + name);
    fieldInput = fieldInput.replace('fieldcontent', content);

    document.body.insertAdjacentHTML('beforeend', '<div> ' + fieldName + fieldInput + '</div> ');
}

function ClearBody() {
    document.body.innerHTML = '';
}

function ChangeAllEditableFieldContent() {
    var fields = document.getElementsByClassName(editableClass);
    for (var i = 0; i < fields.length; i++) {
        fields[i].innerHTML = arguments[i];
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