using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    // رفع ابهام UserControl با استفاده صریح از System.Windows.Controls.UserControl
    public partial class QrcodeView : System.Windows.Controls.UserControl, IViewFor<QrcodeViewModel>
    {
        public QrcodeView()
        {
            InitializeComponent();
            ViewModel = new QrcodeViewModel();

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.QrcodeSource, v => v.imgQrcode.Source).DisposeWith(disposables);
            });
        }

        public QrcodeViewModel ViewModel
        {
            get => (QrcodeViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(QrcodeViewModel), typeof(QrcodeView));

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (QrcodeViewModel)value;
        }
    }
}