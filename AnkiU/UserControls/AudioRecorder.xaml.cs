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

using AnkiU.AnkiCore;
using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Text;

namespace AnkiU.UserControls
{
    public sealed partial class AudioRecorder : UserControl, IDisposable
    {
        private MediaCapture capture;
        MediaElement playback;
        private InMemoryRandomAccessStream buffer;
        private Stopwatch stopwatch = new Stopwatch();        
        private bool isRecording;
        private static readonly string audioExtension;
        private TimeSpan timeRecord;
        private CoreDispatcher dispatcher;

        static AudioRecorder()
        {
            if (UIHelper.GetDeviceFamily() != "Windows.Mobile")
                audioExtension = ".mp3";
            else
                audioExtension = ".mp4";
        }

        public AudioRecorder()
        {
            this.InitializeComponent();
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
        }           

        private async void RecordButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (isRecording)
                        return;

                    recordButton.Visibility = Visibility.Collapsed;
                    stopRecordButton.Visibility = Visibility.Visible;
                    playButton.IsEnabled = false;
                    StartRecordTimerCount();

                    if (await SetupRecordProcess())
                    {
                        if (UIHelper.GetDeviceFamily() != "Windows.Mobile")
                            await capture.StartRecordToStreamAsync(MediaEncodingProfile.CreateMp3(AudioEncodingQuality.Auto), buffer);
                        else
                        { //No mp3 or wma on win mobile so we use mp4 instead, still give better compressed size than using raw format                   
                            await capture.StartRecordToStreamAsync(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto), buffer);
                        }
                        if (isRecording)
                        {
                            ThrowInvalidOperantionException();
                            return;
                        }

                        isRecording = true;
                    }
                    else
                    {
                        await UnableToStartRecording();
                    }
                }
                catch
                {
                    await UnableToStartRecording();
                }
            });
        }

        private async Task UnableToStartRecording()
        {
            ResetViewToStart();
            DisplayCurrentTimeSpan(new TimeSpan(0));
            await UIHelper.ShowMessageDialog("Unable to start recording. Please make sure you have a recording device and the app is allowed to access it.");
        }

        [Conditional("DEBUG")]
        private static void ThrowInvalidOperantionException()
        {
            throw new InvalidOperationException();
        }

        private async Task<bool> SetupRecordProcess()
        {
            if (buffer != null)            
                buffer.Dispose();            
            buffer = new InMemoryRandomAccessStream();

            if (capture != null)
            {
                capture.Dispose();
            }

            try
            {
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Audio
                };
                capture = new MediaCapture();
                await capture.InitializeAsync(settings);
                capture.RecordLimitationExceeded +=  RecordLimitExceedEventHandler;                                
                capture.Failed += CaptureFailedEventHandler;
            }
            catch (Exception ex)
            {
                ThrowException(ex);
                return false;                         
            }
            return true;
        }

        [Conditional("DEBUG")]
        private void ThrowException(Exception ex)
        {
            if (ex.InnerException != null && ex.InnerException.GetType() == typeof(UnauthorizedAccessException))
            {
                throw ex.InnerException;
            }
            throw ex;
        }

        private async void RecordLimitExceedEventHandler(MediaCapture sender)
        {
            await StopRecording();
            isRecording = false;
            await UIHelper.ShowMessageDialog("Record limitation exceeded!");
        }

        private void CaptureFailedEventHandler(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {                        
            if (isRecording)
            {
                capture.RecordLimitationExceeded -= RecordLimitExceedEventHandler;
                capture.Failed -= CaptureFailedEventHandler;
                isRecording = false;
                Close();
            }
        }

        private async void StopRecordButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await StopRecording();
        }

        private async Task StopRecording()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ResetViewToStart();
                playButton.IsEnabled = true;

                await capture.StopRecordAsync();
                isRecording = false;
            });
        }

        private void ResetViewToStart()
        {
            stopRecordButton.Visibility = Visibility.Collapsed;
            recordButton.Visibility = Visibility.Visible;            
            StopTimerCount();
        }

        private async void StartRecordTimerCount()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                stopwatch.Start();
                while (stopwatch != null && stopwatch.IsRunning)
                {
                    timeRecord = stopwatch.Elapsed;
                    DisplayCurrentTimeSpan(timeRecord);
                    await Task.Delay(500);
                }
            });
        }

        private async void PlayButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                playButton.Visibility = Visibility.Collapsed;
                stopPlayButton.Visibility = Visibility.Visible;
                recordButton.IsEnabled = false;

                PlayRecordedAudio();
            });
        }

        public void PlayRecordedAudio()
        {
            playback = new MediaElement();            

            if (buffer == null)
                return;

            playback.SetSource(buffer, audioExtension);
            playback.Play();
            StartPlaybackRecordCountDown();            
        }

        private async void StopPlayButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await StopPlaying();
        }

        private async Task StopPlaying()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (playback == null)
                    return;

                playback.Stop();
                StopTimerCount();
                playback = null;
                //GC.Collect(); //Disable in "Creator Update"

                playButton.Visibility = Visibility.Visible;
                stopPlayButton.Visibility = Visibility.Collapsed;
                recordButton.IsEnabled = true;
            });
        }

        private void DisplayCurrentTimeSpan(TimeSpan time)
        {
            hourTextBlock.Text = time.Hours.ToString("00");
            minueTextBlock.Text = time.Minutes.ToString("00");
            secondTextBlock.Text = time.Seconds.ToString("00");
        }

        private async void StartPlaybackRecordCountDown()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var timeSpan = timeRecord;                
                stopwatch.Start();
                while (stopwatch != null && timeSpan.TotalSeconds > 0 && stopwatch.IsRunning)
                {                              
                    timeSpan = timeRecord - stopwatch.Elapsed;
                    DisplayCurrentTimeSpan(timeSpan);
                    await Task.Delay(500);
                }
                StopPlayButtonClickHandler(null, null);
                DisplayCurrentTimeSpan(timeRecord);
            });
        }

        private void StopTimerCount()
        {
            stopwatch.Reset();
        }

        public async Task<StorageFile> TrySaveAudio(StorageFolder folder = null)
        {
            try
            {
                if (isRecording)
                    await StopRecording();
                if (playback != null)
                    await StopPlaying();

                if (folder == null)
                    folder = ApplicationData.Current.LocalFolder;

                IRandomAccessStream audio = buffer.CloneStream();
                if (audio == null || audio.Size == 0)
                    return null;

                var name = UIHelper.GetDateTimeStringForName();
                name.Append(DateTimeOffset.Now.Millisecond);
                string filename = name.Append(audioExtension).ToString();

                StorageFile storageFile = await folder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);                
                using (IRandomAccessStream fileStream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await RandomAccessStream.CopyAndCloseAsync(audio.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
                    await audio.FlushAsync();
                    audio.Dispose();
                }
                return storageFile;
            }
            catch
            {
                return null;
            }
        }

        public void Close()
        {
            if (buffer != null)
            {
                buffer.Dispose();
                buffer = null;
            }
            if (capture != null)
            {
                capture.Dispose();
                capture = null;
            }
            if (playback != null)
            {
                playback.Stop();
                playback = null;
            }
            if(stopwatch != null)
            {
                stopwatch.Stop();
                stopwatch = null;
            }
            //GC.Collect(); //Disable in "Creator Update"
        }

        public void Dispose()
        {
            Close();
        }
    }
}
