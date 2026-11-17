using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using System.Windows.Media.Imaging;

namespace KinoWPF
{
    public partial class EmployesWindow : Window
    {
        // Список для отображения сотрудников в таблице
        private List<EmployeeDto> _all = new List<EmployeeDto>();

        // Переменная для хранения записи, которую сейчас редактируем
        private employee _editing;

        // Временное хранилище для фотографии в виде массива байтов
        private byte[] _currentPhoto = null;

        public EmployesWindow()
        {
            // Проверка прав доступа: окно доступно только администратору
            if (!MainWindow.IsAdmin)
            {
                MessageBox.Show("Доступ только для администратора.");
                this.Close();
                return;
            }

            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (var db = new KinoEntities())
                {
                    // Загрузка сотрудников из БД и перенос данных DataGride
                    _all = db.employees
                        .AsEnumerable()
                        .Select(x => new EmployeeDto
                        {
                            Id = x.Id,
                            LastName = x.Second_name,
                            FirstName = x.Name,
                            Specialization = x.post != null ? x.post.Name : "",
                            Phone = x.Phone,
                            Email = x.Email,
                            Rate = x.Bet ?? 0
                        })
                        .ToList();
                }
                ApplyFilter();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки: " + ex.Message); }
        }

        private void ApplyFilter()
        {
            // Поиск сотрудников по введенной фамилии
            string q = tb_Search.Text.Trim().ToLower();
            dg_Employees.ItemsSource = string.IsNullOrEmpty(q)
                ? _all
                : _all.Where(x => (x.LastName ?? "").ToLower().Contains(q)).ToList();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Очистка формы и подготовка к созданию новой записи
            _editing = null;
            _currentPhoto = null;
            ImagePhoto.Source = null; // Убираем старое фото

            ClearForm();
            LoadPosts();
            lbl_FormTitle.Text = "Новый сотрудник";
            panelForm.Visibility = Visibility.Visible;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Employees.SelectedItem as EmployeeDto;
            if (dto == null) { MessageBox.Show("Выберите сотрудника."); return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Получение полной записи из базы данных по выбранному ID
                    _editing = db.employees.FirstOrDefault(x => x.Id == dto.Id);
                    if (_editing == null) return;

                    LoadPosts();

                    // Заполнение формы текущими данными сотрудника
                    tb_SecondName.Text = _editing.Second_name;
                    tb_Name.Text = _editing.Name;
                    tb_Bet.Text = (_editing.Bet ?? 0).ToString();
                    cb_Post.SelectedValue = _editing.Id_post;
                    tb_Login.Text = _editing.login;
                    tb_Password.Text = ""; // Пароль всегда скрыт, оставляем пустым

                    tb_Phone.Text = _editing.Phone;
                    tb_Email.Text = _editing.Email;

                    // Загрузка и отображение фото сотрудника
                    _currentPhoto = _editing.Photo;
                    ImagePhoto.Source = ByteArrayToImage(_currentPhoto);
                }

                lbl_FormTitle.Text = "Редактирование сотрудника";
                panelForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Employees.SelectedItem as EmployeeDto;
            if (dto == null) { MessageBox.Show("Выберите сотрудника."); return; }

            // Подтверждение удаления
            if (MessageBox.Show("Удалить сотрудника?", "Подтверждение",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Проверка на наличие связанных записей (съемки, затраты)
                    if (db.film_crew.Any(x => x.Id_employee == dto.Id) ||
                        db.expenses.Any(x => x.Id_employee == dto.Id))
                    {
                        MessageBox.Show("Нельзя удалить: есть связанные записи.");
                        return;
                    }

                    var emp = db.employees.FirstOrDefault(x => x.Id == dto.Id);
                    if (emp != null)
                    {
                        // Сначала удаляем учетную запись пользователя
                        var acc = db.acess_right.FirstOrDefault(a => a.login == emp.login);
                        if (acc != null) db.acess_right.Remove(acc);

                        // Затем удаляем самого сотрудника
                        db.employees.Remove(emp);
                    }
                    db.SaveChanges();
                }
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            lbl_ValidationError.Text = "";

            // Проверка обязательных текстовых полей
            if (string.IsNullOrEmpty(tb_SecondName.Text))
            { lbl_ValidationError.Text = "Фамилия обязательна."; return; }

            if (string.IsNullOrEmpty(tb_Name.Text))
            { lbl_ValidationError.Text = "Имя обязательно."; return; }

            // Проверка телефона и email на корректность
            if (!IsValidPhone(tb_Phone.Text))
            { lbl_ValidationError.Text = "Телефон в формате +7XXXXXXXXXX или 8XXXXXXXXXX."; return; }

            if (!tb_Email.Text.Contains("@"))
            { lbl_ValidationError.Text = "Email должен содержать @."; return; }

            // Проверка ставки на положительное значение
            decimal bet;
            if (!decimal.TryParse(tb_Bet.Text, out bet) || bet <= 0)
            { lbl_ValidationError.Text = "Ставка больше нуля."; return; }

            if (cb_Post.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите должность."; return; }

            // Проверка логина и пароля для авторизации
            string login = tb_Login.Text.Trim();
            string password = tb_Password.Text;

            if (string.IsNullOrEmpty(login))
            { lbl_ValidationError.Text = "Логин обязателен."; return; }

            // Пароль обязателен только при создании нового пользователя
            if (_editing == null && string.IsNullOrEmpty(password))
            { lbl_ValidationError.Text = "Пароль обязателен для нового сотрудника."; return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Проверка, не занят ли такой логин кем-то другим
                    var existingAcc = db.acess_right.FirstOrDefault(a => a.login == login);

                    if (_editing == null && existingAcc != null)
                    { lbl_ValidationError.Text = "Этот логин уже занят другим сотрудником."; return; }

                    if (_editing != null && _editing.login != login && existingAcc != null)
                    { lbl_ValidationError.Text = "Этот логин уже занят другим сотрудником."; return; }

                    // Обработка учетной записи в таблице доступов
                    acess_right accToUpdate = null;

                    if (_editing == null)
                    {
                        // Для нового сотрудника создаем учетную запись
                        accToUpdate = new acess_right { login = login };
                        db.acess_right.Add(accToUpdate);
                    }
                    else if (_editing.login != login)
                    {
                        // Если логин сменился, удаляем старую запись и создаем новую
                        var oldAcc = db.acess_right.FirstOrDefault(a => a.login == _editing.login);
                        if (oldAcc != null) db.acess_right.Remove(oldAcc);

                        accToUpdate = new acess_right { login = login };
                        db.acess_right.Add(accToUpdate);
                    }
                    else
                    {
                        // Если логин не менялся, просто берем текущую запись
                        accToUpdate = db.acess_right.FirstOrDefault(a => a.login == login);
                    }

                    // Если введен пароль, генерируем соль и хешируем его
                    if (!string.IsNullOrEmpty(password))
                    {
                        byte[] salt = AutorizationWindow.GenerateSalt();
                        accToUpdate.soled = salt;
                        accToUpdate.password = AutorizationWindow.HashPasswordWithArgon2id(password, salt);
                    }

                    // Сохранение данных самого сотрудника
                    employee emp;
                    if (_editing == null) { emp = new employee(); db.employees.Add(emp); }
                    else emp = db.employees.First(x => x.Id == _editing.Id);

                    emp.Second_name = tb_SecondName.Text.Trim();
                    emp.Name = tb_Name.Text.Trim();

                    // Запись контактных данных
                    emp.Phone = tb_Phone.Text.Trim();
                    emp.Email = tb_Email.Text.Trim();

                    emp.Bet = bet;
                    emp.Id_post = (int)cb_Post.SelectedValue;
                    emp.login = login;

                    // Сохранение фотографии в базу данных
                    emp.Photo = _currentPhoto;

                    db.SaveChanges();
                }

                panelForm.Visibility = Visibility.Collapsed;
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Скрытие формы без сохранения изменений
            panelForm.Visibility = Visibility.Collapsed;
            _editing = null;
            _currentPhoto = null;
            ImagePhoto.Source = null;
        }

        private void LoadPosts()
        {
            // Загрузка списка должностей для выпадающего списка
            using (var db = new KinoEntities())
                cb_Post.ItemsSource = db.posts.ToList();
        }

        private void ClearForm()
        {
            // Очистка всех полей ввода на форме
            tb_SecondName.Text = "";
            tb_Name.Text = "";
            tb_Phone.Text = "";
            tb_Email.Text = "";
            tb_Bet.Text = "";
            tb_Login.Text = "";
            tb_Password.Text = "";
            cb_Post.SelectedIndex = -1;
            lbl_ValidationError.Text = "";
        }

        private bool IsValidPhone(string p)
        {
            // Проверка формата телефона (российский формат)
            if (string.IsNullOrEmpty(p)) return false;
            p = p.Replace(" ", "").Replace("-", "");
            return (p.StartsWith("+7") && p.Length == 12) ||
                   (p.StartsWith("8") && p.Length == 11);
        }

        // Обработка загрузки фото
        private void BtnDownloadPhoto_Click(object sender, RoutedEventArgs e)
        {
            // Открытие диалога выбора графического файла
            var dlg = new OpenFileDialog
            {
                Filter = "Image|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dlg.ShowDialog() == true)
            {
                // Чтение файла и его отображение
                _currentPhoto = File.ReadAllBytes(dlg.FileName);
                ImagePhoto.Source = ByteArrayToImage(_currentPhoto);
            }
        }

        // Конвертация массива байтов в изображение
        public BitmapImage ByteArrayToImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;

            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
    }

    // Класс-обертка (DTO) для отображения сотрудников в таблице
    public class EmployeeDto
    {
        public int Id { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Specialization { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public decimal Rate { get; set; }
    }
}