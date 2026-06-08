using System;
using System.Windows.Threading;
using AgvDispatcher.Core.Events;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Shell.ViewModels
{
    public class HeaderViewModel : BindableBase
    {
        private string _currentPageTitle = string.Empty;
        public string CurrentPageTitle
        {
            get => _currentPageTitle;
            set => SetProperty(ref _currentPageTitle, value);
        }

        private string _currentTime = string.Empty;
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public HeaderViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.GetEvent<NavigationTitleEvent>().Subscribe(title =>
            {
                CurrentPageTitle = string.IsNullOrWhiteSpace(title) ? "" : $" · {title}";
            });

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, e) =>
            {
                var now = DateTime.Now;
                string dayOfWeek = now.DayOfWeek switch
                {
                    DayOfWeek.Sunday => "星期日",
                    DayOfWeek.Monday => "星期一",
                    DayOfWeek.Tuesday => "星期二",
                    DayOfWeek.Wednesday => "星期三",
                    DayOfWeek.Thursday => "星期四",
                    DayOfWeek.Friday => "星期五",
                    DayOfWeek.Saturday => "星期六",
                    _ => ""
                };
                CurrentTime = $"{now:yyyy-MM-dd HH:mm:ss}  {dayOfWeek}";
            };
            timer.Start();
            
            // Call once immediately
            var initNow = DateTime.Now;
            CurrentTime = $"{initNow:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
