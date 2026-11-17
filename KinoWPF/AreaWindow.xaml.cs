using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KinoWPF
{
    public partial class AreaWindow : Window
    {
        private List<AreaDto> _all = new List<AreaDto>();
        private area _editing;

        public AreaWindow()
        {
            InitializeComponent();
            LoadLookups();
            LoadData();
        }

        private void LoadLookups()
        {
            // Загрузка справочников городов и типов площадок
            using (var db = new KinoEntities())
            {
                cb_City.ItemsSource = db.cities.ToList();
                cb_TypeArea.ItemsSource = db.type_area.ToList();
            }
        }

        private void Cb_City_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Динамическая загрузка улиц при выборе города
            var cityId = cb_City.SelectedValue as int?;
            if (cityId == null) return;
            using (var db = new KinoEntities())
                cb_Street.ItemsSource = db.streets.Where(s => s.Id_city == cityId).ToList();
        }

        private void LoadData()
        {
            try
            {
                using (var db = new KinoEntities())
                {
                    // Формирование списка площадок для таблицы с объединением адреса
                    _all = db.areas.Select(a => new AreaDto
                    {
                        Id = a.Id,
                        Address = a.adress != null && a.adress.street != null
                            ? a.adress.street.city.Name + ", " +
                              a.adress.street.Name + ", д." + a.adress.Home
                            : "",
                        Type = a.type_area != null ? a.type_area.Name : "",
                        RentalPrice = a.Rental_cost ?? 0,
                        Capacity = a.Capacity ?? 0,
                        IdCity = a.adress != null && a.adress.street != null
                            ? a.adress.street.Id_city : null,
                        IdStreet = a.adress != null ? a.adress.Id_street : null,
                        Home = a.adress != null ? a.adress.Home : ""
                    }).ToList();
                }
                dg_Areas.ItemsSource = _all;
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Подготовка формы для создания новой площадки
            _editing = null;
            ClearForm();
            lbl_FormTitle.Text = "Новая площадка";
            panelForm.Visibility = Visibility.Visible;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Areas.SelectedItem as AreaDto;
            if (dto == null) { MessageBox.Show("Выберите площадку."); return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    // Поиск выбранной площадки в базе данных
                    _editing = db.areas.FirstOrDefault(x => x.Id == dto.Id);
                    if (_editing == null) return;
                }

                // Заполнение полей формы текущими данными
                if (dto.IdCity != null) cb_City.SelectedValue = (int)dto.IdCity;
                if (dto.IdStreet != null) cb_Street.SelectedValue = (int)dto.IdStreet;
                tb_Home.Text = dto.Home;
                cb_TypeArea.SelectedValue = _editing.Id_type_area;
                tb_RentalCost.Text = (_editing.Rental_cost ?? 0).ToString();
                tb_Capacity.Text = (_editing.Capacity ?? 0).ToString();

                lbl_FormTitle.Text = "Редактирование площадки";
                panelForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var dto = dg_Areas.SelectedItem as AreaDto;
            if (dto == null) { MessageBox.Show("Выберите площадку."); return; }

            if (MessageBox.Show("Удалить площадку?", "Подтверждение",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new KinoEntities())
                {
                    // Проверка наличия связанных съемок перед удалением
                    if (db.shooting_schedule.Any(x => x.Id_area == dto.Id))
                    {
                        MessageBox.Show("Нельзя удалить: есть съёмки на площадке.");
                        return;
                    }

                    var a = db.areas.FirstOrDefault(x => x.Id == dto.Id);
                    if (a != null) db.areas.Remove(a);
                    db.SaveChanges();
                }
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            lbl_ValidationError.Text = "";

            // Проверка заполнения обязательных полей
            if (cb_City.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите город."; return; }

            if (cb_Street.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите улицу."; return; }

            if (string.IsNullOrEmpty(tb_Home.Text))
            { lbl_ValidationError.Text = "Укажите дом."; return; }

            if (cb_TypeArea.SelectedValue == null)
            { lbl_ValidationError.Text = "Выберите тип площадки."; return; }

            // Проверка числовых значений
            decimal cost;
            if (!decimal.TryParse(tb_RentalCost.Text, out cost) || cost <= 0)
            { lbl_ValidationError.Text = "Стоимость больше нуля."; return; }

            int cap;
            if (!int.TryParse(tb_Capacity.Text, out cap) || cap <= 0)
            { lbl_ValidationError.Text = "Вместимость больше нуля."; return; }

            try
            {
                using (var db = new KinoEntities())
                {
                    int streetId = (int)cb_Street.SelectedValue;
                    string home = tb_Home.Text.Trim();

                    // Поиск существующего адреса или создание нового
                    var adr = db.adresses
                        .FirstOrDefault(x => x.Id_street == streetId && x.Home == home);
                    if (adr == null)
                    {
                        adr = new adress { Id_street = streetId, Home = home };
                        db.adresses.Add(adr);
                        db.SaveChanges();
                    }

                    // Создание или обновление записи площадки
                    area a;
                    if (_editing == null) { a = new area(); db.areas.Add(a); }
                    else a = db.areas.First(x => x.Id == _editing.Id);

                    a.Id_adress = adr.Id;
                    a.Id_type_area = (int)cb_TypeArea.SelectedValue;
                    a.Rental_cost = cost;
                    a.Capacity = cap;

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
            // Сброс полей формы в исходное состояние
            cb_City.SelectedIndex = -1;
            cb_Street.ItemsSource = null;
            tb_Home.Text = "";
            cb_TypeArea.SelectedIndex = -1;
            tb_RentalCost.Text = "";
            tb_Capacity.Text = "";
            lbl_ValidationError.Text = "";
        }
    }

    public class AreaDto
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string Type { get; set; }
        public decimal RentalPrice { get; set; }
        public int Capacity { get; set; }
        public int? IdCity { get; set; }
        public int? IdStreet { get; set; }
        public string Home { get; set; }
    }
}