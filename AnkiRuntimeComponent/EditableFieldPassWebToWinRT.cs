using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace AnkiRuntimeComponent
{
    public delegate void EditableFieldChangedHandler(string fieldName, string html);
    public delegate void EditableFieldStateChangedEventHandler(string state);
    public delegate void EditableFieldRoutedEventHandler();
    public delegate void EditableFieldObjectRoutedEventHandler(object obj);

    [AllowForWeb]
    public sealed class EditableFieldPassWebToWinRT
    {
        public event EditableFieldChangedHandler EditableFieldTextChangedEvent;
        public event EditableFieldStateChangedEventHandler NoteFieldStateChangedEvent;
        public event EditableFieldRoutedEventHandler EditableFieldPasteEvent;        
        public event EditableFieldObjectRoutedEventHandler EditableContextMenuEvent;

        public void editableFieldTextChangedEventFire(string fieldName, string html)
        {
            EditableFieldTextChangedEvent?.Invoke(fieldName, html);
        }

        public void editableFieldStateChangedEventFire(string state)
        {
            NoteFieldStateChangedEvent?.Invoke(state);
        }

        public void editableFieldPasteEventFire()
        {
            EditableFieldPasteEvent?.Invoke();
        }

        public void editableFieldContextMenuEventFire(object obj)
        {
            EditableContextMenuEvent?.Invoke(obj);
        }

    }
}
