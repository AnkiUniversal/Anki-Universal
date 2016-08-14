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

var editableFieldName = '<h2>name</h2>';
var editableFieldInputTinyMce = '<div contenteditable class="card" id="fieldName" oncontextmenu="ContextMenuHandler(event)" tabindex="0" onfocusout="FocusOutHandler(this)">fieldcontent</div>';
var editableClass = 'card';

var toolbarWideScreenWidth = 'undo redo removeformat | styleselect fontselect fontsizeselect | stylebutton forecolor backcolor | addField addTypeField addClozeField miscbuttonshort table';
var toolbarMediumScreenWidth = ['undo redo removeformat | styleselect fontselect fontsizeselect',
                                'stylebutton forecolor backcolor | addField addTypeField addClozeField miscbuttonshort table'];
var toolbarNarrowScreenWidth = ['undo styleselect fontselect fontsizeselect',
                                'removeformat stylebutton forecolor backcolor addField miscbutton table'];

var toolbarWideScreenWidthTouch = 'undo redo removeformat | bold italic underline | addField addTypeField addClozeField | CSS code hr';
var toolbarMediumScreenWidthTouch = 'undo redo removeformat | bold italic underline | addField addTypeField addClozeField | CSS code hr ';
var toolbarNarrowScreenWidthTouch = 'undo removeformat bold italic underline addField addTypeField addClozeField CSS';

var tinymceInit = {
    selector: '.card',
    body_class: 'card',
    content_css: '/html/templateeditor.css',
    theme: 'modern',
    plugins: [
      'advlist autolink lists charmap hr autoresize colorpicker',
      'visualblocks textcolor code textpattern table paste'
    ],    
    menubar: false,
    setup: function (editor) {

        editor.on("keydown", function (e) {
            return KeyPress(e);
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

        editor.addButton('miscbutton', {
            type: 'menubutton',
            text: 'Misc',
            menu: [{
                text: 'Add type field',
                image: '/html/tinymce/img/input.png',
                onclick: function () {
                    AddTypeField();
                }
            }, {
                text: 'Add Cloze Field',
                image: '/html/tinymce/img/cloze.png',
                onclick: function () {
                    NotifyButtonClick('addCloze');
                }
            }, {
                text: 'Horizontal Line',
                icon: 'hr',
                onclick: function () {
                    editor.execCommand('InsertHorizontalRule');
                }
            }, {
                text: 'Code',
                icon: 'code',
                onclick: function () {
                    editor.execCommand('mceCodeEditor');
                },
            }, {
                text: 'CSS Code',
                icon: 'code',
                onclick: function () {
                    NotifyButtonClick('stylecode');
                },
            }]
        });

        editor.addButton('miscbuttonshort', {
            type: 'menubutton',
            text: 'Misc',
            menu: [{
                text: 'Horizontal Line',
                icon: 'hr',
                onclick: function () {
                    editor.execCommand('InsertHorizontalRule');
                }
            }, {
                text: 'Code',
                icon: 'code',
                onclick: function () {
                    editor.execCommand('mceCodeEditor');
                },
            }, {
                text: 'CSS Code',
                icon: 'code',
                onclick: function () {
                    NotifyButtonClick('stylecode');
                },
            }]
        });   

        editor.addButton('addField', {
            title: 'Add Field',
            text: '{{...}}',
            onclick: function () {                
                NotifyButtonClick('addField');
            },
        });

        editor.addButton('addTypeField', {
            title: 'Add Type Field',
            image: '/html/tinymce/img/input.png',
            onclick: function () {
                AddTypeField();
            },
        });

        editor.addButton('addClozeField', {
            title: 'Add Cloze Field',
            text: '[...]',
            onclick: function () {
                NotifyButtonClick('addCloze');
            },
        });

        editor.addButton('CSS', {
            title: 'Edit CSS',
            text: 'CSS',
            onclick: function () {
                NotifyButtonClick('stylecode');
            },
        });

    },
    style_formats: [
    {title: 'Alignment', items: [
      {title: 'Left', icon: 'alignleft', format: 'alignleft'},
      {title: 'Center', icon: 'aligncenter', format: 'aligncenter'},
      {title: 'Right', icon: 'alignright', format: 'alignright'},
      {title: 'Justify', icon: 'alignjustify', format: 'alignjustify'}
    ]},
    {title: 'Blocks', items: [
      {title: 'Paragraph', format: 'p'},
      {title: 'Div', format: 'div'}
    ]}    
    ],
    paste_data_images: false,
    paste_preprocess: function (plugin, args) {
        if (args.content == '' || (args.content.indexOf("<img") != -1)) {
            PasteEventHandler();
            args.content = '';
        }
    },
    code_dialog_height: 300,
    code_dialog_width: 300,
    fontsize_formats: '8pt 10pt 12pt 14pt 16pt 18pt 20pt 24pt 36pt 48pt 60pt',    
    elementpath: false,    
    inline: true,
    custom_undo_redo_levels: 10,
    statusbar: false,
    convert_fonts_to_spans: false,
    element_format: 'xhtml',
    object_resizing: true,
    keep_styles: false,
    remove_trailing_brs: true,
    forced_root_block: 'div',
    force_br_newlines: true,
    toolbar: toolbarNarrowScreenWidth,
    font_formats: 'Andale Mono=andale mono,times;' +
                  'Arial=arial,helvetica,sans-serif;' +
                  'Arial Black=arial black,avant garde;' +
                  'Book Antiqua=book antiqua,palatino;' +
                  'Comic Sans MS=comic sans ms,sans-serif;' +
                  'Courier New=courier new,courier;' +
                  'Georgia=georgia,palatino;' +
                  'Helvetica=helvetica;' +
                  'Impact=impact,chicago;' +
                  'Fira Sans=fira sans,sans-serif;' +
                  'Meiryo=meiryo, MS Mincho;' +
                  'Symbol=symbol;' +
                  'Tahoma=tahoma,arial,helvetica,sans-serif;' +
                  'Terminal=terminal,monaco;' +
                  'Times New Roman=times new roman,times;' +
                  'Trebuchet MS=trebuchet ms,geneva;' +
                  'Verdana=verdana,geneva;'
};

function AddTypeField() {
    var fields = document.getElementsByClassName("card");
    if (tinymce.activeEditor.id == fields[0].id)
        NotifyButtonClick('addTypeFront');
    else
        NotifyButtonClick('addTypeBack');
}

function ChangeTemplateStyle(htmlText) {
    cardstyle = document.getElementById('cardstyle');
    cardstyle.innerHTML = htmlText;
}

function KeyPress(e) {
    isKeydownHandle = true;

    var evtobj = window.event ? event : e

    //Updated (13_08_2016): this function is no longer needed on new Windows Phone 10 Vers
    //But we still keeps it here if it appears again in future updates
    //For touch input, enter key does not function well in
    //tinymce so we have to handle it separately    
    //if (isTouchInput && evtobj.keyCode == 13) {
    //    NotifyButtonClick('enter');
    //    return false;
    //}

    if (evtobj.ctrlKey) {
        if (evtobj.keyCode == 69) {
            tinymce.activeEditor.execCommand('JustifyCenter');
            return false;
        }

        if (evtobj.keyCode == 82) {
            tinymce.activeEditor.execCommand('JustifyRight');
            return false;
        }

        if (evtobj.keyCode == 76) {
            tinymce.activeEditor.execCommand('JustifyLeft');
            return false;
        }

        if (evtobj.keyCode == 74) {
            tinymce.activeEditor.execCommand('JustifyFull');
            return false;
        }

        if (evtobj.keyCode == 189) {
            //Return false to avoid zomming
            return false;
        }

        if (evtobj.keyCode == 187 && !evtobj.shiftKey) {
            tinymce.activeEditor.execCommand('Subscript');
            return false;
        }

        if (evtobj.keyCode == 187 && evtobj.shiftKey) {
            tinymce.activeEditor.execCommand('Superscript');
            return false;
        }

        if (evtobj.keyCode == 32) {
            tinymce.activeEditor.execCommand('RemoveFormat');
            return false;
        }

        if (evtobj.keyCode == 83) {
            NotifyButtonClick('save');
            return false;
        }
    }
    return true;
}

function InsertAfterField(name, html) {
    var id = EDITABLE_FIELD_PREFIX + name;
    var ele = document.getElementById(id);
    ele.insertAdjacentHTML('afterend', html);
}

function ChangeCodeDialogWidthHeight(width, height) {
    tinymceInit['code_dialog_width'] = parseInt(width, 10);
    tinymceInit['code_dialog_height'] = parseInt(height, 10);
}

function ChangeEditableZoom(contentZoom, heightAdapt) {
    var fields = document.getElementsByClassName("card");
    
    for (var i = 0; i < fields.length; i++) {
        fields[i].style.zoom = contentZoom;
        if (heightAdapt != null)
            fields[i].style.maxHeight = heightAdapt + 'px';
    }
}

function InsertIntoAllFields(html) {
    var fields = document.getElementsByClassName("card");
    for (var i = 0; i < fields.length; i++) {
        var editor = tinymce.get(fields[i].id);
        editor.execCommand('mceInsertContent', false, html);
        NotifyContentChanged(editor.id);
    }
}