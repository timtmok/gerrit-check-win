using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Gerrit_Check.Model;
// ReSharper disable LocalizableElement

namespace Gerrit_Check
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const int TooltipDuration = 5000;
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _defaultIcon = System.Drawing.Icon.FromHandle(Properties.Resources.Gerrit_16x16.GetHicon());
        private readonly Icon _readyIcon = System.Drawing.Icon.FromHandle(Properties.Resources.Ready.GetHicon());
        private readonly Icon _pendingIcon = System.Drawing.Icon.FromHandle(Properties.Resources.Pending.GetHicon());
        private readonly ReviewsModel _reviewsModel;

        public MainWindow()
        {
            InitializeComponent();
            _reviewsModel = new ReviewsModel
            {
                Server = ServerText.Text,
                Project = ProjectText.Text,
                Username = UserText.Text
            };
            _reviewsModel.OnUpdated += ReviewsModelOnOnUpdated;
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = _defaultIcon;
            _notifyIcon.Click += NotifyIcon_Click;
            HideApplication();
        }

        private void ReviewsModelOnOnUpdated(UpdateStatus status)
        {
            if (status.HasNewSubmittable)
            {
                var commitWord = _reviewsModel.SubmittableReviews > 1 ? "commits" : "commit";
                ShowTooltip("Ready to submit", $"{_reviewsModel.SubmittableReviews} {commitWord} ready to submit", _readyIcon);
            }
            else if (status.HasNewPending)
            {
                var commitWord = _reviewsModel.PendingReviews > 1 ? "commits" : "commit";
                ShowTooltip("Pending Reviews", $"{_reviewsModel.PendingReviews} {commitWord} to review", _pendingIcon);
            }
            else if (_reviewsModel.PendingReviews == 0 && _reviewsModel.SubmittableReviews == 0)
            {
                _notifyIcon.Icon = _defaultIcon;
            }
        }

        private void ShowTooltip(string title, string tipText, Icon icon)
        {
            _notifyIcon.Icon = icon;
            _notifyIcon.BalloonTipText = tipText;
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.ShowBalloonTip(TooltipDuration);
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            var mouseEvent = e as MouseEventArgs;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (mouseEvent?.Button)
            {
                case MouseButtons.Left:
                    RestoreApplication();
                    break;
                case MouseButtons.Right:
                    break;
            }
        }

        private void RestoreApplication()
        {
            Show();
            WindowState = WindowState.Normal;
            _notifyIcon.Visible = false;
        }

        private void MainWindow_OnStateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                HideApplication();
            }
        }

        private void HideApplication()
        {
            Hide();
            _notifyIcon.Visible = true;
        }

        private void ApplyButton_OnClick(object sender, RoutedEventArgs e)
        {
            HideApplication();
            _reviewsModel.InitModel(ServerText.Text, ProjectText.Text, UserText.Text);
        }
    }
}

