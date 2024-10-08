﻿using Azure.Communication.Calling.WindowsClient;
using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Media.Core;
using Windows.UI.WindowManagement;
using WinRT.Interop;

namespace CallingQuickstart
{
    public partial class MainPage : Page
    {
#if CTE
        //private const string teamsAuthToken = "<TEAMS_AUTHENTICATION_TOKEN>";
        private const string teamsAuthToken = "eyJhbGciOiJSUzI1NiIsImtpZCI6IjYwNUVCMzFEMzBBMjBEQkRBNTMxODU2MkM4QTM2RDFCMzIyMkE2MTkiLCJ4NXQiOiJZRjZ6SFRDaURiMmxNWVZpeUtOdEd6SWlwaGsiLCJ0eXAiOiJKV1QifQ.eyJza3lwZWlkIjoib3JnaWQ6MmEyZWU0YTgtOTAxZS00MDRkLTllZmEtODk1ZWQ4Y2JiOGMwIiwic2NwIjoxMDI0LCJjc2kiOiIxNzIzMTk1MjYzIiwiZXhwIjoxNzIzMjAwMTU5LCJyZ24iOiJhbWVyIiwidGlkIjoiNTg2MDczZmItMDZhZC00OWM4LWIwZDYtZGJiNTg4MDdmNGQxIiwiYWNzU2NvcGUiOiJ2b2lwLGNoYXQiLCJyZXNvdXJjZUlkIjoiZWZhNTAxNDMtZGU3NC00MTVhLTkyODgtOGI2MWFlMjEzMzRiIiwiYWFkX2lhdCI6IjE3MjMxOTUyNjMiLCJhYWRfdXRpIjoiR0x3bGVjRFMtRU9CQVhLTHNJNF9BQSIsImFhZF9hcHBpZCI6IjFmZDUxMThlLTI1NzYtNDI2My04MTMwLTk1MDMwNjRjODM3YSIsImlhdCI6MTcyMzE5NTU2M30.W56syuDFwU7cp0qmiZZTTjafolbg8wiAl8juJ6C9JjQL7IPMjOhDmw9jdFJ4i0obpq1SU3fZyadgVeeQVXdeNNDPCjn7-Z2X-bdWQG0C2ZMZ6ql3SUdWWl4Cmg7YQJDl_8flqCLjSebWEVKOnkmYOKU91ywEc0Rq77t46DfglKk5nDnu8swX1AcHcf3B7228UTwianA_INpg8y6EYT3hNe5T69mGwYMsY28vvHmcHqq0nDRyI1c1tCou9lBYE36I3PG0EMGCal62hhqdDIHBTHGNNu0V3fPPCxxvsvh5o7_IiFANkPBd_FAfvxJvT11xVDMeiSdHNokKGyM21YWJPg";

        private TeamsCallAgent teamsCallAgent;
        private TeamsCommunicationCall teamsCall;

        private async Task InitCallAgentAndDeviceManagerAsync()
        {
            this.callClient = new CallClient(new CallClientOptions() {
                Diagnostics = new CallDiagnosticsOptions() { 
                        AppName = "CallingQuickstart",
                        AppVersion="1.0",
                        Tags = new[] { "Calling", "Teams", "Windows" }
                    }
                });

            // Set up local video stream using the first camera enumerated
            var deviceManager = await this.callClient.GetDeviceManagerAsync();
            var camera = deviceManager?.Cameras?.FirstOrDefault();
            var mic = deviceManager?.Microphones?.FirstOrDefault();
            micStream = new LocalOutgoingAudioStream();

            CameraList.ItemsSource = deviceManager.Cameras.ToList();

            if (camera != null)
            {
                CameraList.SelectedIndex = 0;
            }

            var teamsTokenCredential = new CallTokenCredential(teamsAuthToken);

            var callAgentOptions = new CallAgentOptions()
            {
                DisplayName = $"{Environment.MachineName}/{Environment.UserName}",
                //https://github.com/lukes/ISO-3166-Countries-with-Regional-Codes/blob/master/all/all.csv
                EmergencyCallOptions = new EmergencyCallOptions() { CountryCode = "840" }
            };

            try
            {
                this.teamsCallAgent = await this.callClient.CreateTeamsCallAgentAsync(teamsTokenCredential);
                this.teamsCallAgent.CallsUpdated += OnCallsUpdatedAsync;
                this.teamsCallAgent.IncomingCallReceived += OnIncomingCallAsync;
            }
            catch(Exception ex)
            {
                if (ex.HResult == -2147024809)
                {
                    // E_INVALIDARG
                    // Handle possible invalid token
                }
            }
        }

        private async void CameraList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCamerea = CameraList.SelectedItem as VideoDeviceDetails;
            if (cameraStream != null)
            {
                await cameraStream.StopPreviewAsync();
            }
            
            cameraStream = new LocalOutgoingVideoStream(selectedCamerea);
            var localUri = await cameraStream.StartPreviewAsync();
            LocalVideo.Source = MediaSource.CreateFromUri(localUri);

            if (teamsCall != null)
            {
                await teamsCall.StartVideoAsync(cameraStream);
            }
        }

        private async void CallButton_Click(object sender, RoutedEventArgs e)
        {
            var callString = CalleeTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(callString))
            {
                if (callString.StartsWith("8:")) // 1:1 ACS call
                {
                    teamsCall = await StartCteCallAsync(callString);
                }
                else if (callString.All(char.IsDigit)) // rooms call
                { 
                }
                else if (callString.StartsWith("+")) // 1:1 phone call
                {
                    teamsCall = await StartPhoneCallAsync(callString);
                }
                else if (Uri.TryCreate(callString, UriKind.Absolute, out Uri teamsMeetinglink)) //Teams meeting link
                {
                    teamsCall = await JoinTeamsMeetingByLinkWithCteAsync(teamsMeetinglink);
                }
            }

            teamsCall.RemoteParticipantsUpdated += OnRemoteParticipantsUpdatedAsync;
            teamsCall.StateChanged += OnStateChangedAsync;
        }

        private async void HangupButton_Click(object sender, RoutedEventArgs e)
        {
            var call = this.teamsCallAgent?.Calls?.FirstOrDefault();
            foreach (var localVideoStream in call?.OutgoingVideoStreams)
            {
                await call.StopVideoAsync(localVideoStream);
            }

            try
            {
                if (cameraStream != null)
                {
                    await cameraStream.StopPreviewAsync();
                }

                await call.HangUpAsync(new HangUpOptions() { ForEveryone = false });
            }
            catch(Exception ex) 
            { 
                var errorCode = unchecked((int)(0x0000FFFFU & ex.HResult));
                if (errorCode != 98) // sam_status_failed_to_hangup_for_everyone (98)
                {
                    throw;
                }
            }
        }

        private async void MuteLocal_Click(object sender, RoutedEventArgs e)
        {
            var muteCheckbox = sender as CheckBox;

            if (muteCheckbox != null)
            {
                var call = this.teamsCallAgent?.Calls?.FirstOrDefault();

                if ((bool)muteCheckbox.IsChecked)
                {
                    await call?.MuteOutgoingAudioAsync();
                }
                else
                {
                    await call?.UnmuteOutgoingAudioAsync();
                }

                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    AppTitleBar.Background = call.IsOutgoingAudioMuted ? new SolidColorBrush(Colors.PaleVioletRed) : new SolidColorBrush(Colors.SeaGreen);
                });

                //await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                //{
                //    AppTitleBar.Background = call.IsOutgoingAudioMuted ? new SolidColorBrush(Colors.PaleVioletRed) : new SolidColorBrush(Colors.SeaGreen);
                //});
            }
        }

        private async void OnIncomingCallAsync(object sender, TeamsIncomingCallReceivedEventArgs args)
        {
            var teamsIncomingCall = args.IncomingCall;

            var acceptTeamsCallOptions = new AcceptTeamsCallOptions()
            {
                IncomingVideoOptions = new IncomingVideoOptions()
                {
                    StreamKind = VideoStreamKind.RemoteIncoming
                }
            };

            teamsCall = await teamsIncomingCall.AcceptAsync(acceptTeamsCallOptions);
            teamsCall.StateChanged += OnStateChangedAsync;
            teamsCall.RemoteParticipantsUpdated += OnRemoteParticipantsUpdatedAsync;
        }

        private async void OnCallsUpdatedAsync(object sender, TeamsCallsUpdatedEventArgs args)
        {
            var removedParticipants = new List<RemoteParticipant>();
            var addedParticipants = new List<RemoteParticipant>();

            foreach (var teamsCall in args.RemovedCalls)
            {
                removedParticipants.AddRange(teamsCall.RemoteParticipants.ToList<RemoteParticipant>());
            }

            foreach (var teamsCall in args.AddedCalls)
            {
                addedParticipants.AddRange(teamsCall.RemoteParticipants.ToList<RemoteParticipant>());
            }

            await OnParticipantChangedAsync(removedParticipants, addedParticipants);
        }

        private async void OnStateChangedAsync(object sender, PropertyChangedEventArgs args)
        {
            var call = sender as TeamsCommunicationCall;

            if (call != null)
            {
                var state = call.State;

                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    QuickstartTitle.Text = $"{Package.Current.DisplayName} - {state.ToString()}";
                    //Window.Current.SetTitleBar(AppTitleBar);

                    HangupButton.IsEnabled = state == CallState.Connected || state == CallState.Ringing;
                    CallButton.IsEnabled = !HangupButton.IsEnabled;
                    MuteLocal.IsEnabled = !CallButton.IsEnabled;
                });

                //await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                //{
                //    QuickstartTitle.Text = $"{Package.Current.DisplayName} - {state.ToString()}";
                //    Window.Current.SetTitleBar(AppTitleBar);

                //    HangupButton.IsEnabled = state == CallState.Connected || state == CallState.Ringing;
                //    CallButton.IsEnabled = !HangupButton.IsEnabled;
                //    MuteLocal.IsEnabled = !CallButton.IsEnabled;
                //});

                switch (state)
                {
                    case CallState.Connected:
                        {
                            await call.StartAudioAsync(micStream);
                            await DispatcherQueue.EnqueueAsync(async () =>
                            {
                                Stats.Text = $"Call id: {Guid.Parse(call.Id).ToString("D")}, Remote caller id: {call.RemoteParticipants.FirstOrDefault()?.Identifier.RawId}";
                            });

                            //await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            //{
                            //    Stats.Text = $"Call id: {Guid.Parse(call.Id).ToString("D")}, Remote caller id: {call.RemoteParticipants.FirstOrDefault()?.Identifier.RawId}";
                            //});

                            break;
                        }
                    case CallState.Disconnected:
                        {
                            call.RemoteParticipantsUpdated -= OnRemoteParticipantsUpdatedAsync;
                            call.StateChanged -= OnStateChangedAsync;

                            await DispatcherQueue.EnqueueAsync(async () =>
                            {
                                Stats.Text = $"Call ended: {call.CallEndReason.ToString()}";
                            });

                            //await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            //{
                            //    Stats.Text = $"Call ended: {call.CallEndReason.ToString()}";
                            //});

                            call.Dispose();

                            break;
                        }
                    default: break;
                }
            }
        }

        private async Task<TeamsCommunicationCall> JoinTeamsMeetingByLinkWithCteAsync(Uri teamsCallLink)
        {
            var joinTeamsCallOptions = GetJoinTeamsCallOptions();

            var teamsMeetingLinkLocator = new TeamsMeetingLinkLocator(teamsCallLink.AbsoluteUri);
            var call = await teamsCallAgent.JoinAsync(teamsMeetingLinkLocator, joinTeamsCallOptions);
            return call;
        }

        private JoinTeamsCallOptions GetJoinTeamsCallOptions()
        {
            return new JoinTeamsCallOptions()
            {
                OutgoingAudioOptions = new OutgoingAudioOptions() { IsMuted = true },
                OutgoingVideoOptions = new OutgoingVideoOptions() { Streams = new OutgoingVideoStream[] { cameraStream } }
            };
        }

        private StartTeamsCallOptions GetStartTeamsCallOptions()
        {
            var startTeamsCallOptions = new StartTeamsCallOptions()
            {
                OutgoingAudioOptions = new OutgoingAudioOptions()
                {
                    IsMuted = true,
                    Stream = micStream,
                    Filters = new OutgoingAudioFilters()
                    {
                        AnalogAutomaticGainControlEnabled = true,
                        AcousticEchoCancellationEnabled = true,
                        NoiseSuppressionMode = NoiseSuppressionMode.High
                    }
                },
                OutgoingVideoOptions = new OutgoingVideoOptions() { Streams = new OutgoingVideoStream[] { cameraStream } }
            };

            return startTeamsCallOptions;
        }

        private async Task<TeamsCommunicationCall> StartCteCallAsync(string callee)
        {
            var options = GetStartTeamsCallOptions();
            var call = await this.teamsCallAgent.StartCallAsync(new MicrosoftTeamsUserCallIdentifier(callee), options);
            return call;
        }

        private async Task<TeamsCommunicationCall> StartPhoneCallAsync(string callee)
        {
            var options = GetStartTeamsCallOptions();

            var call = await this.teamsCallAgent.StartCallAsync(new PhoneNumberCallIdentifier(callee), options);
            return call;
        }
#endif
    }
}
