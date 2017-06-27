using Shared.AnkiCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace Shared
{
    public class AnkiDeckBackgroundRegisterHelper
    {
        private const string STORAGE_FOLDER = "BackgroundProcessStorage";

        public const string ENTRY_POINT = "AnkiBackgroundRuntimeComponent.AnkiUniversalDeckBackgroundTask";
        public const string SYSTEM_TRIGGERED_TASK_NAME = "AnkiUniversalDeckSystemTriggeredTask";
        public const string TIME_TRIGGERED_TASK_NAME = "AnkiUniversalDeckTimeTriggeredTask";

        private const int BACKGROUND_RATE = 60;
        private bool isTimeTaskRegistered = false;
        private bool isSystemTaskRegistered = false;

        public IBackgroundTaskRegistration SytemTriggeredBackgroundTask { get; private set; }
        public IBackgroundTaskRegistration TimeTriggeredBackgroundTask { get; private set; }

        public async Task RegisterBackgroundTasks()
        {
            isSystemTaskRegistered = GetTaskStatus(SYSTEM_TRIGGERED_TASK_NAME);
            isTimeTaskRegistered = GetTaskStatus(TIME_TRIGGERED_TASK_NAME);

            if (!isSystemTaskRegistered)
            {                
                var trigger = new SystemTrigger(SystemTriggerType.UserPresent | SystemTriggerType.SessionConnected, false);
                SytemTriggeredBackgroundTask = await BackgroundTasksHelper.RegisterBackgroundTask(ENTRY_POINT,
                                                SYSTEM_TRIGGERED_TASK_NAME,
                                                trigger,
                                                null,
                                                true);
                isSystemTaskRegistered = true;
            }
            
            if (!isTimeTaskRegistered)
            {
                SystemCondition userPresentCondition = new SystemCondition(SystemConditionType.UserPresent);
                TimeTriggeredBackgroundTask = await BackgroundTasksHelper.RegisterBackgroundTask(ENTRY_POINT,
                                                                    TIME_TRIGGERED_TASK_NAME,
                                                                    new TimeTrigger(BACKGROUND_RATE, false),
                                                                    userPresentCondition,
                                                                    true);
                isTimeTaskRegistered = true;
            }


        }

        private bool GetTaskStatus(string taskName)
        {
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == taskName)
                {                 
                    return true;
                }
            }
            return false;
        }

        public void UnRegisterBackgroundTasks()
        {
            BackgroundTasksHelper.UnregisterBackgroundTasks(SYSTEM_TRIGGERED_TASK_NAME);
            isSystemTaskRegistered = false;
            BackgroundTasksHelper.UnregisterBackgroundTasks(TIME_TRIGGERED_TASK_NAME);
            isTimeTaskRegistered = false;
        }

        public static async Task<StorageFolder> GetBackgroundStorageFolder()
        {
            StorageFolder folder = null;
            var item = await Storage.AppLocalFolder.TryGetItemAsync(STORAGE_FOLDER);
            if (item != null)
                folder = item as StorageFolder;

            if (folder == null)
                folder = await Storage.AppLocalFolder.CreateFolderAsync(STORAGE_FOLDER);
            return folder;
        }
    }
}
