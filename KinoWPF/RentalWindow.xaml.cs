using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KinoWPF
{
    public partial class RentalWindow : Window
    {
        private List<RentalDto> _all = new List<RentalDto>();
        private rental _editing;

        public RentalWindow()
        {
            InitializeComponent();
            LoadLookups();
            LoadData();
        }

        private void LoadLookups()
        {
            // Загрузка списка проектов для выпадающего списка
            using (var db = new KinoEntities())
                cb_Project.ItemsSource = db.projects.ToList();
        }

        private void LoadData()
        {
            try
            {
                using (var db = new KinoEntities())
                {
                    // Формирование списка записей о прокате для таблицы
                    _all = db.rentals.Select(x => new RentalDto
                    {
                        Id = x.Id,
                        ProjectName = x.project.Name,
                        ReleaseDate = x.Release_date,
                        TheaterCount = x.Number_of_cinemas ?? 0,
                        BoxOffice = x.Collections ?? 0,
                        Rating = x.Audience_rating ?? 0
                    }).ToList();
                }
                dg_Rental.ItemsSource = _all;
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Очистка формы и подготовка к созданию новой записи
            _editing = null;
            ClearForm();
            lbl_FormTitle.Text = "Новая запись о прокате";
            panelForm.Visibility = Visibility.Visible;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Rental.SelectedItem as RentalDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск выбранной записи в базе данных
                    _editing = db.rentals.FirstOrDefault(x => x.Id == dto.Id);
                    if (_editing == null) return;

                    // Заполнение полей формы текущими данными
                    cb_Project.SelectedValue = _editing.Id_project;
                    dp_ReleaseDate.SelectedDate = _editing.Release_date;
                    tb_Theaters.Text = (_editing.Number_of_cinemas ?? 0).ToString();
                    tb_BoxOffice.Text = (_editing.Collections ?? 0).ToString();
                    tb_Rating.Text = (_editing.Audience_rating ?? 0).ToString();
                }
                lbl_FormTitle.Text = "Редактирование записи";
                panelForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Rental.SelectedItem as RentalDto;
            if (dto == null) { MessageBox.Show("Выберите запись."); return; }

            if (MessageBox.Show("Удалить запись?", "Подтверждение",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск и удаление записи из базы данных
                    var item = db.rentals.FirstOrDefault(x => x.Id == dto.Id);
                    if (item != null) db.rentals.Remove(item);
                    db.SaveChanges();
                }
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            lbl_ValidationError.Text = "";

            // Проверка на то, что проект выбран
            if (cb_Project.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите проект."; return; }

            // Проверка на наличие даты выхода
            if (dp_ReleaseDate.SelectedDate == null)
            { lbl_ValidationError.Text = "Укажите дату выхода."; return; }

            // Проверка количества кинотеатров
            int theaters;
            if (!int.TryParse(tb_Theaters.Text, out theaters) || theaters <= 0)
            { lbl_ValidationError.Text = "Количество кинотеатров больше нуля."; return; }

            // Проверка кассовых сборов (должно быть числом, если заполнено)
            decimal box = 0;
            if (!string.IsNullOrEmpty(tb_BoxOffice.Text) &&
                !decimal.TryParse(tb_BoxOffice.Text, out box))
            { lbl_ValidationError.Text = "Сборы — число."; return; }

            // Проверка рейтинга (от 0 до 10)
            decimal rating;
            if (!decimal.TryParse(tb_Rating.Text, out rating) ||
                rating < 0 || rating > 10)
            { lbl_ValidationError.Text = "Рейтинг от 0 до 10."; return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Создание новой или получение существующей записи для сохранения
                    rental r;
                    if (_editing == null) { r = new rental(); db.rentals.Add(r); }
                    else r = db.rentals.First(x => x.Id == _editing.Id);

                    // Присвоение значений из полей формы
                    r.Id_project = (int)cb_Project.SelectedValue;
                    r.Release_date = dp_ReleaseDate.SelectedDate;
                    r.Number_of_cinemas = theaters;
                    r.Collections = box;
                    r.Audience_rating = rating;
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
        }

        private void ClearForm()
        {
            // Сброс всех полей формы
            cb_Project.SelectedIndex = -1;
            dp_ReleaseDate.SelectedDate = null;
            tb_Theaters.Text = "";
            tb_BoxOffice.Text = "";
            tb_Rating.Text = "";
            lbl_ValidationError.Text = "";
        }
    }

    public class RentalDto
    {
        public int Id { get; set; }
        public string ProjectName { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public int TheaterCount { get; set; }
        public decimal BoxOffice { get; set; }
        public decimal Rating { get; set; }
    }
}