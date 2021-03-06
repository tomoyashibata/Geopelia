﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using CoreTweet;
using CoreTweet.Core;
using CoreTweet.Streaming;
using Geopelia.ViewModels;
using Microsoft.Practices.ObjectBuilder2;
using Prism.Mvvm;
using Prism.Windows.Navigation;

namespace Geopelia.Models
{
    public class TwitterClient : BindableBase
    {
        private readonly Tokens _tokens;

        private ObservableCollection<TweetModel> _timeLines = new ObservableCollection<TweetModel>();
        public ObservableCollection<TweetModel> Timelines
        {
            get { return this._timeLines; }
            set { this.SetProperty(ref this._timeLines, value); }
        }

        private ObservableCollection<TweetItemViewModel> _tweetItems = new ObservableCollection<TweetItemViewModel>();
        public ObservableCollection<TweetItemViewModel> TweetItems
        {
            get { return this._tweetItems; }
            set { this.SetProperty(ref this._tweetItems, value); }
        }

        public ObservableCollection<TweetItemViewModel> MentionItems = new ObservableCollection<TweetItemViewModel>();
        public ObservableCollection<TweetItemViewModel> MyItems      = new ObservableCollection<TweetItemViewModel>();

        private ObservableCollection<string> _friendScreenNames = new ObservableCollection<string>();
        public ObservableCollection<string> FriendScreenNames
        {
            get { return this._friendScreenNames; }
            set { this.SetProperty(ref this._friendScreenNames, value); }
        }

        private ObservableCollection<string> _filteredfriendScreenNames = new ObservableCollection<string>();
        public ObservableCollection<string> FilteredFriendScreenNames
        {
            get { return this._filteredfriendScreenNames; }
            set { this.SetProperty(ref this._filteredfriendScreenNames, value); }
        }

        private ObservableCollection<User> _followers = new ObservableCollection<User>();
        public ObservableCollection<User> Followers
        {
            get { return this._followers; }
            set { this.SetProperty(ref this._followers, value); }
        }

        private Status _replyToStatus;
        public Status ReplyToStatus
        {
            get { return this._replyToStatus; }
            set { this.SetProperty(ref this._replyToStatus, value); }
        }

        public TwitterClient()
        {
            this._tokens = Tokens.Create(TwitterConst.ConsumerKey, TwitterConst.ConsumerSecret, TwitterConst.AccessToken,
                TwitterConst.AccessTokenSecret);
        }

        private ObservableCollection<TweetItemViewModel> _userTweetItems = new ObservableCollection<TweetItemViewModel>();
        public ObservableCollection<TweetItemViewModel> UserTweetItems
        {
            get { return this._userTweetItems; }
            set { this.SetProperty(ref this._userTweetItems, value); }
        }

        /// <summary>
        /// ツイートを投稿する
        /// </summary>
        /// <param name="t">ツイート本文</param>
        /// <param name="selectedPictures">選択された画像ファイルの List</param>
        public async void PostTweetAsync(string t, IReadOnlyList<StorageFile> selectedPictures)
        {
            var mediaIds         =  await this.UploadPictures(selectedPictures);
            var replayToStatusId = this._replyToStatus?.Id;
            await this._tokens.Statuses.UpdateAsync(status => t, in_reply_to_status_id => replayToStatusId, media_ids => mediaIds);
            this._replyToStatus = null;
        }

        /// <summary>
        /// ツイート投稿前に添画像ファイルのアップロードを行う
        /// </summary>
        /// <param name="selectedPictures">選択された画像ファイルの List</param>
        /// <returns>mediaId の Array</returns>
        private async Task<IEnumerable<long>> UploadPictures(IEnumerable<StorageFile> selectedPictures)
        {
            if (selectedPictures == null) return null;
            var mediaUploadResults = await Task.WhenAll(selectedPictures.Select(s => this._tokens.Media.UploadAsync(media => s)));
            return mediaUploadResults.Select(m => m.MediaId);
        }

        /// <summary>
        /// リツイート状態を切替える
        /// </summary>
        /// <param name="tweetModel">ツイート Model</param>
        /// <param name="newIsRetweeted"></param>
        /// <returns></returns>
        public async Task<StatusResponse> ChangeIsRetweetedAsync(TweetModel tweetModel, bool newIsRetweeted)
            => newIsRetweeted ? await this._tokens.Statuses.RetweetAsync(tweetModel.Id)
                              : await this._tokens.Statuses.DestroyAsync(tweetModel.MyRetweetId);

        /// <summary>
        /// お気に入り状態を切替える
        /// </summary>
        /// <param name="id">ツイート ID</param>
        /// <param name="newIsFavorited"></param>
        public async Task<StatusResponse> ChangeIsFavorited(long id, bool newIsFavorited)
            => newIsFavorited ? await this._tokens.Favorites.CreateAsync(id)
                              : await this._tokens.Favorites.DestroyAsync(id);

        /// <summary>
        /// TL, Mention の Streaming 受信を開始する
        /// </summary>
        /// <param name="iNavigationService"></param>
        public void StartStreaming(INavigationService iNavigationService)
        {
            var observable = this._tokens.Streaming.UserAsObservable()
                .Where(m => m.Type == MessageType.Create);
            observable
                .Cast<StatusMessage>()
                .Subscribe(m => this.TweetItems.Insert(0, new TweetItemViewModel(iNavigationService, m.Status, this)));
            observable
                .Cast<StatusMessage>()
                .Where(m => m.Status.InReplyToScreenName?.Contains("tomoya_shibata") ?? false)
                .Subscribe(m => this.MentionItems.Insert(0, new TweetItemViewModel(iNavigationService, m.Status, this)));
        }

        public async void GetMyFollowers()
        {
            var followers = await this._tokens.Followers.ListAsync(cursor => -1, count => 200);
            followers.ToObservable().Subscribe(u => this.Followers.Add(u));
        }

        /// <summary>
        /// 自分のプロフィール情報を取得する
        /// </summary>
        /// <returns></returns>
        public async Task<UserResponse> GetMyProfile()
            => await this._tokens.Users.ShowAsync(57864731);

        /// <summary>
        /// 初回描画時のタイムラインを取得する
        /// </summary>
        /// <returns></returns>
        public void InitTimelines(INavigationService iNavigationService)
        {
            this._tokens.Statuses.HomeTimelineAsync(50)
                .Result.ToObservable()
                .Subscribe(s => this.TweetItems.Insert(0, new TweetItemViewModel(iNavigationService, s, this)));
        }

        /// <summary>
        /// 初回描画時のメンションを取得する
        /// </summary>
        /// <param name="iNavigationService"></param>
        public void InitMentions(INavigationService iNavigationService)
        {
            this._tokens.Statuses.MentionsTimelineAsync(10)
                .Result.ToObservable()
                .Subscribe(s => this.MentionItems.Add(new TweetItemViewModel(iNavigationService, s, this)));
        }

        /// <summary>
        /// 初回描画時の自ツイートを取得する
        /// </summary>
        /// <param name="iNavigationService"></param>
        public void InitMyItems(INavigationService iNavigationService)
        {
            this._tokens.Statuses.UserTimelineAsync(this._tokens.UserId)
                .Result.ToObservable()
                .Subscribe(s => this.MyItems.Insert(0, new TweetItemViewModel(iNavigationService, s, this)));
        }

        /// <summary>
        /// ツイートを取得する
        /// </summary>
        /// <param name="id">ツイートID</param>
        /// <returns>ツイート</returns>
        public async Task<StatusResponse> GetTweetAsync(long id)
        {
            return await _tokens.Statuses.ShowAsync(id);
        }

        /// <summary>
        /// フォローユーザの ScreenName のリストをセット
        /// </summary>
        /// <returns></returns>
        public void SetFriendScreenNames()
        {
            this._friendScreenNames.Add("TanakaShibata");
            this._friendScreenNames.Add("TanashimaShibata");
            this._friendScreenNames.Add("TaneshimaShibata");
            this._friendScreenNames.Add("TaneshigeShibata");
            this._friendScreenNames.Add("TaneshibaShibata");
            this._friendScreenNames.Add("TanesadaShibata");

            //long nextCursor = -1;
            //while (nextCursor != 0)
            //{
            //    // TODO:count ベタウチは直す（本当に？）
            //    var friends = this._tokens.Friends.ListAsync(cursor => nextCursor, count => 200).Result;
            //    friends.ForEach(f => this._friendScreenNames.Add(f.ScreenName));
            //    nextCursor = friends.NextCursor;
            //}
        }

        /// <summary>
        /// フォロー ScreenName リストをフィルタリングする
        /// </summary>
        /// <param name="inputTweetText">ツイート文字列</param>
        /// <param name="caretIndex">カーソル位置</param>
        public void FilteringFriendScreenNames(string inputTweetText, int caretIndex)
        {
            var startPosWipScreenName       = inputTweetText.Substring(0, caretIndex).LastIndexOf('@') + 1;
            var wipScreenName               = inputTweetText.Substring(startPosWipScreenName, caretIndex - 1);
            this._filteredfriendScreenNames.Clear();
            this._friendScreenNames
                .Where(f => f.StartsWith(wipScreenName))
                .ForEach(f => this._filteredfriendScreenNames.Add(f));
        }

        /// <summary>
        /// ユーザを取得する
        /// </summary>
        /// <param name="userId">ユーザ ID</param>
        /// <returns></returns>
        public async Task<UserResponse> GetUser(long userId)
        {
            var user = await this._tokens.Users.ShowAsync(user_id => userId);
            return user;
        }

        /// <summary>
        /// ユーザのツイート一覧をリフレッシュします
        /// </summary>
        /// <param name="userId">ユーザID</param>
        /// <returns></returns>
        public async void RefreshUserTweetItems(INavigationService iNavigationService, long userId)
        {
            this._userTweetItems.Clear();
            var userTweets =  await this._tokens.Statuses.UserTimelineAsync(
                user_id => userId,
                count => 50,
                exclude_replies => true,
                include_rts => true
                );
            userTweets.ForEach(u => this._userTweetItems.Add(new TweetItemViewModel(iNavigationService, u, this)));
        }

        public async Task LoadPastUserTweetItems(INavigationService iNavigationService, long userId)
        {
            var userTweets = await this._tokens.Statuses.UserTimelineAsync(
                user_id => userId,
                count => 50,
                since_id => this._userTweetItems.Last().TweetModel.Value.Id,
                exclude_replies => true,
                include_rts => true
            );
            userTweets.ForEach(u => this._userTweetItems.Add(new TweetItemViewModel(iNavigationService, u, this)));
        }
    }
}
