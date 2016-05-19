﻿using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml.Data;
using Geopelia.Models;
using Prism.Unity.Windows;
using Microsoft.Practices.Unity;
using Prism.Windows.AppModel;

namespace Geopelia
{
    /// <summary>
    /// 既定の Application クラスを補完するアプリケーション固有の動作を提供します。
    /// </summary>
    [Bindable]
    sealed partial class App : PrismUnityApplication
    {
        /// <summary>
        /// 単一アプリケーション オブジェクトを初期化します。これは、実行される作成したコードの
        ///最初の行であるため、main() または WinMain() と論理的に等価です。
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        protected override Task OnLaunchApplicationAsync(LaunchActivatedEventArgs args)
        {
            this.NavigationService.Navigate("Main", null);
            return Task.CompletedTask;
        }

        protected override void ConfigureContainer()
        {
            base.ConfigureContainer();
            this.Container.RegisterType<TwitterClient>(new ContainerControlledLifetimeManager());
        }
    }
}
