using System;
using Windows.ApplicationModel.Background;

namespace AnkiU.Anki.Notifications
{
    public sealed class AnkiUniversalDeckBackgroundTask : IBackgroundTask
    {        
        public const string ENTRY_POINT = "Tasks.AnkiUniversalDeckBackgroundTask";                        
        public const string TIME_TRIGGERED_TASK_NAME = "AnkiUniversalDeckTimeTriggeredTask";        
        public bool IsTimeTriggeredTaskRegistered { get; private set; } = false;

        // Note: defined at class scope so we can mark it complete inside the OnCancel() callback if we choose to support cancellation
        private BackgroundTaskDeferral deferral;         

        public AnkiUniversalDeckBackgroundTask()
        {

        }

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            deferral = taskInstance.GetDeferral();
            try
            {
                ToastHelper.PopToast("TEST TITLE", "content");
                using (var collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME))
                {
                    var deckListViewModel = new DeckListViewModel(collection);
                    deckListViewModel.GetAllDeckInformation();
                    await deckListViewModel.UpdateAllSecondaryTilesIfHas();

                    if (deckListViewModel.TotalNewCards + deckListViewModel.TotalDueCards > 0)
                    {
                        string message = String.Format("You have {0} new card(s) and {1} due card(s) to review.",
                                                        deckListViewModel.TotalNewCards,
                                                        deckListViewModel.TotalDueCards);
                        ToastHelper.PopToast("Review cards", message);
                    }
                }
            }
            catch
            {

            }
            finally
            {
                deferral.Complete();
            }
        }

        public void RegisterTimeTriggeredBackgroundTasks()
        {
            IsTimeTriggeredTaskRegistered = GetTaskStatus(TIME_TRIGGERED_TASK_NAME);
            if (IsTimeTriggeredTaskRegistered)
                return;

            var task = BackgroundTasksHelper.RegisterBackgroundTask(ENTRY_POINT,
                                                                TIME_TRIGGERED_TASK_NAME,
                                                                new TimeTrigger(15, false),
                                                                null,
                                                                true);            

            IsTimeTriggeredTaskRegistered = true;
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
    }
}
