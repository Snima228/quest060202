using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Konscious.Security.Cryptography;

namespace KinoWPF
{
    public partial class AutorizationWindow : Window
    {
        public AutorizationWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = tb_Login.Text.Trim();
            string password = tb_Password.Text;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль.");
                return;
            }

            try
            {
                using (var db = new KinoEntities())
                {
                    var acc = db.acess_right.FirstOrDefault(x => x.login == login);
                    if (acc == null)
                    {
                        MessageBox.Show("Такого пользователя нет в системе!");
                        return;
                    }

                    string inputHash = HashPasswordWithArgon2id(password, acc.soled);
                    if (acc.password != inputHash)
                    {
                        MessageBox.Show("Не верный пароль!");
                        return;
                    }

                    var emp = db.employees.FirstOrDefault(x => x.login == login);
                    if (emp == null)
                    {
                        MessageBox.Show("Учётная запись не привязана к сотруднику.");
                        return;
                    }

                    // Сохраняем сессию в MainWindow
                    MainWindow.CurrentUser = emp;
                    MainWindow.Role = emp.post != null ? emp.post.Name : "Сотрудник";

                    new MainWindow().Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения к БД: " + ex.Message);
            }
        }

        public static string HashPasswordWithArgon2id(string password, byte[] salt)
        {
            using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
            {
                argon2.Salt = salt;
                argon2.DegreeOfParallelism = 8;
                argon2.Iterations = 4;
                argon2.MemorySize = 1024 * 1024;
                byte[] hash = argon2.GetBytes(32);
                return Convert.ToBase64String(hash);
            }
        }

        public static byte[] GenerateSalt()
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);
            return salt;
        }
    }
}