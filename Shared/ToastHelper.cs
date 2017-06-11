//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Shared.AnkiCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Notifications;

namespace Shared
{
    public class ToastHelper
    {        
        public static bool IsAlreadyShown()
        {   
            var today = DateTimeOffset.Now.DayOfYear;
            var settings = ApplicationData.Current.LocalSettings;
            object dayShow;
            bool isSuccess = settings.Values.TryGetValue("NoticeToast", out dayShow);
            if(isSuccess)
            {
                if (Convert.ToInt32(dayShow) == today)
                    return true;
            }
            return false;
        }

        public static void MarkAlreadyShown()
        {
            var today = DateTimeOffset.Now.DayOfYear;
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["NoticeToast"] = today;
        }

        public static void SetNotShownForToday()
        {            
            var settings = ApplicationData.Current.LocalSettings;
            object dayShow;
            bool isSuccess = settings.Values.TryGetValue("NoticeToast", out dayShow);
            if (isSuccess)
            {
                settings.Values["NoticeToast"] = Convert.ToInt32(dayShow) - 1;
            }            
        }

        public static ToastNotification CreateToast(string title, string content)
        {
            return CreateToast(title, content, null, null);
        }

        public static ToastNotification CreateToast(string title, string content, string tag, string group)
        {
            string xml = 
                $@"
                <toast activationType='foreground' launch='args' scenario='reminder'>
                    <visual>
                        <binding template='ToastGeneric'>                            
                        </binding>
                    </visual>
                    <actions>
                            <input id='snoozeTime' type='selection' defaultInput='15' >
                                  <selection id='1' content='1 minute' />
                                  <selection id='15' content='15 minutes' />                                  
                                  <selection id='30' content='30 minutes' />
                                  <selection id='60' content='1 hour' />                                  
                                  <selection id='180' content='3 hours' />
                            </input>

                        <action activationType='system' arguments='snooze' hint-inputId='snoozeTime' content='' />

                        <action activationType='system' arguments='dismiss' content=''/>
                    </actions>

                </toast>";
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            var binding = doc.SelectSingleNode("//binding");

            var el = doc.CreateElement("text");
            el.InnerText = title;

            binding.AppendChild(el);

            el = doc.CreateElement("text");
            el.InnerText = content;
            binding.AppendChild(el);

            return CreateCustomToast(doc, tag, group);
        }

        public static ToastNotification CreateCustomToast(string xml)
        {
            return CreateCustomToast(xml, null, null);
        }

        public static ToastNotification CreateCustomToast(string xml, string tag, string group)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);


            return CreateCustomToast(doc, tag, group);
        }

        [DefaultOverloadAttribute]
        public static ToastNotification CreateCustomToast(XmlDocument doc, string tag, string group)
        {
            var toast = new ToastNotification(doc);

            if (tag != null)
                toast.Tag = tag;

            if (group != null)
                toast.Group = group;            

            return toast;
        }

        public static void PopToast(ToastNotification toast)
        {
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public static string ToString(ValueSet valueSet)
        {
            StringBuilder builder = new StringBuilder();

            foreach (var pair in valueSet)
            {
                if (builder.Length != 0)
                    builder.Append('\n');

                builder.Append(pair.Key);
                builder.Append(": ");
                builder.Append(pair.Value);
            }

            return builder.ToString();
        }
    }
}
