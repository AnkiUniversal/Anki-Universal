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

var editableFieldName = '<h2 class="noneditable">name</h2>';
var editableFieldInputTinyMce = '<div contenteditable class="editable" id="fieldName" oncontextmenu="ContextMenuHandler(event)" tabindex="0" onfocusout="FocusOutHandler(this)">fieldcontent</div>';
var editableClass = 'editable';

var popUpHtml = '<span class="popuptext" id="firstFieldPopup">Duplicate</span>';

var toolbarWideScreenWidth = 'undo redo | removeformat | bold italic underline subscript superscript forecolor backcolor |  mediabutton recorderbutton link cloze | charmap  code';
var toolbarMediumScreenWidth = 'undo redo | removeformat bold italic underline subscript superscript forecolor backcolor mediagroupbutton link cloze';
var toolbarNarrowScreenWidth = 'undo removeformat stylebutton forecolor backcolor mediagroupbutton link cloze';

var toolbarWideScreenWidthTouch = 'undo redo | removeformat bold italic underline subscript superscript |  mediabutton recorderbutton link cloze ';
var toolbarMediumScreenWidthTouch = 'undo redo | removeformat bold italic underline subscript superscript mediabutton recorderbutton link cloze';
var toolbarNarrowScreenWidthTouch = 'undo removeformat bold italic underline mediabutton recorderbutton link cloze';

var tinymceInit = {
    selector: '.editable',
    body_class: 'editable',
    theme: 'modern',
    plugins: [
      'advlist autolink lists charmap hr autoresize',
      'visualblocks paste textcolor code colorpicker'
    ],
    content_css: '/html/fieldeditor.css',    
    menubar: false,    
    setup: function (editor) {        

        editor.addButton('mediagroupbutton', {
            type: 'menubutton',
            title: 'Add Media',
            icon: 'media',
            menu: [{                
                text: 'Add Image/Audio/Video',
                icon: 'media',
                onclick: function () {
                    //buttonNotifyApp is injected into javascript from C# at runtime
                    NotifyButtonClick('media');
                }
            }, {
                text: 'Voice Recorder',                
                image: '/html/tinymce/img/microphone.png',
                onclick: function () {
                    NotifyButtonClick('microphone');
                }
            }]
        });

        editor.addButton('stylebutton', {
            type: 'menubutton',
            title: 'Style',
            image: '/html/tinymce/img/aletter.png',
            menu: [{
                text: 'Bold',
                icon: 'bold',
                onclick: function () {
                    editor.execCommand('bold');
                }
            }, {
                text: 'Italic',
                icon: 'italic',
                onclick: function () {
                    editor.execCommand('italic');
                },
            }, {
                text: 'Underline',
                icon: 'underline',
                onclick: function () {
                    editor.execCommand('underline');
                },
            }, {
                text: 'Subscript',
                icon: 'subscript',
                onclick: function () {
                    editor.execCommand('subscript');
                },
            }, {
                text: 'Superscript',
                icon: 'superscript',
                onclick: function () {
                    editor.execCommand('superscript');
                },
            }
            ]
        });

        editor.addButton('link', {
            title: 'Link',
            icon: 'link',            
            onclick: function () {                
                NotifyButtonClick('link');
            }
        });

        editor.addButton('cloze', {
            title: 'Add Cloze',
            text: '[...]',
            onclick: function () {
                NotifyButtonClick('cloze');
            }
        });

        editor.addButton('mediabutton', {
            title: 'Add Media',
            icon: 'media',
            onclick: function () {
                NotifyButtonClick('media');
            }
        });

        editor.addButton('recorderbutton', {
            title: 'Voice Recoreder',
            image: '/html/tinymce/img/microphone.png',
            onclick: function () {
                NotifyButtonClick('microphone');
            }
        });

        editor.on("keydown", function (e) {
            return KeyPress(e);            
        });

        editor.on("init", function () {
            EditorInitEvent(editor);
        });        

    },    
    paste_data_images: false,
    paste_preprocess: function(plugin, args) {        
        if (args.content == '' || (args.content.indexOf("<img") != -1)) {
            PasteEventHandler();
            args.content = '';
        } 
    },
    elementpath: false,
    inline: true,    
    custom_undo_redo_levels: 10,
    statusbar: false,
    convert_fonts_to_spans: false,
    element_format: 'xhtml',
    object_resizing: false,
    keep_styles: false,
    remove_trailing_brs: true,
    forced_root_block: 'div',
    force_br_newlines: true,
    toolbar: toolbarNarrowScreenWidth
};

function EditorInitEvent(editor) {
    //Since tinymce will include a space if field has no content
    //we have to make sure to delete it.
    if (editor.getElement().innerHTML == '<div>&nbsp;</div>') {
        editor.getElement().innerHTML = '';
    }
}

function KeyPress(e) {
    isKeydownHandle = true;

    var evtobj = window.event ? event : e

    //For touch input, enter key does not function well in
    //tinymce so we have to handle it separately    
    if (isTouchInput && evtobj.keyCode == 13) {
        NotifyButtonClick('enter');
        return false;
    }

    if (evtobj.ctrlKey) {

        if (evtobj.keyCode == 189) {
            //Return false to avoid zomming
            return false;
        }

        //"+"
        if (evtobj.keyCode == 187 && !evtobj.shiftKey) {
            tinymce.activeEditor.execCommand('Subscript');
            return false;
        }

        //shift & "+"
        if (evtobj.keyCode == 187 && evtobj.shiftKey) {
            tinymce.activeEditor.execCommand('Superscript');
            return false;
        }

        //space
        if (evtobj.keyCode == 32) {
            tinymce.activeEditor.execCommand('RemoveFormat');
            return false;
        }

        //m
        if (evtobj.keyCode == 77) {
            NotifyButtonClick('media');
            return false;
        }

        //r
        if (evtobj.keyCode == 82) {
            NotifyButtonClick('microphone');
            return false;
        }

        //s
        if (evtobj.keyCode == 83) {
            NotifyButtonClick('save');
            return false;
        }

        //shift & c
        if (evtobj.keyCode == 67 && evtobj.shiftKey) {
            NotifyButtonClick('cloze');
            return false;
        }
    }

}

function GetNameHeaderField(id) {
    var fieldNames = document.getElementsByClassName('noneditable');
    var nameHtml = editableFieldName.replace('name', id);
    for (var i = 0; i < fieldNames.length; i++) {        
        if (fieldNames[i].outerHTML == nameHtml) {
            return fieldNames[i];
        }
    }
    
    throw 'Invalid header id';
}

function AddField(name) {
    InsertNewEditableField(name, "");
    CreateEditor(EDITABLE_FIELD_PREFIX + name);
}

function RemoveField(name) {

    var header = GetNameHeaderField(name);
    header.parentNode.removeChild(header); 

    var id = EDITABLE_FIELD_PREFIX + name;
    RemoveEditor(id);
    var field = document.getElementById(id);
    field.parentNode.removeChild(field);
}

function RenameField(oldName, newName) {    
    var header = GetNameHeaderField(oldName);
    header.innerText = newName;

    var oldId = EDITABLE_FIELD_PREFIX + oldName;
    RemoveEditor(oldId);

    var newId = EDITABLE_FIELD_PREFIX + newName;
    document.getElementById(oldId).id = newId;
    CreateEditor(newId);
}

function CreateEditor(id) {
    tinymce.EditorManager.execCommand('mceAddEditor', true, id);
}

function RemoveEditor(id) {
    tinymce.get(id).remove();
}

function MoveField(name, newOrder) {    

    var header = GetNameHeaderField(name);
    header.parentNode.removeChild(header);

    var id = EDITABLE_FIELD_PREFIX + name;
    var field = document.getElementById(id);
    field.parentNode.removeChild(field);
       
    if (newOrder == 0) {
        document.body.insertAdjacentElement('afterbegin', header);        
    }
    else {
        var fields = document.getElementsByClassName('editable');
        fields[newOrder - 1].insertAdjacentElement('afterEnd', header);
    }

    header.insertAdjacentElement('afterEnd', field);
}

function ShowPopup(name) {    
    var header = GetNameHeaderField(name);   
    header.insertAdjacentHTML('beforeEnd', popUpHtml);

    var popup = document.getElementById('firstFieldPopup');
    popup.classList.toggle('show');
}

function RemovePopup() {
    var popup = document.getElementById('firstFieldPopup');
    if (popup != null)
        popup.parentNode.removeChild(popup);
}