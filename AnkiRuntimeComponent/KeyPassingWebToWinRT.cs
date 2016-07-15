using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace AnkiRuntimeComponent
{
    public delegate void KeyDownEventHandler(int keyCode);
    /// <summary>
    /// Because WebviewControl does not support Keydown and Keyup events
    /// we need this class to map key events from webview to native code runtime
    /// </summary>
    [AllowForWeb]
    public sealed class KeyPassingWebToWinRT
    {
        public event KeyDownEventHandler KeyDownEvent;

        //WARNING: (Maybe an error in framework) in javascript function name is automatically set to has
        //the first letter in lower case, we also start with lower case here to avoid
        //misleading name
        public void keyDownEventFire(int keyCode)
        {
            KeyDownEvent(keyCode);
        }
    }
}
