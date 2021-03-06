﻿using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Phone.UI.Input;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using UnofficialSteamAuthenticator.Lib.SteamAuth;
using UnofficialSteamAuthenticator.Lib.Models;
using UnofficialSteamAuthenticator.Lib;

namespace UnofficialSteamAuthenticator
{
    public sealed partial class UsersPage
    {
        private readonly Dictionary<ulong, User> listElems = new Dictionary<ulong, User>();
        private readonly SteamWeb web = ((App) Application.Current).SteamWeb;
        private Storyboard storyboard;

        public UsersPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed += this.BackPressed;

            this.ReloadList();
        }

        public void ReloadList()
        {
            this.listElems.Clear();

            this.steamGuardUpdate_Tick(null, null);

            Dictionary<ulong, SessionData> accs = Storage.GetAccounts();
            if (accs.Count < 1)
            {
                return;
            }

            foreach (ulong steamId in accs.Keys)
            {
                var usr = new User(steamId, accs[steamId].LastCurrent, StringResourceLoader.GetString("GeneratingCode_Ellipsis"));
                this.listElems.Add(steamId, usr);
            }

            this.AccountList.ItemsSource = this.listElems.Values.OrderByDescending(item => item.LastCurrent).ToList();

            UserSummary.GetSummaries(this.web, accs.First().Value, accs.Keys.ToArray(), this.SummariesCallback);
        }

        private async void SummariesCallback(Players responseObj)
        {
            if (responseObj?.PlayersList == null)
            {
                return;
            }

            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (Player p in responseObj.PlayersList)
                {
                    if (this.listElems.ContainsKey(p.SteamId))
                    {
                        this.listElems[p.SteamId].Avatar.SetUri(new Uri(p.AvatarUri));
                        this.listElems[p.SteamId].Title = p.Username;
                    }
                }
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= this.BackPressed;
        }

        private void BackPressed(object sender, BackPressedEventArgs args)
        {
            if (!this.Frame.CanGoBack)
                return;

            args.Handled = true;
            this.Frame.GoBack();
        }

        private void AccountList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var user = (User) e.ClickedItem;
            Storage.SetCurrentUser(user.SteamId);
            this.Frame.Navigate(typeof(MainPage));
        }

        private void steamGuardUpdate_Tick(object sender, object e)
        {
            try
            {
                this.storyboard?.Stop();
            }
            catch (NotImplementedException)
            {
                // Not sure why this happens
            }

            TimeAligner.GetSteamTime(this.web, async time =>
            {
                var timeRemaining = (byte) (31 - time % 30);
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.storyboard = new Storyboard();
                    var animation = new DoubleAnimation
                    {
                        Duration = TimeSpan.FromSeconds(timeRemaining),
                        From = timeRemaining,
                        To = 0,
                        EnableDependentAnimation = true
                    };
                    Storyboard.SetTarget(animation, this.SteamGuardTimer);
                    Storyboard.SetTargetProperty(animation, "Value");
                    this.storyboard.Children.Add(animation);
                    this.storyboard.Completed += this.steamGuardUpdate_Tick;
                    this.storyboard.Begin();

                    foreach (ulong steamid in this.listElems.Keys)
                    {
                        this.listElems[steamid].UpdateCode(time);
                    }
                });
            });
        }

        private void NewUser_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(LoginPage));
        }

        private void AccountList_OnHolding(object sender, HoldingRoutedEventArgs e)
        {
            var senderElement = sender as FrameworkElement;
            // If you need the clicked element:
            // Item whichOne = senderElement.DataContext as Item;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);
            flyoutBase.ShowAt(senderElement);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement) e.OriginalSource;
            var selectedOne = (User) element.DataContext;
            if (selectedOne == null)
                return;

            var folderPicker = new FolderPicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            folderPicker.ContinuationData["user"] = selectedOne.SteamId;
            folderPicker.PickFolderAndContinue();
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement) e.OriginalSource;
            var selectedOne = (User) element.DataContext;
            if (selectedOne == null)
                return;

            var dialog = new MessageDialog(StringResourceLoader.GetString("User_Logout_Prompt_Message"))
            {
                Title = StringResourceLoader.GetString("User_Logout_Prompt_Title")
            };
            dialog.Commands.Add(new UICommand(StringResourceLoader.GetString("UiCommand_Yes_Text"), cmd =>
            {
                Storage.Logout(selectedOne.SteamId);
                this.AccountList.Items?.Remove(selectedOne);
            }));
            dialog.Commands.Add(new UICommand(StringResourceLoader.GetString("UiCommand_No_Text"))); // Take no action
            await dialog.ShowAsync();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(AboutPage));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FolderPicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            openPicker.PickFolderAndContinue();
        }
    }
}
