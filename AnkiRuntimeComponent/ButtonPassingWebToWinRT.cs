using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace AnkiRuntimeComponent
{
    public delegate void ClickEventHandler(object sender);

    [AllowForWeb]
    public sealed class ButtonPassingWebToWinRT
    {
        public event ClickEventHandler ButtonClickEvent;

        //WARNING: (Maybe an error in framework) in javascript function name is automatically set to has
        //the first letter in lower case, we also start with lower case here to avoid
        //misleading name
        public void clickEventFire(object sender)
        {
            ButtonClickEvent(sender);
        }
    }
}
