using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TODOList.Client.Services;

namespace TODOList.Client
{
    /// <summary>
    /// RegisterWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RegisterWindow : Window
    {
        #region 01 회원가입 창 생성과 닫기
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();        //회원가입 창 닫기
        }
        #endregion

        #region 02 입력 검증과 회원가입 요청
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            //RegisterMessage.Text = "";
            //RegisterMessage.Foreground = Brushes.Red;

            //화면에서 입력한 값 가져오기
            string memberName = RegisterNameBox.Text.Trim();
            string password = RegisterPasswordInput.Password;
            string confirmPassword = ConfirmPasswordInput.Password;

            //1. 이름검사
            if (string.IsNullOrWhiteSpace(memberName))
            {
                RegisterMessage.Text = "가입명을 입력하세요.";
                RegisterNameBox.Focus();
                return;
            }
            // 2. 비밀번호 검사
            if (string.IsNullOrWhiteSpace(password))
            {
                RegisterMessage.Text = "비밀번호를 입력해 주세요.";
                RegisterPasswordInput.Focus();
                return;
            }
            // 3. 비밀번호 확인 검사
            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                RegisterMessage.Text = "비밀번호를 확인해주세요.";
                ConfirmPasswordInput.Focus();
                return;
            }
            // 4. 두 비밀번호가 같은지 검사
            if (password != confirmPassword)
            {
                RegisterMessage.Text = "비밀번호가 일치하지 않습니다.";
                ConfirmPasswordInput.Focus();
                return;
            }

            // 요청 중 중복 클릭 방지
            RegisterButton.IsEnabled = false;
            CancelButton.IsEnabled = false;

            RegisterMessage.Foreground = Brushes.SlateGray;
            RegisterMessage.Text = "회원가입 요청 중입니다.";

            try
            {
                // 입력 검사를 통과한 값을 서버로 전송
                var member = await ApiService.RegisterAsync(memberName, password);

                // 기다리는 동안 창이 닫혔다면 안내창을 띄우지 않습니다.
                if (!IsVisible)
                {
                    return;
                }

                MessageBox.Show(
                    this,
                    $"회원가입이 완료되었습니다.\n" + $"회원번호: {member.MemberID}\n" + "가입명과 비밀번호로 로그인해 주세요.",
                    "회원가입",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // 회원가입 창만 닫습니다.
                Close();
            }
            catch (HttpRequestException)
            {
                RegisterMessage.Foreground = Brushes.IndianRed;
                RegisterMessage.Text = "서버와 통신하지 못했습니다. " + "서버 실행 상태와 주소, HTTPS 인증서를 확인해 주세요.";
            }
            catch (TaskCanceledException)
            {
                RegisterMessage.Foreground = Brushes.IndianRed;
                RegisterMessage.Text = "서버 응답 시간이 초과되었습니다. " + "재가입 전에 회원 목록에서 저장 여부를 확인해 주세요.";
            }
            catch (JsonException)
            {
                RegisterMessage.Foreground = Brushes.IndianRed;
                RegisterMessage.Text = "서버 응답을 읽지 못했습니다. " + "회원 목록에서 저장 여부를 확인해 주세요.";
            }
            catch (InvalidOperationException ex)
            {
                RegisterMessage.Foreground = Brushes.IndianRed;
                RegisterMessage.Text = ex.Message;
            }
            finally
            {
                RegisterButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }
        #endregion
    }
}
