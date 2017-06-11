using Shared;
using Shared.AnkiCore;
using Shared.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.System;
using Windows.UI.Notifications;

namespace AnkiBackgroundRuntimeComponent
{

    public sealed class AnkiUniversalDeckBackgroundTask : IBackgroundTask
    {        
        // Note: defined at class scope so we can mark it complete inside the OnCancel() callback if we choose to support cancellation
        private BackgroundTaskDeferral deferral;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            deferral = taskInstance.GetDeferral();
            await EnableCancelOfTask(taskInstance);            

            try
            {
                using (var collection = await Storage.OpenCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME))
                {
                    var deckListViewModel = new DeckListViewModel(collection);
                    deckListViewModel.GetAllDeckInformation();
                    await deckListViewModel.UpdateAllSecondaryTilesIfHas();

                    if (deckListViewModel.TotalNewCards + deckListViewModel.TotalDueCards > 0)                        
                        ShowToastIfNeeded(deckListViewModel);                        
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

        private async Task EnableCancelOfTask(IBackgroundTaskInstance taskInstance)
        {
            MakeSureOnlyHookCancelOne(taskInstance);
            taskInstance.Progress = 0;
            await Task.Delay(200);
        }

        private void ShowToastIfNeeded(DeckListViewModel deckListViewModel)
        {
            var settings = ApplicationData.Current.LocalSettings;
            bool isShown;
            if(settings.Values.ContainsKey("IsEnableNotifciation"))            
                isShown = (bool)settings.Values["IsEnableNotifciation"];
            else
            {
                settings.Values["IsEnableNotifciation"] = true;
                isShown = true;
            }

            if (!isShown)
                return;

            if (ToastHelper.IsAlreadyShown())
                return;

            string message = String.Format("You have {0} new card(s) and {1} due card(s) to review today.",
                                                                    deckListViewModel.TotalNewCards,
                                                                    deckListViewModel.TotalDueCards);        
            var toast = ToastHelper.CreateToast("Review", message);
            ToastHelper.PopToast(toast);
            ToastHelper.MarkAlreadyShown();
        }

        private void MakeSureOnlyHookCancelOne(IBackgroundTaskInstance taskInstance)
        {
            taskInstance.Canceled -= OnTaskInstanceCanceled;
            taskInstance.Canceled += OnTaskInstanceCanceled;
        }

        private void OnTaskInstanceCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if(deferral != null)
                deferral.Complete();
        }
    }

}
