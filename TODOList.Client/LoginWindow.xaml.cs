using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TODOList.Client.Services;

namespace TODOList.Client
{
    /// <summary>
    /// LoginWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LoginWindow : Window
    {
        #region 01 로그인 창 초기화
        public LoginWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region 02 회원가입 창 열기
        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Owner = this;        //현재 로그인 창을 회원가입 창의 부모 창으로 지정
            registerWindow.ShowDialog();
        }
        #endregion

        #region 03 입력 검증 로그인과 메인 창 전환
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginMessage.Text = "";
            LoginMessage.Foreground = Brushes.IndianRed;

            string memberName = NameBox.Text.Trim();
            string password = PasswordInput.Password;

            // 1. 이름검사
            if (string.IsNullOrWhiteSpace(memberName))
            {
                LoginMessage.Text = "가입명을 입력해 주세요.";
                NameBox.Focus();
                return;
            }
            // 2. 비밀번호 검사
            if (string.IsNullOrWhiteSpace(password))
            {
                LoginMessage.Text = "비밀번호를 입력해 주세요.";
                PasswordInput.Focus();
                return;
            }
            // 요청중에 다른창을못열게, 중복클릭 방지
            LoginButton.IsEnabled = false;
            RegisterButton.IsEnabled = false;

            LoginMessage.Foreground = Brushes.SlateGray;
            LoginMessage.Text = "로그인 중입니다.";

            try
            {
                // 서버에 로그인 요청
                var member = await ApiService.LoginAsync(memberName, password);

                if (!IsVisible)
                {
                    return;
                }

                //로그인한 회원 정보 기억
                UserSession.CurrentMember = member;

                //입력한 비밀번호 비우기
                PasswordInput.Clear();

                //일정 관리 화면 생성
                MainWindow mainWindow = new MainWindow();

                //로그인한 회원을 확인할 수 있게 창 제목 설정
                mainWindow.Title = $"TODO - {member.MemberName}님의 일정관리";

                //로그인 후에는 앱의 대표창을 일정관리창으로 변경
                Application.Current.MainWindow = mainWindow;

                //일정 창을  먼저 열고 로그인 창 닫기
                mainWindow.Show();
                Close();
            }
            catch (HttpRequestException)
            {
                LoginMessage.Foreground = Brushes.IndianRed;
                LoginMessage.Text = "서버와 통신하지 못했습니다. " + "서버 실행 상태와 주소, HTTPS 인증서를 확인해 주세요.";
            }
            catch (TaskCanceledException)
            {
                LoginMessage.Foreground = Brushes.IndianRed;
                LoginMessage.Text = "서버 응답 시간이 초과되었습니다. ";
            }
            catch (JsonException)
            {
                LoginMessage.Foreground = Brushes.IndianRed;
                LoginMessage.Text = "서버의 회원 정보를 읽지 못했습니다. ";
            }
            catch (InvalidOperationException ex)
            {
                LoginMessage.Foreground = Brushes.IndianRed;
                LoginMessage.Text = ex.Message;
            }
            finally
            {
                LoginButton.IsEnabled = true;
                RegisterButton.IsEnabled = true;
            }
        }
        #endregion
    }
}
